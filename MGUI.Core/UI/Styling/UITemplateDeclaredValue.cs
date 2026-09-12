namespace MGUI.Core.UI.Styling
{
    /// <summary>A pilot value that the XAML definition of a control template declares on one of the elements of its structure (backlog task 15):
    /// the <see cref="UIResolvedContribution"/> of kind <see cref="UIValueSourceKind.Template"/> that the attribute recorded on <paramref name="Target"/>
    /// when the structure was instantiated, named <c>"&lt;template&gt;:&lt;element name&gt;.&lt;property&gt;"</c>. <c>MGElement.ApplyControlTemplate</c>
    /// applies it again after the template's defaults, so that the declaration of the template wins over the defaults of its base applicator.</summary>
    internal readonly record struct UITemplateDeclaredValue(MGElement Target, UIPilotProperty Property, UIValueSlot Slot, UIResolvedContribution Contribution);
}
