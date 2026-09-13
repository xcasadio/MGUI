namespace MGUI.Core.UI;

public readonly record struct MGCornerRadius
{
    public static readonly MGCornerRadius Zero = new(0);

    public int TopLeft { get; init; }
    public int TopRight { get; init; }
    public int BottomRight { get; init; }
    public int BottomLeft { get; init; }

    public bool IsZero =>
        TopLeft == 0 &&
        TopRight == 0 &&
        BottomRight == 0 &&
        BottomLeft == 0;

    public MGCornerRadius(int uniformRadius)
        : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius) { }

    public MGCornerRadius(int topLeft, int topRight, int bottomRight, int bottomLeft)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomRight = bottomRight;
        BottomLeft = bottomLeft;
    }

    public override string ToString() =>
        $"{nameof(MGCornerRadius)}: ({TopLeft},{TopRight},{BottomRight},{BottomLeft})";

    public static explicit operator MGCornerRadius(int uniformRadius) => new(uniformRadius);
}