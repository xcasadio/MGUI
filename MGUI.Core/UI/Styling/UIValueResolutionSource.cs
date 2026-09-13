namespace MGUI.Core.UI.Styling;

public readonly record struct UIValueResolutionSource(
    UIValueSourceKind Kind,
    UIValuePrecedence Precedence,
    UIInvalidationKind Invalidation,
    string Name = null)
{
    public bool IsLocal => Kind == UIValueSourceKind.LocalValue || Kind == UIValueSourceKind.LocalBinding;

    public bool IsStyle => Kind == UIValueSourceKind.ImplicitStyle || Kind == UIValueSourceKind.ExplicitStyle;

    public bool IsRuntimeOverride => Kind == UIValueSourceKind.Animation || Kind == UIValueSourceKind.VisualState;

    public static UIValueResolutionSource Default(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.DefaultValue, UIValuePrecedence.DefaultValue, invalidation, name);

    public static UIValueResolutionSource Inherited(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.Inherited, UIValuePrecedence.Inherited, invalidation, name);

    public static UIValueResolutionSource Theme(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.Theme, UIValuePrecedence.Theme, invalidation, name);

    public static UIValueResolutionSource DynamicResource(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.DynamicResource, UIValuePrecedence.DynamicResource, invalidation, name);

    public static UIValueResolutionSource ImplicitStyle(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.ImplicitStyle, UIValuePrecedence.ImplicitStyle, invalidation, name);

    public static UIValueResolutionSource ExplicitStyle(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.ExplicitStyle, UIValuePrecedence.ExplicitStyle, invalidation, name);

    public static UIValueResolutionSource Template(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.Template, UIValuePrecedence.Template, invalidation, name);

    public static UIValueResolutionSource VisualState(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.VisualState, UIValuePrecedence.VisualState, invalidation, name);

    public static UIValueResolutionSource LocalBinding(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.LocalBinding, UIValuePrecedence.LocalBinding, invalidation, name);

    public static UIValueResolutionSource LocalValue(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.LocalValue, UIValuePrecedence.LocalValue, invalidation, name);

    public static UIValueResolutionSource Animation(UIInvalidationKind invalidation, string name = null)
        => new(UIValueSourceKind.Animation, UIValuePrecedence.Animation, invalidation, name);
}