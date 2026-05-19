using Microsoft.Xna.Framework;
using System;

namespace MGUI.Core.UI
{
    public readonly struct ColorValue : IEquatable<ColorValue>
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public ColorSpaceMode ColorSpace { get; }
        public bool IsHdr { get; }

        public ColorValue(float r, float g, float b)
            : this(r, g, b, 1f, ColorSpaceMode.Srgb, false)
        {
        }

        public ColorValue(float r, float g, float b, float a)
            : this(r, g, b, a, ColorSpaceMode.Srgb, false)
        {
        }

        public ColorValue(float r, float g, float b, float a, ColorSpaceMode colorSpace)
            : this(r, g, b, a, colorSpace, false)
        {
        }

        public ColorValue(float r, float g, float b, float a, ColorSpaceMode colorSpace, bool isHdr)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            ColorSpace = colorSpace;
            IsHdr = isHdr || HasHdrChannel(r, g, b);
        }

        public ColorValue WithAlpha(float alpha)
            => new(R, G, B, alpha, ColorSpace, IsHdr);

        public ColorValue WithColorSpace(ColorSpaceMode colorSpace)
            => new(R, G, B, A, colorSpace, IsHdr);

        public ColorValue ClampLdr()
            => new(
                Clamp01(R),
                Clamp01(G),
                Clamp01(B),
                Clamp01(A),
                ColorSpace,
                false);

        public Color ToXnaColor()
        {
            ColorValue clamped = ClampLdr();
            return new Color(
                ToByte(clamped.R),
                ToByte(clamped.G),
                ToByte(clamped.B),
                ToByte(clamped.A));
        }

        public Vector4 ToVector4()
            => new(R, G, B, A);

        public System.Numerics.Vector4 ToSystemVector4()
            => new(R, G, B, A);

        public string ToHex(ColorValueFormat format)
            => ColorFormatter.ToHex(this, format);

        public static ColorValue FromXnaColor(Color color)
            => FromXnaColor(color, ColorSpaceMode.Srgb);

        public static ColorValue FromXnaColor(Color color, ColorSpaceMode colorSpace)
            => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f, colorSpace, false);

        public static ColorValue FromVector3(Vector3 value)
            => FromVector3(value, ColorSpaceMode.Srgb);

        public static ColorValue FromVector3(Vector3 value, ColorSpaceMode colorSpace)
            => new(value.X, value.Y, value.Z, 1f, colorSpace, false);

        public static ColorValue FromVector4(Vector4 value)
            => FromVector4(value, ColorSpaceMode.Srgb);

        public static ColorValue FromVector4(Vector4 value, ColorSpaceMode colorSpace)
            => new(value.X, value.Y, value.Z, value.W, colorSpace, false);

        public static ColorValue FromVector3(System.Numerics.Vector3 value)
            => FromVector3(value, ColorSpaceMode.Srgb);

        public static ColorValue FromVector3(System.Numerics.Vector3 value, ColorSpaceMode colorSpace)
            => new(value.X, value.Y, value.Z, 1f, colorSpace, false);

        public static ColorValue FromVector4(System.Numerics.Vector4 value)
            => FromVector4(value, ColorSpaceMode.Srgb);

        public static ColorValue FromVector4(System.Numerics.Vector4 value, ColorSpaceMode colorSpace)
            => new(value.X, value.Y, value.Z, value.W, colorSpace, false);

        public static bool TryParse(string text, out ColorValue value)
            => ColorParser.TryParse(text, out value);

        public bool Equals(ColorValue other)
            => R.Equals(other.R)
            && G.Equals(other.G)
            && B.Equals(other.B)
            && A.Equals(other.A)
            && ColorSpace == other.ColorSpace
            && IsHdr == other.IsHdr;

        public override bool Equals(object obj)
            => obj is ColorValue other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(R, G, B, A, ColorSpace, IsHdr);

        public override string ToString()
            => $"{nameof(ColorValue)}({R}, {G}, {B}, {A}, {ColorSpace}, HDR={IsHdr})";

        public static bool operator ==(ColorValue left, ColorValue right)
            => left.Equals(right);

        public static bool operator !=(ColorValue left, ColorValue right)
            => !left.Equals(right);

        private static bool HasHdrChannel(float r, float g, float b)
            => r > 1f || g > 1f || b > 1f;

        private static float Clamp01(float value)
            => Math.Clamp(value, 0f, 1f);

        private static byte ToByte(float value)
            => (byte)Math.Clamp((int)MathF.Round(value * byte.MaxValue), byte.MinValue, byte.MaxValue);
    }
}