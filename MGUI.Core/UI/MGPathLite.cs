using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering.Clipping;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MGUI.Core.UI
{
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
                NPC(nameof(Commands));
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

            MGPointShapePlacement placement = GetPlacement(layoutBounds);
            Vector2 origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
            Color strokeColor = Stroke * DA.Opacity;
            bool hasSolidFill = TryGetSolidFillColor(DA.Opacity, out Color fillColor);
            bool hasBrushFill = HasVisibleFill && !hasSolidFill;

            for (int i = 0; i < _Figures.Length; i++)
            {
                MGPathLiteFigure figure = _Figures[i];
                if (figure.Points.Count < 2)
                {
                    continue;
                }

                if (figure.IsClosed && figure.Points.Count >= 3 && hasBrushFill)
                {
                    ClipGeometry clipGeometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(figure.Points, origin);
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
                        int segmentCount = figure.IsClosed ? figure.Points.Count : figure.Points.Count - 1;
                        for (int segment = 0; segment < segmentCount; segment++)
                        {
                            Vector2 start = figure.Points[segment];
                            Vector2 end = figure.Points[(segment + 1) % figure.Points.Count];
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

            MGPointShapePlacement placement = GetPlacement(LayoutBounds);
            Vector2 layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
            Vector2 localPoint = layoutPoint - placement.GeometryOrigin;

            for (int i = 0; i < _Figures.Length; i++)
            {
                MGPathLiteFigure figure = _Figures[i];
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

            MGPathLiteFigure figure = _Figures[0];
            MGPointShapePlacement placement = GetPlacement(layoutBounds);
            if (!figure.IsClosed || figure.Points.Count < 3)
            {
                return CreateRectangleClipDefinition(TransformClipBounds(DA, placement.Bounds), $"{ElementType}.Self");
            }

            ClipGeometry geometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(figure.Points, DA.Offset.ToVector2() + placement.GeometryOrigin);
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

            for (int i = 0; i < _Commands.Length; i++)
            {
                MGPathLiteCommand command = _Commands[i];
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
            for (int i = 0; i < rawFigures.Count; i++)
            {
                allPoints.AddRange(rawFigures[i].Points);
            }

            Vector2[] normalizedAllPoints = MGVectorShapeHelper.NormalizePoints(allPoints.ToArray(), out MonoGame.Extended.RectangleF bounds);
            Vector2 offset = new(bounds.X, bounds.Y);
            MGPathLiteFigure[] normalizedFigures = new MGPathLiteFigure[rawFigures.Count];
            for (int i = 0; i < rawFigures.Count; i++)
            {
                Vector2[] figurePoints = new Vector2[rawFigures[i].Points.Count];
                for (int pointIndex = 0; pointIndex < figurePoints.Length; pointIndex++)
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
}