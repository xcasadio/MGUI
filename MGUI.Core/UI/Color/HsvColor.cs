namespace MGUI.Core.UI;

public readonly struct HsvColor : IEquatable<HsvColor>
{
    public float H { get; }
    public float S { get; }
    public float V { get; }
    public float A { get; }

    public HsvColor(float h, float s, float v)
        : this(h, s, v, 1f)
    {
    }

    public HsvColor(float h, float s, float v, float a)
    {
        H = h;
        S = s;
        V = v;
        A = a;
    }

    public bool Equals(HsvColor other)
        => H.Equals(other.H) && S.Equals(other.S) && V.Equals(other.V) && A.Equals(other.A);

    public override bool Equals(object obj)
        => obj is HsvColor other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(H, S, V, A);

    public static bool operator ==(HsvColor left, HsvColor right)
        => left.Equals(right);

    public static bool operator !=(HsvColor left, HsvColor right)
        => !left.Equals(right);
}