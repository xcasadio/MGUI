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
    Animation = 100,
}