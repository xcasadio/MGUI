using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MGUI.Samples.Features
{
    public class PerformanceTest : SampleBase
    {
        private MGListBox<object> _listBox;
        private MGTextBlock _tbFps;
        private MGTextBlock _tbFrameMs;
        private MGTextBlock _tbElements;
        private MGTextBlock _tbMode;

        private int _currentItemCount = 5_000;

        // Frame-time measurement
        private readonly Stopwatch _fpsWatch = Stopwatch.StartNew();
        private long _lastTick;
        private int _frameCount;

        // Cached event handler delegate so we can unsubscribe in WindowClosed
        private readonly EventHandler<EventArgs> _onEndUpdate;

        public PerformanceTest(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "PerformanceTest.xaml")
        {
            // --- Retrieve named elements ---
            _listBox  = Window.GetElementByName<MGListBox<object>>("PerfList");
            _tbFps      = Window.GetElementByName<MGTextBlock>("TbFps");
            _tbFrameMs  = Window.GetElementByName<MGTextBlock>("TbFrameMs");
            _tbElements = Window.GetElementByName<MGTextBlock>("TbElements");
            _tbMode     = Window.GetElementByName<MGTextBlock>("TbMode");

            // --- Item-count buttons ---
            Window.GetElementByName<MGButton>("BtnLoad5k").MouseHandler.LMBReleasedInside
                += (_, __) => LoadItems(5_000);
            Window.GetElementByName<MGButton>("BtnLoad10k").MouseHandler.LMBReleasedInside
                += (_, __) => LoadItems(10_000);
            Window.GetElementByName<MGButton>("BtnLoad50k").MouseHandler.LMBReleasedInside
                += (_, __) => LoadItems(50_000);

            // --- Virtualisation-mode buttons ---
            Window.GetElementByName<MGButton>("BtnModeNever").MouseHandler.LMBReleasedInside
                += (_, __) => { _listBox.VirtualizationMode = ListBoxVirtualizationMode.Never;  LoadItems(_currentItemCount); };
            Window.GetElementByName<MGButton>("BtnModeAuto").MouseHandler.LMBReleasedInside
                += (_, __) => { _listBox.VirtualizationMode = ListBoxVirtualizationMode.Auto;   LoadItems(_currentItemCount); };
            Window.GetElementByName<MGButton>("BtnModeAlways").MouseHandler.LMBReleasedInside
                += (_, __) => { _listBox.VirtualizationMode = ListBoxVirtualizationMode.Always; LoadItems(_currentItemCount); };

            // --- Frame-time hook: fires at the end of every Update() tick ---
            _onEndUpdate = OnEndUpdate;
            if (Desktop.Renderer.Host is IObservableUpdate observable)
            {
                observable.EndUpdate += _onEndUpdate;
            }

            // Unsubscribe when the sample window is closed
            Window.WindowClosed += (_, __) =>
            {
                if (Desktop.Renderer.Host is IObservableUpdate obs)
                {
                    obs.EndUpdate -= _onEndUpdate;
                }
            };

            // --- Initial data ---
            _lastTick = _fpsWatch.ElapsedTicks;
            LoadItems(_currentItemCount);
        }

        // -----------------------------------------------------------------------
        private void LoadItems(int count)
        {
            _currentItemCount = count;
            var items = new List<object>(count);
            for (int i = 0; i < count; i++)
                items.Add($"Item {i + 1:N0}");
            _listBox.SetItemsSource(items);
            RefreshStats(0.0);
        }

        // -----------------------------------------------------------------------
        private void OnEndUpdate(object sender, EventArgs e)
        {
            long now     = _fpsWatch.ElapsedTicks;
            double dtMs  = (now - _lastTick) * 1000.0 / Stopwatch.Frequency;
            _lastTick    = now;
            _frameCount++;

            double elapsedSec = _fpsWatch.Elapsed.TotalSeconds;
            if (elapsedSec >= 0.5)
            {
                double fps = _frameCount / elapsedSec;
                _tbFps.Text     = $"FPS: {fps:F1}";
                _tbFrameMs.Text = $"Frame time: {dtMs:F2} ms";
                _frameCount = 0;
                _fpsWatch.Restart();
            }

            RefreshStats(dtMs);
        }

        // -----------------------------------------------------------------------
        private void RefreshStats(double dtMs)
        {
            bool virt = _listBox.IsVirtualizing;
            int realized;

            if (virt)
            {
                VirtualizingStackPanel vsp = _listBox.VirtualizingPanel;
                if (vsp != null)
                {
                    int first = vsp.FirstRealizedIndex;
                    int last  = vsp.LastRealizedIndex;
                    realized = (first >= 0 && last >= first) ? (last - first + 1) : 0;
                }
                else
                {
                    realized = 0;
                }
            }
            else
            {
                realized = _listBox.ListBoxItems?.Count ?? 0;
            }

            _tbElements.Text = $"Elements: total={_currentItemCount:N0}   realized={realized}";
            _tbMode.Text     = $"Mode: {_listBox.VirtualizationMode}  |  Virtualizing: {virt}";
        }
    }
}
