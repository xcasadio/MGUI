using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering.Clipping;
using System.Diagnostics;

namespace MGUI.Core.UI;

public enum MGPathLiteCommandType
{
    MoveTo,
    LineTo,
    Close,
}

public readonly record struct MGPathLiteCommand(MGPathLiteCommandType Type, Vector2 Point)
{
    public static MGPathLiteCommand MoveTo(Vector2 point) => new(MGPathLiteCommandType.MoveTo, point);
    public static MGPathLiteCommand LineTo(Vector2 point) => new(MGPathLiteCommandType.LineTo, point);
    public static MGPathLiteCommand Close() => new(MGPathLiteCommandType.Close, Vector2.Zero);
}

public class MGPathLite : MGVertexShapeElementBase
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGPathLiteCommand[] _Commands = Array.Empty<MGPathLiteCommand>();
    public IReadOnlyList<MGPathLiteCommand> Commands
    {
        get => _Commands;
        set
        {
            _Commands = value == null ? Array.Empty<MGPathLiteCommand>() : new List<MGPathLiteCommand>(value).ToArray();
            RebuildGeometry();
            NotifyPropertyChanged(nameof(Commands));
        }
    }

    private MGPathLiteFigure[] _Figures = Array.Empty<MGPathLiteFigure>();

    public MGPathLite(MGWindow window)
        : this(window, Array.Empty<MGPathLiteCommand>(), Color.White, 1f, Color.Transparent)
    {
    }

    public MGPathLite(MGWindow window, IReadOnlyList<MGPathLiteCommand> commands, Color stroke, float strokeThickness, Color fill)
        : base(window, MGElementType.PathLite)
    {
        using (BeginInitializing())
        {
            Commands = commands ?? Array.Empty<MGPathLiteCommand>();
            Stroke = stroke;
            StrokeThickness = strokeThickness;
            Fill = fill;
        }
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (_Figures.Length == 0)
        {
            return;
        }

        var placement = GetPlacement(layoutBounds);
        var origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
        var strokeColor = Stroke * DA.Opacity;
        var hasSolidFill = TryGetSolidFillColor(DA.Opacity, out var fillColor);
        var hasBrushFill = HasVisibleFill && !hasSolidFill;

        for (var i = 0; i < _Figures.Length; i++)
        {
            var figure = _Figures[i];
            if (figure.Points.Count < 2)
            {
                continue;
            }

            if (figure.IsClosed && figure.Points.Count >= 3 && hasBrushFill)
            {
                var clipGeometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(figure.Points, origin);
                DrawClippedFillBrush(DA, placement.Bounds,
                    CreateGeometryClipDefinition(TransformClipBounds(DA, placement.Bounds), clipGeometry, $"{ElementType}.Fill", allowRectangleFallback: true));
            }

            if (figure.IsClosed && figure.Points.Count >= 3 && hasSolidFill && HasVisibleStroke)
            {
                DA.Context.StrokeAndFillPolygon(origin, figure.Points, strokeColor, fillColor, StrokeThickness);
            }
            else
            {
                if (figure.IsClosed && figure.Points.Count >= 3 && hasSolidFill)
                {
                    DA.Context.FillPolygon(origin, figure.Points, fillColor);
                }

                if (HasVisibleStroke)
                {
                    var segmentCount = figure.IsClosed ? figure.Points.Count : figure.Points.Count - 1;
                    for (var segment = 0; segment < segmentCount; segment++)
                    {
                        var start = figure.Points[segment];
                        var end = figure.Points[(segment + 1) % figure.Points.Count];
                        DA.Context.StrokeLineSegment(origin, start, end, strokeColor, StrokeThickness);
                    }
                }
            }
        }
    }

    protected internal override bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
    {
        if (!ActualLayoutBounds.ContainsInclusive(unscaledScreenPosition) || _Figures.Length == 0)
        {
            return false;
        }

        var placement = GetPlacement(LayoutBounds);
        var layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
        var localPoint = layoutPoint - placement.GeometryOrigin;

        for (var i = 0; i < _Figures.Length; i++)
        {
            var figure = _Figures[i];
            if (figure.IsClosed && figure.Points.Count >= 3 && HasVisibleFill && MGVectorShapeHelper.IsPointInPolygon(figure.Points, localPoint))
            {
                return true;
            }

            if (HasVisibleStroke && MGVectorShapeHelper.IsPointNearPolyline(figure.Points, localPoint, StrokeThickness, figure.IsClosed))
            {
                return true;
            }
        }

        return false;
    }

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        if (!ClipToBounds || _Figures.Length != 1)
        {
            return null;
        }

        var figure = _Figures[0];
        var placement = GetPlacement(layoutBounds);
        if (!figure.IsClosed || figure.Points.Count < 3)
        {
            return CreateRectangleClipDefinition(TransformClipBounds(DA, placement.Bounds), $"{ElementType}.Self");
        }

        var geometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(figure.Points, DA.Offset.ToVector2() + placement.GeometryOrigin);
        return CreateGeometryClipDefinition(TransformClipBounds(DA, placement.Bounds), geometry, $"{ElementType}.Self", allowRectangleFallback: true);
    }

    private void RebuildGeometry()
    {
        List<MGPathLiteFigure> rawFigures = new();
        List<Vector2> currentFigure = null;

        void FlushCurrent(bool isClosed)
        {
            if (currentFigure != null && currentFigure.Count > 1)
            {
                rawFigures.Add(new MGPathLiteFigure(currentFigure.ToArray(), isClosed));
            }

            currentFigure = null;
        }

        for (var i = 0; i < _Commands.Length; i++)
        {
            var command = _Commands[i];
            switch (command.Type)
            {
                case MGPathLiteCommandType.MoveTo:
                    FlushCurrent(false);
                    currentFigure = new List<Vector2> { command.Point };
                    break;
                case MGPathLiteCommandType.LineTo:
                    currentFigure ??= new List<Vector2>();
                    currentFigure.Add(command.Point);
                    break;
                case MGPathLiteCommandType.Close:
                    FlushCurrent(true);
                    break;
            }
        }

        FlushCurrent(false);

        if (rawFigures.Count == 0)
        {
            _Figures = Array.Empty<MGPathLiteFigure>();
            GeometrySize = new MonoGame.Extended.Size(0, 0);
            LayoutChanged(this, true);
            return;
        }

        List<Vector2> allPoints = new();
        for (var i = 0; i < rawFigures.Count; i++)
        {
            allPoints.AddRange(rawFigures[i].Points);
        }

        var normalizedAllPoints = MGVectorShapeHelper.NormalizePoints(allPoints.ToArray(), out var bounds);
        Vector2 offset = new(bounds.X, bounds.Y);
        var normalizedFigures = new MGPathLiteFigure[rawFigures.Count];
        for (var i = 0; i < rawFigures.Count; i++)
        {
            var figurePoints = new Vector2[rawFigures[i].Points.Count];
            for (var pointIndex = 0; pointIndex < figurePoints.Length; pointIndex++)
            {
                figurePoints[pointIndex] = rawFigures[i].Points[pointIndex] - offset;
            }

            normalizedFigures[i] = new MGPathLiteFigure(figurePoints, rawFigures[i].IsClosed);
        }

        _Figures = normalizedFigures;
        GeometrySize = new MonoGame.Extended.Size(Math.Max(0, (int)Math.Ceiling(bounds.Width)), Math.Max(0, (int)Math.Ceiling(bounds.Height)));
        LayoutChanged(this, true);
    }
}