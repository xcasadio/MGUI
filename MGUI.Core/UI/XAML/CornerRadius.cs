using System.ComponentModel;
using System.Globalization;

namespace MGUI.Core.UI.XAML;

[TypeConverter(typeof(CornerRadiusStringConverter))]
public struct CornerRadius
{
    public int TopLeft { get; set; }
    public int TopRight { get; set; }
    public int BottomRight { get; set; }
    public int BottomLeft { get; set; }

    public CornerRadius() : this(0) { }
    public CornerRadius(int uniformRadius) : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius) { }

    public CornerRadius(int topLeft, int topRight, int bottomRight, int bottomLeft)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomRight = bottomRight;
        BottomLeft = bottomLeft;
    }

    public override string ToString() => $"{nameof(CornerRadius)}: ({TopLeft},{TopRight},{BottomRight},{BottomLeft})";

    public MGCornerRadius ToCornerRadius() => new(TopLeft, TopRight, BottomRight, BottomLeft);
}

public class CornerRadiusStringConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        if (sourceType == typeof(string))
        {
            return true;
        }

        return base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is string stringValue)
        {
            int[] values = stringValue.Split(',').Select(x => int.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return values.Length switch
            {
                1 => new CornerRadius(values[0]),
                4 => new CornerRadius(values[0], values[1], values[2], values[3]),
                _ => throw new ArgumentException(stringValue)
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}