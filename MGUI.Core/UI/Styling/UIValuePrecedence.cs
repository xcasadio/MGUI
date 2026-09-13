namespace MGUI.Core.UI.Styling;

public enum UIValuePrecedence
{
    DefaultValue = 0,
    Inherited = 10,
    Theme = 20,
    DynamicResource = 30,
    ImplicitStyle = 40,
    ExplicitStyle = 50,
    Template = 60,
    VisualState = 70,
    LocalBinding = 80,
    LocalValue = 90,

    /// <summary>A named visual state declared with <see cref="MGUI.Core.UI.Animation.States.UIVisualState.OverridesLocalValue"/> (ADR-0008,
    /// decision 4): still <see cref="MGUI.Core.UI.Styling.UIValueSourceKind.VisualState"/> in kind, but written above <see cref="LocalValue"/>
    /// and <see cref="LocalBinding"/> so the state wins over them; a plain <see cref="Animation"/> contribution still outranks it.</summary>
    VisualStateOverride = 95,

    Animation = 100,
}