using Microsoft.Xna.Framework;
using System;

namespace MGUI.Core.UI
{
    internal static class PropertyGridColorAdapter
    {
        internal static bool IsSupportedColorType(Type propertyType)
        {
            Type actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return actualType == typeof(Color)
                || actualType == typeof(Vector3)
                || actualType == typeof(Vector4)
                || actualType == typeof(System.Numerics.Vector3)
                || actualType == typeof(System.Numerics.Vector4);
        }

        internal static bool TryToColorValue(object value, out ColorValue colorValue)
        {
            switch (value)
            {
                case Color color:
                    colorValue = ColorValue.FromXnaColor(color);
                    return true;
                case Vector3 vector3:
                    colorValue = ColorValue.FromVector3(vector3);
                    return true;
                case Vector4 vector4:
                    colorValue = ColorValue.FromVector4(vector4);
                    return true;
                case System.Numerics.Vector3 vector3:
                    colorValue = ColorValue.FromVector3(vector3);
                    return true;
                case System.Numerics.Vector4 vector4:
                    colorValue = ColorValue.FromVector4(vector4);
                    return true;
            }

            colorValue = default;
            return false;
        }

        internal static object ToPropertyValue(ColorValue value, Type propertyType)
        {
            Type actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (actualType == typeof(Color))
            {
                return value.ToXnaColor();
            }

            if (actualType == typeof(Vector3))
            {
                return new Vector3(value.R, value.G, value.B);
            }

            if (actualType == typeof(Vector4))
            {
                return value.ToVector4();
            }

            if (actualType == typeof(System.Numerics.Vector3))
            {
                return new System.Numerics.Vector3(value.R, value.G, value.B);
            }

            if (actualType == typeof(System.Numerics.Vector4))
            {
                return value.ToSystemVector4();
            }

            throw new NotSupportedException($"Unsupported PropertyGrid color type '{propertyType}'.");
        }
    }
}