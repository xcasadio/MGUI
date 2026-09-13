using MGUI.Shared.Helpers;
using System.ComponentModel;
using System.Diagnostics;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML;

[ContentProperty(nameof(Content))]
public class ContentTemplate
{
    /// <summary>The name of the <see cref="MGElementTemplate"/> to use when generating the Content. <see cref="MGElementTemplate"/>s are retrieved via <see cref="MGResources.ElementTemplates"/><para/>
    /// You should only specify either <see cref="ContentTemplateName"/> or <see cref="Content"/>, not both.<para/>
    /// See also: <see cref="MGDesktop.Resources"/>, <see cref="MGResources.ElementTemplates"/></summary>
    [Category("Data")]
    public string ContentTemplateName { get; set; }

    [Category("Data")]
    public Element Content { get; set; }

    /// <param name="ApplyBaseSettings">If not null, this action will be invoked before <see cref="Element.ApplySettings(MGElement, MGElement, bool)"/>
    /// executes on the created <see cref="MGElement"/>.</param>
    public MGElement GetContent(MGWindow Window, MGElement Parent, object DataContext, Action<MGElement> ApplyBaseSettings = null)
    {
        if (Content != null)
        {
            var ContentCopy = Content.Copy();
            var Item = ContentCopy.ToElement(Window, Parent, ApplyBaseSettings);
            Element.ProcessBindings(Item, true, DataContext);
            return Item;
        }
        else if (ContentTemplateName != null)
        {
            var Resources = Window.GetResources();
            if (!Resources.TryGetElementTemplate(ContentTemplateName, out var Template))
            {
                Debug.WriteLine($"Warning - No {nameof(MGElementTemplate)} was found with the name '{ContentTemplateName}' in {nameof(MGResources)}.{nameof(MGResources.ElementTemplates)}.");
                return null;
            }

            var Item = Template.GetInstance(Window);
            //  Note: ideally ApplyBaseSettings should be invoked *before* the template applies its own customization.
            //  This requires splitting MGElementTemplate.Template into Create + Style phases.
            //  As a best-effort fix, we apply it here (after template creation) so it at least runs.
            ApplyBaseSettings?.Invoke(Item);
            Debug.WriteLineIf(ApplyBaseSettings != null, $"[{nameof(ContentTemplate)}] Applied {nameof(ApplyBaseSettings)} post-creation for template '{ContentTemplateName}'. " +
                                                         $"Note: ideally {nameof(ApplyBaseSettings)} should run before template customization for correct style precedence.");
            Element.ProcessBindings(Item, true, DataContext);
            return Item;
        }
        else
        {
            return null;
        }
    }
}