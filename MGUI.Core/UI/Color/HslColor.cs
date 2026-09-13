namespace MGUI.Core.UI;

public readonly struct HslColor : IEquatable<HslColor>
{
    public float H { get; }
    public float S { get; }
    public float L { get; }
    public float A { get; }

    public HslColor(float h, float s, float l)
        : this(h, s, l, 1f)
    {
    }

    public HslColor(float h, float s, float l, float a)
    {
        H = h;
        S = s;
        L = l;
        A = a;
    }

    public bool Equals(HslColor other)
        => H.Equals(other.H) && S.Equals(other.S) && L.Equals(other.L) && A.Equals(other.A);

    public override bool Equals(object obj)
        => obj is HslColor other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(H, S, L, A);

    public static bool operator ==(HslColor left, HslColor right)
        => left.Equals(right);

    public static bool operator !=(HslColor left, HslColor right)
        => !left.Equals(right);
}