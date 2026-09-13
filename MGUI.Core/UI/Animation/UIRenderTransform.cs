using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Render-only transform of an <see cref="MGElement"/> (ADR-0006, Docs/Tasks/animation-tasks.md S2): applied when the element is drawn
/// and inverted for hit-testing, never consulted by the layout, so a transformed element keeps its measured bounds and may overlap its neighbours.<para/>
/// Semantics follow NoesisGUI and WPF: <see cref="Origin"/> is a point relative to the element bounds, in [0, 1]², whose default (0, 0) is the
/// top-left corner (set (0.5, 0.5) for the centre, as <c>RenderTransformOrigin="0.5,0.5"</c>); <see cref="Rotation"/> is an angle in degrees,
/// positive clockwise on screen (as <c>RotateTransform.Angle</c>); <see cref="Translation"/> is in unscaled layout pixels.<para/>
/// The instance is allocated by <see cref="MGElement.RenderTransform"/> on first access; every setter raises <see cref="PropertyChanged"/>.
/// An identity transform (<see cref="IsIdentity"/>) costs nothing at draw time: no matrix is pushed and the current batch is not broken.
/// </summary>
public sealed class UIRenderTransform : INotifyPropertyChanged
{
    /// <summary>Tolerance under which a component counts as its identity value.</summary>
    public const float IdentityEpsilon = 1e-6f;

    private static readonly PropertyChangedEventArgs TranslationChangedArgs = new(nameof(Translation));
    private static readonly PropertyChangedEventArgs ScaleChangedArgs = new(nameof(Scale));
    private static readonly PropertyChangedEventArgs RotationChangedArgs = new(nameof(Rotation));
    private static readonly PropertyChangedEventArgs OriginChangedArgs = new(nameof(Origin));

    /// <summary>Raised by every setter that changes a component, with cached event args so an animated transform allocates nothing per tick.</summary>
    public event PropertyChangedEventHandler PropertyChanged;

    private void NPC(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);

    private Vector2 _Translation;
    /// <summary>Offset in unscaled layout pixels, applied after the scale and the rotation. Default: <see cref="Vector2.Zero"/>.</summary>
    public Vector2 Translation
    {
        get => _Translation;
        set
        {
            if (_Translation != value)
            {
                _Translation = value;
                NPC(TranslationChangedArgs);
            }
        }
    }

    private Vector2 _Scale = Vector2.One;
    /// <summary>Scale factors around <see cref="Origin"/>. Default: <see cref="Vector2.One"/>.</summary>
    public Vector2 Scale
    {
        get => _Scale;
        set
        {
            if (_Scale != value)
            {
                _Scale = value;
                NPC(ScaleChangedArgs);
            }
        }
    }

    private float _Rotation;
    /// <summary>Angle in degrees around <see cref="Origin"/>, positive clockwise on screen. Default: 0.</summary>
    public float Rotation
    {
        get => _Rotation;
        set
        {
            if (_Rotation != value)
            {
                _Rotation = value;
                NPC(RotationChangedArgs);
            }
        }
    }

    private Vector2 _Origin;
    /// <summary>Pivot of the scale and the rotation, relative to the element bounds: (0, 0) is the top-left corner (the default, as in NoesisGUI
    /// and WPF), (0.5, 0.5) the centre, (1, 1) the bottom-right corner. Values outside [0, 1] are allowed.</summary>
    public Vector2 Origin
    {
        get => _Origin;
        set
        {
            if (_Origin != value)
            {
                _Origin = value;
                NPC(OriginChangedArgs);
            }
        }
    }

    /// <summary>True when the transform has no visible effect (translation zero, scale one, rotation zero, within <see cref="IdentityEpsilon"/>);
    /// <see cref="Origin"/> is irrelevant then.</summary>
    public bool IsIdentity =>
        Math.Abs(_Translation.X) <= IdentityEpsilon && Math.Abs(_Translation.Y) <= IdentityEpsilon &&
        Math.Abs(_Scale.X - 1f) <= IdentityEpsilon && Math.Abs(_Scale.Y - 1f) <= IdentityEpsilon &&
        Math.Abs(_Rotation) <= IdentityEpsilon;

    /// <summary>Resets every component to its default value.</summary>
    public void Reset()
    {
        Translation = Vector2.Zero;
        Scale = Vector2.One;
        Rotation = 0f;
        Origin = Vector2.Zero;
    }

    /// <summary>The matrix of this transform for an element occupying <paramref name="bounds"/> (unscaled screen space).
    /// See <see cref="CreateMatrix"/>.</summary>
    public Matrix ToMatrix(Rectangle bounds) => CreateMatrix(bounds, _Translation, _Scale, _Rotation, _Origin);

    /// <summary>Builds <c>T(-pivot) * S(scale) * R(rotation) * T(pivot + translation)</c> (XNA row-vector convention: the leftmost factor
    /// applies first) where <c>pivot = bounds.Location + relativeOrigin * bounds.Size</c>. Composed by <see cref="MGElement.Draw"/>
    /// before the current draw transform and inverted by the hit-test, so the same matrix serves both.</summary>
    public static Matrix CreateMatrix(Rectangle bounds, Vector2 translation, Vector2 scale, float rotationDegrees, Vector2 relativeOrigin)
    {
        var pivotX = bounds.X + bounds.Width * relativeOrigin.X;
        var pivotY = bounds.Y + bounds.Height * relativeOrigin.Y;

        var matrix = Matrix.CreateTranslation(-pivotX, -pivotY, 0f);
        if (scale != Vector2.One)
        {
            matrix *= Matrix.CreateScale(scale.X, scale.Y, 1f);
        }

        if (rotationDegrees != 0f)
        {
            matrix *= Matrix.CreateRotationZ(MathHelper.ToRadians(rotationDegrees));
        }

        matrix *= Matrix.CreateTranslation(pivotX + translation.X, pivotY + translation.Y, 0f);
        return matrix;
    }

    /// <summary>The uniform scale around the centre of <paramref name="bounds"/> used by the state-driven <see cref="MGElement.RenderScale"/>
    /// (Pressed / Hovered), which keeps its own pivot whatever <see cref="Origin"/> says (ADR-0006).</summary>
    public static Matrix CreateCenteredScale(Rectangle bounds, float scale)
    {
        var center = bounds.Center;
        return Matrix.CreateTranslation(-center.X, -center.Y, 0f) *
               Matrix.CreateScale(scale) *
               Matrix.CreateTranslation(center.X, center.Y, 0f);
    }

    public override string ToString()
        => $"{nameof(UIRenderTransform)}: Translation=({_Translation.X},{_Translation.Y}) Scale=({_Scale.X},{_Scale.Y}) Rotation={_Rotation}deg Origin=({_Origin.X},{_Origin.Y})";
}