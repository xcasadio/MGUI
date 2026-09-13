using Microsoft.Xna.Framework;
using System.ComponentModel;
using System.Globalization;

namespace MGUI.Core.UI.XAML;

internal static class ShapeXamlParser
{
    public static Vector2 ParsePoint(string value)
    {
        var values = ParseNumbers(value);
        if (values.Length != 2)
        {
            throw new ArgumentException(value);
        }

        return new Vector2(values[0], values[1]);
    }

    public static Vector2[] ParsePoints(string value)
    {
        var values = ParseNumbers(value);
        if (values.Length == 0)
        {
            return Array.Empty<Vector2>();
        }

        if (values.Length % 2 != 0)
        {
            throw new ArgumentException(value);
        }

        var points = new Vector2[values.Length / 2];
        for (var i = 0; i < values.Length; i += 2)
        {
            points[i / 2] = new Vector2(values[i], values[i + 1]);
        }

        return points;
    }

    public static MGPathLiteCommand[] ParsePathLiteCommands(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<MGPathLiteCommand>();
        }

        var normalized = value
            .Replace(",", " ")
            .Replace("M", " M ", StringComparison.OrdinalIgnoreCase)
            .Replace("L", " L ", StringComparison.OrdinalIgnoreCase)
            .Replace("Z", " Z ", StringComparison.OrdinalIgnoreCase);

        var tokens = normalized.Split(new[] { ' ', '\t', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        List<MGPathLiteCommand> commands = new();

        var index = 0;
        while (index < tokens.Length)
        {
            var token = tokens[index++];
            switch (token.ToUpperInvariant())
            {
                case "M":
                    commands.Add(MGPathLiteCommand.MoveTo(ParsePathPoint(tokens, ref index)));
                    break;
                case "L":
                    commands.Add(MGPathLiteCommand.LineTo(ParsePathPoint(tokens, ref index)));
                    break;
                case "Z":
                    commands.Add(MGPathLiteCommand.Close());
                    break;
                default:
                    throw new ArgumentException(value);
            }
        }

        return commands.ToArray();
    }

    private static Vector2 ParsePathPoint(string[] tokens, ref int index)
    {
        if (index + 1 >= tokens.Length)
        {
            throw new ArgumentException(string.Join(" ", tokens));
        }

        var x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
        var y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
        return new Vector2(x, y);
    }

    private static float[] ParseNumbers(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<float>();
        }

        var tokens = value.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var numbers = new float[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            numbers[i] = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        }

        return numbers;
    }
}

public class Ellipse : Element
{
    public override MGElementType ElementType => MGElementType.Ellipse;

    [Category("Border")]
    public XAMLColor? Stroke { get; set; }
    [Category("Border")]
    public float? StrokeThickness { get; set; }
    [Category("Appearance")]
    public FillBrush Fill { get; set; }
    [Category("Appearance")]
    public int? SegmentCount { get; set; }

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
        => new MGEllipse(Window, Width ?? 16, Height ?? 16,
            Stroke?.ToXNAColor() ?? Color.White,
            StrokeThickness ?? 1f,
            Color.Transparent);

    protected internal override void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent)
    {
        var Desktop = Element.GetDesktop();
        var ellipse = Element as MGEllipse;

        if (Stroke.HasValue)
        {
            ellipse.Stroke = Stroke.Value.ToXNAColor();
        }

        if (StrokeThickness.HasValue)
        {
            ellipse.StrokeThickness = StrokeThickness.Value;
        }

        if (Fill != null)
        {
            ellipse.FillBrush = Fill.ToFillBrush(Desktop, Element);
        }

        if (SegmentCount.HasValue)
        {
            ellipse.SegmentCount = SegmentCount.Value;
        }

        if (Width.HasValue)
        {
            ellipse.Width = Width.Value;
        }

        if (Height.HasValue)
        {
            ellipse.Height = Height.Value;
        }
    }

    protected internal override IEnumerable<Element> GetChildren() => Array.Empty<Element>();
}

public class Line : Element
{
    public override MGElementType ElementType => MGElementType.Line;

    [Category("Layout")]
    public string StartPoint { get; set; }
    [Category("Layout")]
    public string EndPoint { get; set; }
    [Category("Border")]
    public XAMLColor? Stroke { get; set; }
    [Category("Border")]
    public float? StrokeThickness { get; set; }

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
        => new MGLine(Window,
            ShapeXamlParser.ParsePoint(StartPoint ?? "0,0"),
            ShapeXamlParser.ParsePoint(EndPoint ?? "16,0"),
            Stroke?.ToXNAColor() ?? Color.White,
            StrokeThickness ?? 1f);

    protected internal override void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent)
    {
        var line = Element as MGLine;

        if (!string.IsNullOrWhiteSpace(StartPoint))
        {
            line.StartPoint = ShapeXamlParser.ParsePoint(StartPoint);
        }

        if (!string.IsNullOrWhiteSpace(EndPoint))
        {
            line.EndPoint = ShapeXamlParser.ParsePoint(EndPoint);
        }

        if (Stroke.HasValue)
        {
            line.Stroke = Stroke.Value.ToXNAColor();
        }

        if (StrokeThickness.HasValue)
        {
            line.StrokeThickness = StrokeThickness.Value;
        }
    }

    protected internal override IEnumerable<Element> GetChildren() => Array.Empty<Element>();
}

public class Polyline : Element
{
    public override MGElementType ElementType => MGElementType.Polyline;

    [Category("Layout")]
    public string Points { get; set; }
    [Category("Border")]
    public XAMLColor? Stroke { get; set; }
    [Category("Border")]
    public float? StrokeThickness { get; set; }

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
        => new MGPolyline(Window,
            ShapeXamlParser.ParsePoints(Points),
            Stroke?.ToXNAColor() ?? Color.White,
            StrokeThickness ?? 1f);

    protected internal override void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent)
    {
        var polyline = Element as MGPolyline;

        if (!string.IsNullOrWhiteSpace(Points))
        {
            polyline.Points = ShapeXamlParser.ParsePoints(Points);
        }

        if (Stroke.HasValue)
        {
            polyline.Stroke = Stroke.Value.ToXNAColor();
        }

        if (StrokeThickness.HasValue)
        {
            polyline.StrokeThickness = StrokeThickness.Value;
        }
    }

    protected internal override IEnumerable<Element> GetChildren() => Array.Empty<Element>();
}

public class Polygon : Element
{
    public override MGElementType ElementType => MGElementType.Polygon;

    [Category("Layout")]
    public string Points { get; set; }
    [Category("Border")]
    public XAMLColor? Stroke { get; set; }
    [Category("Border")]
    public float? StrokeThickness { get; set; }
    [Category("Appearance")]
    public FillBrush Fill { get; set; }

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
        => new MGPolygon(Window,
            ShapeXamlParser.ParsePoints(Points),
            Stroke?.ToXNAColor() ?? Color.White,
            StrokeThickness ?? 1f,
            Color.Transparent);

    protected internal override void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent)
    {
        var Desktop = Element.GetDesktop();
        var polygon = Element as MGPolygon;

        if (!string.IsNullOrWhiteSpace(Points))
        {
            polygon.Points = ShapeXamlParser.ParsePoints(Points);
        }

        if (Stroke.HasValue)
        {
            polygon.Stroke = Stroke.Value.ToXNAColor();
        }

        if (StrokeThickness.HasValue)
        {
            polygon.StrokeThickness = StrokeThickness.Value;
        }

        if (Fill != null)
        {
            polygon.FillBrush = Fill.ToFillBrush(Desktop, Element);
        }
    }

    protected internal override IEnumerable<Element> GetChildren() => Array.Empty<Element>();
}

public class PathLite : Element
{
    public override MGElementType ElementType => MGElementType.PathLite;

    [Category("Layout")]
    public string Data { get; set; }
    [Category("Border")]
    public XAMLColor? Stroke { get; set; }
    [Category("Border")]
    public float? StrokeThickness { get; set; }
    [Category("Appearance")]
    public FillBrush Fill { get; set; }

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
        => new MGPathLite(Window,
            ShapeXamlParser.ParsePathLiteCommands(Data),
            Stroke?.ToXNAColor() ?? Color.White,
            StrokeThickness ?? 1f,
            Color.Transparent);

    protected internal override void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent)
    {
        var Desktop = Element.GetDesktop();
        var pathLite = Element as MGPathLite;

        if (!string.IsNullOrWhiteSpace(Data))
        {
            pathLite.Commands = ShapeXamlParser.ParsePathLiteCommands(Data);
        }

        if (Stroke.HasValue)
        {
            pathLite.Stroke = Stroke.Value.ToXNAColor();
        }

        if (StrokeThickness.HasValue)
        {
            pathLite.StrokeThickness = StrokeThickness.Value;
        }

        if (Fill != null)
        {
            pathLite.FillBrush = Fill.ToFillBrush(Desktop, Element);
        }
    }

    protected internal override IEnumerable<Element> GetChildren() => Array.Empty<Element>();
}