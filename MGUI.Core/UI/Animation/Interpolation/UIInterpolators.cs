using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Core.UI.Animation.Interpolation
{
    /// <summary>
    /// Registry of the <see cref="IUIInterpolator{T}"/> used by default by the animation engine, keyed by value type.<para/>
    /// Built-in entries (registered once when the type is first used): <see cref="float"/>, <see cref="double"/>, <see cref="int"/>,
    /// <see cref="Nullable{T}"/> <see cref="int"/>, <see cref="Vector2"/>, <see cref="Vector3"/>, <see cref="Vector4"/>, <see cref="Color"/>,
    /// <see cref="Rectangle"/> and <see cref="Thickness"/>. An application registers its own value type with <see cref="Register{T}"/>
    /// (the last registration for a type wins, which also lets an application replace a built-in).<para/>
    /// Thread-safe. Lookups do not allocate.
    /// </summary>
    public static class UIInterpolators
    {
        private static readonly ConcurrentDictionary<Type, object> Registry = new();

        static UIInterpolators()
        {
            Register(UIFloatInterpolator.Instance);
            Register(UIDoubleInterpolator.Instance);
            Register(UIIntInterpolator.Instance);
            Register(UINullableIntInterpolator.Instance);
            Register(UIVector2Interpolator.Instance);
            Register(UIVector3Interpolator.Instance);
            Register(UIVector4Interpolator.Instance);
            Register(UIColorInterpolator.Instance);
            Register(UIRectangleInterpolator.Instance);
            Register(UIThicknessInterpolator.Instance);
        }

        /// <summary>Registers (or replaces) the interpolator used by default for <typeparamref name="T"/>.</summary>
        public static void Register<T>(IUIInterpolator<T> interpolator)
        {
            if (interpolator == null)
            {
                throw new ArgumentNullException(nameof(interpolator));
            }

            Registry[typeof(T)] = interpolator;
        }

        /// <summary>True if an interpolator is registered for <typeparamref name="T"/>.</summary>
        public static bool IsRegistered<T>() => Registry.ContainsKey(typeof(T));

        /// <summary>Retrieves the interpolator registered for <typeparamref name="T"/>, or false when none is.</summary>
        public static bool TryGet<T>(out IUIInterpolator<T> interpolator)
        {
            if (Registry.TryGetValue(typeof(T), out object registered))
            {
                interpolator = (IUIInterpolator<T>)registered;
                return true;
            }

            interpolator = null;
            return false;
        }

        /// <summary>Retrieves the interpolator registered for <typeparamref name="T"/>.</summary>
        /// <exception cref="InvalidOperationException">No interpolator is registered for <typeparamref name="T"/>.</exception>
        public static IUIInterpolator<T> Get<T>()
        {
            if (TryGet(out IUIInterpolator<T> interpolator))
            {
                return interpolator;
            }

            throw new InvalidOperationException(
                $"No {nameof(IUIInterpolator<T>)} is registered for '{typeof(T).FullName}'. " +
                $"Register one with {nameof(UIInterpolators)}.{nameof(Register)}<{typeof(T).Name}>(...) or set the animation's Interpolator explicitly.");
        }
    }
}
