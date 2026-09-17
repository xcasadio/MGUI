using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation;

/// <summary>The animated values of a window's internal enter/exit draw transform (ADR-0011 decision 6, Y7): a windows does not honour
/// <see cref="UIRenderTransform"/> (ADR-0006), so a <see cref="UIEnterExitEffect.Scale"/>/<see cref="UIEnterExitEffect.FadeScale"/>/
/// <see cref="UIEnterExitEffect.SlideLeft"/> (and its siblings) effect on a window animates this transform instead, allocated on demand by
/// <see cref="UIElementAnimationSlot"/> so a window that never opts in never allocates one. Composed in <see cref="MGWindow.Draw"/> around
/// the window's own draw and everything it draws after it (nested windows, modal window, dim overlay), pivoting on the window's own centre
/// in unscaled screen space -- never inverted for hit testing (documented limitation: during an entry the window is hit-testable at its
/// final place).</summary>
internal sealed class UIWindowTransform
{
    /// <summary>The current enter/exit scale, around the window's centre. <see cref="Vector2.One"/> (the default) has no visible effect.
    /// Written only through <see cref="MGElement.SetEnterExitWindowScale"/>.</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>The current enter/exit translation, in unscaled screen space. Written only through
    /// <see cref="MGElement.SetEnterExitWindowTranslation"/>.</summary>
    public Vector2 Translation { get; set; }

    /// <summary>True while neither <see cref="Scale"/> nor <see cref="Translation"/> has a visible effect (within
    /// <see cref="UIRenderTransform.IdentityEpsilon"/>): <see cref="MGWindow.Draw"/> pushes no transform at all in that case.</summary>
    public bool IsIdentity =>
        Math.Abs(Scale.X - 1f) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Scale.Y - 1f) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Translation.X) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Translation.Y) <= UIRenderTransform.IdentityEpsilon;

    /// <summary>The matrix this transform represents, pivoting <see cref="Scale"/> on <paramref name="pivotInUnscaledScreenSpace"/> and
    /// applying <see cref="Translation"/> as a plain screen-space offset.</summary>
    public Matrix ToMatrix(Vector2 pivotInUnscaledScreenSpace) =>
        Matrix.CreateTranslation(new Vector3(-pivotInUnscaledScreenSpace, 0)) *
        Matrix.CreateScale(new Vector3(Scale, 1f)) *
        Matrix.CreateTranslation(new Vector3(pivotInUnscaledScreenSpace + Translation, 0));
}

/// <summary>The explicit, unregistered target of a window's enter/exit scale (ADR-0011 decision 6, Y7): no entry in
/// <see cref="UIAnimationTargets"/>, so it is invisible to the registry, to XAML, to <see cref="UITransition{T}"/> and to the serializer --
/// same shape as <see cref="UILayoutTransitionOffsetTarget"/>. One shared, stateless instance serves every window.<para/>
/// <see cref="RestoreBaseValue"/> ignores <c>baseValue</c> and always writes <see cref="Vector2.One"/>, the transform's own identity scale;
/// <see cref="UIEnterExitEffectFactory"/> always tracks the true captured base itself and restores it explicitly (<see cref="MGElement"/>'s
/// "force restore base" step), exactly like the non-window <see cref="UIBuiltInAnimationTargets.Paths.RenderTransformScale"/> path.</summary>
internal sealed class UIWindowEnterExitScaleTarget : IUIAnimationTarget<Vector2>
{
    public static readonly UIWindowEnterExitScaleTarget Instance = new();

    private UIWindowEnterExitScaleTarget() { }

    public string Path => "EnterExit.WindowScale";

    public bool IsStoreBacked => false;

    public Vector2 GetValue(MGElement element) => element.EnterExitWindowScale;

    public void SetValue(MGElement element, Vector2 value, string animationName) => element.SetEnterExitWindowScale(value);

    public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.SetEnterExitWindowScale(Vector2.One);
}

/// <summary>The explicit, unregistered target of a window's enter/exit translation (ADR-0011 decision 6, Y7), the window counterpart of
/// <see cref="UIWindowEnterExitScaleTarget"/> for the <see cref="UIEnterExitEffect.SlideLeft"/> family of effects.<para/>
/// <see cref="RestoreBaseValue"/> ignores <c>baseValue</c> and always writes <see cref="Vector2.Zero"/>, the transform's own identity
/// translation.</summary>
internal sealed class UIWindowEnterExitTranslationTarget : IUIAnimationTarget<Vector2>
{
    public static readonly UIWindowEnterExitTranslationTarget Instance = new();

    private UIWindowEnterExitTranslationTarget() { }

    public string Path => "EnterExit.WindowTranslation";

    public bool IsStoreBacked => false;

    public Vector2 GetValue(MGElement element) => element.EnterExitWindowTranslation;

    public void SetValue(MGElement element, Vector2 value, string animationName) => element.SetEnterExitWindowTranslation(value);

    public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.SetEnterExitWindowTranslation(Vector2.Zero);
}
