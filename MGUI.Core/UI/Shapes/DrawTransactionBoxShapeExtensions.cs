using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Shapes;

public static class DrawTransactionBoxShapeExtensions
{
    public static void FillRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
        int cornerSegmentCount = 8)
    {
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
        FillRoundedRectangle(drawContext, origin, geometry, color);
    }

    public static void FillRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color)
    {
        if (geometry.UsesRectangleFastPath)
        {
            drawContext.FillRectangle(origin, geometry.Shape.OuterBounds, color);
            return;
        }

        DrawTriangleList(drawContext, origin, geometry.Vertices, geometry.FillIndices, color);
    }

    public static void StrokeRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
        int cornerSegmentCount = 8)
    {
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
        StrokeRoundedRectangle(drawContext, origin, geometry, color);
    }

    public static void StrokeRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color)
    {
        if (geometry.UsesRectangleFastPath)
        {
            drawContext.StrokeRectangle(origin, geometry.Shape.OuterBounds, color, geometry.Shape.NormalizedBorderThickness);
            return;
        }

        drawContext.DrawBorderRing(origin, geometry, color);
    }

    public static void DrawBorderRing(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
        int cornerSegmentCount = 8)
    {
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
        drawContext.DrawBorderRing(origin, geometry, color);
    }

    public static void DrawBorderRing(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color)
    {
        if (!geometry.Shape.HasBorder)
        {
            return;
        }

        if (geometry.UsesRectangleFastPath)
        {
            drawContext.StrokeRectangle(origin, geometry.Shape.OuterBounds, color, geometry.Shape.NormalizedBorderThickness);
            return;
        }

        DrawTriangleList(drawContext, origin, geometry.Vertices, geometry.BorderRingIndices, color);
    }

    /// <summary>Textured counterpart of <see cref="FillRoundedRectangle(IUIDrawContext, Vector2, MGBoxGeometry, Color)"/>: emits the fill mesh of
    /// <paramref name="geometry"/> through <see cref="IUIDrawContext.DrawTexturedTriangleList"/>. <paramref name="textureCoordinates"/> holds one
    /// normalized coordinate per entry of <see cref="MGBoxGeometry.Vertices"/> (the paint owns the UV mapping rules).<para/>
    /// No rectangle fast path is decided here: textured paints keep their own rectangle draw path when <see cref="MGBoxGeometry.UsesRectangleFastPath"/> is true.</summary>
    public static void FillTexturedRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, IUIImageResource texture,
        System.Collections.Generic.IReadOnlyList<Vector2> textureCoordinates, Color color)
    {
        if (!geometry.HasFillMesh)
        {
            return;
        }

        drawContext.DrawTexturedTriangleList(origin, texture, geometry.Vertices, textureCoordinates, geometry.FillIndices, color);
    }

    /// <summary>Textured counterpart of <see cref="DrawBorderRing(IUIDrawContext, Vector2, MGBoxGeometry, Color)"/>: emits the whole border ring of
    /// <paramref name="geometry"/> with a single texture. <paramref name="textureCoordinates"/> holds one normalized coordinate per entry of
    /// <see cref="MGBoxGeometry.Vertices"/>. Paints that need distinct textures per edge/corner emit their own index subsets instead.</summary>
    public static void DrawTexturedBorderRing(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, IUIImageResource texture,
        System.Collections.Generic.IReadOnlyList<Vector2> textureCoordinates, Color color)
    {
        if (!geometry.Shape.HasBorder || !geometry.HasBorderRingMesh)
        {
            return;
        }

        drawContext.DrawTexturedTriangleList(origin, texture, geometry.Vertices, textureCoordinates, geometry.BorderRingIndices, color);
    }

    private static void DrawTriangleList(IUIDrawContext drawContext, Vector2 origin, System.Collections.Generic.IReadOnlyList<Vector2> vertices,
        System.Collections.Generic.IReadOnlyList<int> indices, Color color)
    {
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            Vector2 v0 = vertices[indices[i]];
            Vector2 v1 = vertices[indices[i + 1]];
            Vector2 v2 = vertices[indices[i + 2]];
            drawContext.FillTriangle(origin, v0, color, v1, color, v2, color);
        }
    }
}