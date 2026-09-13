using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph;

public static class GraphBezierGeometry
{
    public const int DefaultSegmentCount = 24;

    public static void BuildDefaultEdge(Vector2 start, Vector2 end, IList<Vector2> output, int segmentCount = DefaultSegmentCount)
    {
        var handleDistance = Math.Max(64.0f, Math.Abs(end.X - start.X) * 0.5f);
        Vector2 control1 = new(start.X + handleDistance, start.Y);
        Vector2 control2 = new(end.X - handleDistance, end.Y);
        SampleCubic(start, control1, control2, end, output, segmentCount);
    }

    public static void SampleCubic(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, IList<Vector2> output, int segmentCount)
    {
        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        output.Clear();
        var clampedSegments = Math.Max(1, segmentCount);
        for (var i = 0; i <= clampedSegments; i++)
        {
            var t = i / (float)clampedSegments;
            output.Add(EvaluateCubic(start, control1, control2, end, t));
        }
    }

    public static Vector2 EvaluateCubic(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var clamped = Math.Clamp(t, 0.0f, 1.0f);
        var oneMinusT = 1.0f - clamped;
        return oneMinusT * oneMinusT * oneMinusT * start
               + 3.0f * oneMinusT * oneMinusT * clamped * control1
               + 3.0f * oneMinusT * clamped * clamped * control2
               + clamped * clamped * clamped * end;
    }
}