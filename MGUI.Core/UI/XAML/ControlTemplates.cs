using MGUI.Core.UI.Data_Binding;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML;

public class ControlTemplates : ControlTemplatesDocument { }

[ContentProperty(nameof(Templates))]
public class ControlTemplatesDocument : XAMLBindableBase
{
    public List<ControlTemplateDefinition> Templates { get; set; } = new();
}

public class ControlTemplate : ControlTemplateDefinition { }

[ContentProperty(nameof(Root))]
public class ControlTemplateDefinition : XAMLBindableBase
{
    public string Name { get; set; }
    public string TargetType { get; set; }
    public string BasedOn { get; set; }

    /// <summary>Root visual produced by the template.
    /// Initial support intentionally focuses on a single root plus explicitly mapped parts.</summary>
    public Element Root { get; set; }

    /// <summary>Additional detached visuals materialized alongside <see cref="Root"/>.
    /// These elements are available for template-part mapping even when they are not descendants of the primary root.</summary>
    public List<Element> DetachedRoots { get; set; } = new();

    public List<TemplatePartDefinition> Parts { get; set; } = new();

    public string Notes { get; set; }
}

public class TemplatePart : TemplatePartDefinition { }

public class TemplatePartDefinition : XAMLBindableBase
{
    public string Name { get; set; }
    public string ElementName { get; set; }
    public bool IsRequired { get; set; } = true;
}