using MGUI.Core.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace MGUI.Core.Tooling
{
    public static class UIPerformanceProbe
    {
        private const string OutputPathEnvironmentVariable = "CASA_MGUI_PERF_PROBE";
        private const string SampleIntervalEnvironmentVariable = "CASA_MGUI_PERF_SAMPLE_INTERVAL";
        private const string TopCountEnvironmentVariable = "CASA_MGUI_PERF_TOP";
        private const int DefaultSampleInterval = 30;
        private const int DefaultTopCount = 12;

        private static readonly object SyncRoot = new();
        private static bool _initialized;
        private static ProbeState _state;

        public static bool IsEnabled
        {
            get
            {
                EnsureInitialized();
                return _state != null;
            }
        }

        public static ElementScope BeginElementUpdate(MGElement element)
            => BeginElementScope(element, ElementOperation.Update);

        public static ElementScope BeginElementDraw(MGElement element)
            => BeginElementScope(element, ElementOperation.Draw);

        public static ElementScope BeginElementLayout(MGElement element)
            => BeginElementScope(element, ElementOperation.Layout);

        public static ElementScope BeginElementMeasure(MGElement element)
            => BeginElementScope(element, ElementOperation.Measure);

        public static void RecordLayoutInvalidation(MGElement source, MGElement receiver, bool notifyParent)
        {
            ProbeState state = GetState();
            state?.RecordLayoutInvalidation(source, receiver, notifyParent);
        }

        public static void FlushFrame(MGDesktop desktop, string phase)
        {
            ProbeState state = GetState();
            state?.FlushFrame(desktop, phase);
        }

        public static DesktopPhaseScope BeginDesktopPhase(string phaseName)
        {
            ProbeState state = GetState();
            return state == null ? default : state.BeginDesktopPhase(phaseName);
        }

        private static ElementScope BeginElementScope(MGElement element, ElementOperation operation)
        {
            ProbeState state = GetState();
            return state == null ? default : state.BeginElementScope(element, operation);
        }

        private static ProbeState GetState()
        {
            EnsureInitialized();
            return _state;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                string rawPath = Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(rawPath))
                {
                    string outputPath = ResolveOutputPath(rawPath);
                    string outputDirectory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    int sampleInterval = ReadPositiveInt(SampleIntervalEnvironmentVariable, DefaultSampleInterval);
                    int topCount = ReadPositiveInt(TopCountEnvironmentVariable, DefaultTopCount);
                    _state = new ProbeState(outputPath, sampleInterval, topCount);
                    File.WriteAllText(outputPath,
                        "CasaEngine MGUI performance probe" + Environment.NewLine +
                        $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine +
                        $"SampleInterval: {sampleInterval}" + Environment.NewLine +
                        $"TopCount: {topCount}" + Environment.NewLine + Environment.NewLine);
                }

                _initialized = true;
            }
        }

        private static string ResolveOutputPath(string rawPath)
        {
            if (string.Equals(rawPath, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawPath, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath("mgui-perf-probe.txt");
            }

            return Path.GetFullPath(rawPath);
        }

        private static int ReadPositiveInt(string environmentVariable, int fallback)
        {
            string rawValue = Environment.GetEnvironmentVariable(environmentVariable);
            return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
                ? value
                : fallback;
        }

        public readonly struct ElementScope : IDisposable
        {
            private readonly ProbeState _state;
            private readonly int _scopeIndex;

            internal ElementScope(ProbeState state, int scopeIndex)
            {
                _state = state;
                _scopeIndex = scopeIndex;
            }

            public void Dispose()
            {
                if (_state == null)
                {
                    return;
                }

                _state.EndElementScope(_scopeIndex);
            }
        }

        public readonly struct DesktopPhaseScope : IDisposable
        {
            private readonly ProbeState _state;
            private readonly string _phaseName;
            private readonly long _startTimestamp;

            internal DesktopPhaseScope(ProbeState state, string phaseName)
            {
                _state = state;
                _phaseName = phaseName;
                _startTimestamp = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_state == null || string.IsNullOrWhiteSpace(_phaseName))
                {
                    return;
                }

                _state.RecordDesktopPhase(_phaseName, Stopwatch.GetTimestamp() - _startTimestamp);
            }
        }

        internal enum ElementOperation
        {
            Update,
            Draw,
            Layout,
            Measure,
        }

        internal sealed class ProbeState
        {
            private readonly string _outputPath;
            private readonly int _sampleInterval;
            private readonly int _topCount;
            private readonly Dictionary<string, ElementMetrics> _metricsByElementId = new(StringComparer.Ordinal);
            private readonly List<ActiveScope> _activeScopes = new();
            private int _frameIndex;
            private long _updateTicks;
            private long _drawTicks;
            private long _layoutTicks;
            private long _measureTicks;
            private int _updateCount;
            private int _drawCount;
            private int _layoutCount;
            private int _measureCount;
            private int _layoutInvalidationCount;
            private int _layoutPropagationCount;
            private readonly Dictionary<string, DesktopPhaseMetric> _desktopPhases = new(StringComparer.Ordinal);

            public ProbeState(string outputPath, int sampleInterval, int topCount)
            {
                _outputPath = outputPath;
                _sampleInterval = sampleInterval;
                _topCount = topCount;
            }

            public ElementScope BeginElementScope(MGElement element, ElementOperation operation)
            {
                int scopeIndex = _activeScopes.Count;
                _activeScopes.Add(new ActiveScope(element, operation, Stopwatch.GetTimestamp()));
                return new ElementScope(this, scopeIndex);
            }

            public DesktopPhaseScope BeginDesktopPhase(string phaseName)
                => new(this, phaseName);

            public void EndElementScope(int scopeIndex)
            {
                if (scopeIndex < 0 || scopeIndex >= _activeScopes.Count)
                {
                    return;
                }

                long now = Stopwatch.GetTimestamp();
                ActiveScope activeScope = _activeScopes[scopeIndex];
                long totalTicks = now - activeScope.StartTimestamp;
                long selfTicks = Math.Max(0, totalTicks - activeScope.ChildTicks);

                if (scopeIndex == _activeScopes.Count - 1)
                {
                    _activeScopes.RemoveAt(scopeIndex);
                    if (_activeScopes.Count > 0)
                    {
                        int parentIndex = _activeScopes.Count - 1;
                        ActiveScope parentScope = _activeScopes[parentIndex];
                        parentScope.ChildTicks += totalTicks;
                        _activeScopes[parentIndex] = parentScope;
                    }
                }
                else
                {
                    _activeScopes.Clear();
                }

                RecordElementOperation(activeScope.Element, activeScope.Operation, totalTicks, selfTicks);
            }

            public void RecordElementOperation(MGElement element, ElementOperation operation, long totalTicks, long selfTicks)
            {
                if (totalTicks <= 0)
                {
                    return;
                }

                ElementMetrics metrics = GetMetrics(element);
                switch (operation)
                {
                    case ElementOperation.Update:
                        metrics.UpdateTicks += totalTicks;
                        metrics.UpdateSelfTicks += selfTicks;
                        metrics.UpdateCount++;
                        _updateTicks += selfTicks;
                        _updateCount++;
                        break;
                    case ElementOperation.Draw:
                        metrics.DrawTicks += totalTicks;
                        metrics.DrawSelfTicks += selfTicks;
                        metrics.DrawCount++;
                        _drawTicks += selfTicks;
                        _drawCount++;
                        break;
                    case ElementOperation.Layout:
                        metrics.LayoutTicks += totalTicks;
                        metrics.LayoutSelfTicks += selfTicks;
                        metrics.LayoutCount++;
                        _layoutTicks += selfTicks;
                        _layoutCount++;
                        break;
                    case ElementOperation.Measure:
                        metrics.MeasureTicks += totalTicks;
                        metrics.MeasureSelfTicks += selfTicks;
                        metrics.MeasureCount++;
                        _measureTicks += selfTicks;
                        _measureCount++;
                        break;
                }
            }

            public void RecordLayoutInvalidation(MGElement source, MGElement receiver, bool notifyParent)
            {
                if (source != null)
                {
                    GetMetrics(source).LayoutInvalidationSourceCount++;
                }

                if (receiver != null && !ReferenceEquals(source, receiver))
                {
                    GetMetrics(receiver).LayoutInvalidationReceiverCount++;
                }

                _layoutInvalidationCount++;
                if (notifyParent)
                {
                    _layoutPropagationCount++;
                }
            }

            public void RecordDesktopPhase(string phaseName, long ticks)
            {
                if (ticks <= 0 || string.IsNullOrWhiteSpace(phaseName))
                {
                    return;
                }

                if (!_desktopPhases.TryGetValue(phaseName, out DesktopPhaseMetric metric))
                {
                    metric = new DesktopPhaseMetric();
                }

                metric.Ticks += ticks;
                metric.Count++;
                _desktopPhases[phaseName] = metric;
            }

            public void FlushFrame(MGDesktop desktop, string phase)
            {
                _frameIndex++;
                if (_frameIndex % _sampleInterval != 0)
                {
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine($"[MGUI Perf] frame={_frameIndex} phase={phase} time={desktop?.Runtime.UpdateArgs.TotalElapsed:c}");
                builder.AppendLine($"  totals: updateSelf={FormatMilliseconds(_updateTicks)}ms/{_updateCount} drawSelf={FormatMilliseconds(_drawTicks)}ms/{_drawCount} layoutSelf={FormatMilliseconds(_layoutTicks)}ms/{_layoutCount} measureSelf={FormatMilliseconds(_measureTicks)}ms/{_measureCount} invalidations={_layoutInvalidationCount} propagated={_layoutPropagationCount}");
                AppendDesktopPhases(builder);
                AppendTopElements(builder, "update", static metrics => metrics.UpdateSelfTicks, static metrics => metrics.UpdateTicks, static metrics => metrics.UpdateCount);
                AppendTopElements(builder, "draw", static metrics => metrics.DrawSelfTicks, static metrics => metrics.DrawTicks, static metrics => metrics.DrawCount);
                AppendTopElements(builder, "layout", static metrics => metrics.LayoutSelfTicks, static metrics => metrics.LayoutTicks, static metrics => metrics.LayoutCount);
                AppendTopElements(builder, "measure", static metrics => metrics.MeasureSelfTicks, static metrics => metrics.MeasureTicks, static metrics => metrics.MeasureCount);
                AppendTopInvalidations(builder);
                builder.AppendLine();

                File.AppendAllText(_outputPath, builder.ToString());
                ResetInterval();
            }

            private ElementMetrics GetMetrics(MGElement element)
            {
                string id = element.UniqueId;
                if (!_metricsByElementId.TryGetValue(id, out ElementMetrics metrics))
                {
                    metrics = new ElementMetrics(id, element);
                    _metricsByElementId.Add(id, metrics);
                }

                return metrics;
            }

            private void AppendTopElements(StringBuilder builder, string label, Func<ElementMetrics, long> getSelfTicks, Func<ElementMetrics, long> getTotalTicks, Func<ElementMetrics, int> getCount)
            {
                var metrics = new List<ElementMetrics>(_metricsByElementId.Values);
                metrics.Sort((left, right) => getSelfTicks(right).CompareTo(getSelfTicks(left)));
                builder.AppendLine($"  top {label} self-time:");
                AppendMetricList(builder, metrics, getSelfTicks, getTotalTicks, getCount, includeInvalidations: false);
            }

            private void AppendTopInvalidations(StringBuilder builder)
            {
                var metrics = new List<ElementMetrics>(_metricsByElementId.Values);
                metrics.Sort((left, right) => right.TotalLayoutInvalidations.CompareTo(left.TotalLayoutInvalidations));
                builder.AppendLine("  top layout invalidations:");
                AppendMetricList(builder, metrics, static metric => metric.TotalLayoutInvalidations, static metric => metric.TotalLayoutInvalidations, static metric => metric.TotalLayoutInvalidations, includeInvalidations: true);
            }

            private void AppendDesktopPhases(StringBuilder builder)
            {
                builder.AppendLine("  desktop phases:");
                if (_desktopPhases.Count == 0)
                {
                    builder.AppendLine("    <none>");
                    return;
                }

                var phases = new List<KeyValuePair<string, DesktopPhaseMetric>>(_desktopPhases);
                phases.Sort((left, right) => right.Value.Ticks.CompareTo(left.Value.Ticks));
                for (int index = 0; index < phases.Count; index++)
                {
                    KeyValuePair<string, DesktopPhaseMetric> phase = phases[index];
                    builder.Append("    ");
                    builder.Append(index + 1);
                    builder.Append(". ");
                    builder.Append(phase.Key);
                    builder.Append(" ms=");
                    builder.Append(FormatMilliseconds(phase.Value.Ticks));
                    builder.Append(" count=");
                    builder.Append(phase.Value.Count.ToString(CultureInfo.InvariantCulture));
                    builder.AppendLine();
                }
            }

            private void AppendMetricList(StringBuilder builder, List<ElementMetrics> metrics, Func<ElementMetrics, long> getValue, Func<ElementMetrics, long> getTotalValue, Func<ElementMetrics, int> getCount, bool includeInvalidations)
            {
                int written = 0;
                for (int i = 0; i < metrics.Count && written < _topCount; i++)
                {
                    ElementMetrics metric = metrics[i];
                    long value = getValue(metric);
                    int count = getCount(metric);
                    if (value <= 0 || count <= 0)
                    {
                        continue;
                    }

                    written++;
                    builder.Append("    ");
                    builder.Append(written.ToString(CultureInfo.InvariantCulture));
                    builder.Append(". ");
                    if (includeInvalidations)
                    {
                        builder.Append("invalidations=");
                        builder.Append(value.ToString(CultureInfo.InvariantCulture));
                        builder.Append(" source=");
                        builder.Append(metric.LayoutInvalidationSourceCount.ToString(CultureInfo.InvariantCulture));
                        builder.Append(" receiver=");
                        builder.Append(metric.LayoutInvalidationReceiverCount.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append("self=");
                        builder.Append(FormatMilliseconds(value));
                        builder.Append("ms total=");
                        builder.Append(FormatMilliseconds(getTotalValue(metric)));
                        builder.Append("ms count=");
                        builder.Append(count.ToString(CultureInfo.InvariantCulture));
                    }

                    builder.Append(" element=");
                    AppendElementDescription(builder, metric.Element);
                    builder.AppendLine();
                }

                if (written == 0)
                {
                    builder.AppendLine("    <none>");
                }
            }

            private static void AppendElementDescription(StringBuilder builder, MGElement element)
            {
                builder.Append(element.GetType().Name);
                builder.Append("#");
                builder.Append(element.UniqueId);
                builder.Append(" type=");
                builder.Append(element.ElementType);

                if (!string.IsNullOrWhiteSpace(element.Name))
                {
                    builder.Append(" name=");
                    builder.Append(element.Name);
                }

                builder.Append(" bounds=");
                builder.Append(element.LayoutBounds);
                builder.Append(" path=");
                builder.Append(TryGetStablePath(element));
            }

            private static string TryGetStablePath(MGElement element)
            {
                try
                {
                    return UIToolingService.GetStableDiagnosticId(element);
                }
                catch
                {
                    return "<detached>";
                }
            }

            private void ResetInterval()
            {
                _metricsByElementId.Clear();
                _updateTicks = 0;
                _drawTicks = 0;
                _layoutTicks = 0;
                _measureTicks = 0;
                _updateCount = 0;
                _drawCount = 0;
                _layoutCount = 0;
                _measureCount = 0;
                _layoutInvalidationCount = 0;
                _layoutPropagationCount = 0;
                _desktopPhases.Clear();
                _activeScopes.Clear();
            }

            private static string FormatMilliseconds(long ticks)
                => (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private sealed class ElementMetrics
        {
            public ElementMetrics(string uniqueId, MGElement element)
            {
                UniqueId = uniqueId;
                Element = element;
            }

            public string UniqueId { get; }
            public MGElement Element { get; }
            public long UpdateTicks;
            public long UpdateSelfTicks;
            public long DrawTicks;
            public long DrawSelfTicks;
            public long LayoutTicks;
            public long LayoutSelfTicks;
            public long MeasureTicks;
            public long MeasureSelfTicks;
            public int UpdateCount;
            public int DrawCount;
            public int LayoutCount;
            public int MeasureCount;
            public int LayoutInvalidationSourceCount;
            public int LayoutInvalidationReceiverCount;
            public int TotalLayoutInvalidations => LayoutInvalidationSourceCount + LayoutInvalidationReceiverCount;
        }

        private struct ActiveScope
        {
            public ActiveScope(MGElement element, ElementOperation operation, long startTimestamp)
            {
                Element = element;
                Operation = operation;
                StartTimestamp = startTimestamp;
                ChildTicks = 0;
            }

            public MGElement Element;
            public ElementOperation Operation;
            public long StartTimestamp;
            public long ChildTicks;
        }

        private struct DesktopPhaseMetric
        {
            public long Ticks;
            public int Count;
        }
    }
}