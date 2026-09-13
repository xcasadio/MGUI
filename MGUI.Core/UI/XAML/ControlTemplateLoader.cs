using System.Xml.Linq;

namespace MGUI.Core.UI.XAML;

public static class ControlTemplateLoader
{
    public static IReadOnlyList<ControlTemplateDefinition> ParseDefinitions(XamlDocumentSource Source,
        bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        => ParseDefinitions(Source, XamlLoaderMode.Compatibility, SanitizeXAMLString, ReplaceLinebreakLiterals);

    public static IReadOnlyList<ControlTemplateDefinition> ParseDefinitions(XamlDocumentSource Source, XamlLoaderMode Mode,
        bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
    {
        return XamlLoaderDiagnostics.Execute<IReadOnlyList<ControlTemplateDefinition>>(Source, Mode, "control template document", () =>
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            string markup = Source.LoadContent();
            XDocument documentRoot = XDocument.Parse(markup, LoadOptions.SetLineInfo);
            string rootName = documentRoot.Root?.Name.LocalName;
            IReadOnlyList<ControlTemplateDefinition> definitions;
            if (rootName == nameof(ControlTemplatesDocument) || rootName == nameof(ControlTemplates))
            {
                ControlTemplatesDocument document = XAMLParser.ParseObjectDefinition<ControlTemplatesDocument>(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
                definitions = document?.Templates ?? new List<ControlTemplateDefinition>();
            }
            else if (rootName == nameof(ControlTemplateDefinition) || rootName == nameof(ControlTemplate))
            {
                ControlTemplateDefinition definition = XAMLParser.ParseObjectDefinition<ControlTemplateDefinition>(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
                definitions = definition == null ? Array.Empty<ControlTemplateDefinition>() : new[] { definition };
            }
            else if (Mode == XamlLoaderMode.Strict)
            {
                throw XamlLoaderDiagnostics.CreateUnsupportedDocumentRootException(
                    Source,
                    "control template document",
                    rootName,
                    $"'{nameof(ControlTemplate)}' or '{nameof(ControlTemplatesDocument)}'",
                    documentRoot.Root is null ? null : ((System.Xml.IXmlLineInfo)documentRoot.Root).LineNumber,
                    documentRoot.Root is null ? null : ((System.Xml.IXmlLineInfo)documentRoot.Root).LinePosition);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported control template document root '{rootName}'. Expected '{nameof(ControlTemplate)}' or '{nameof(ControlTemplatesDocument)}'.");
            }

            XamlLoaderDiagnostics.ValidateRequiredTemplateParts(definitions, Source, Mode);
            return definitions;
        });
    }

    public static IReadOnlyDictionary<string, Styling.MGControlTemplate> BuildTemplates(IEnumerable<ControlTemplateDefinition> Definitions)
        => BuildTemplates(Definitions, null);

    public static IReadOnlyDictionary<string, Styling.MGControlTemplate> BuildTemplates(IEnumerable<ControlTemplateDefinition> Definitions,
        Func<string, Styling.MGControlTemplate> ResolveExternalTemplate)
    {
        if (Definitions == null)
        {
            throw new ArgumentNullException(nameof(Definitions));
        }

        Dictionary<string, ControlTemplateDefinition> definitionsByName = new(StringComparer.Ordinal);
        foreach (ControlTemplateDefinition definition in Definitions.Where(x => x != null))
        {
            string name = definition.Name ?? throw new InvalidOperationException($"{nameof(ControlTemplateDefinition)} requires a non-null {nameof(ControlTemplateDefinition.Name)}.");
            definitionsByName[name] = definition;
        }

        Dictionary<string, Styling.MGControlTemplate> cache = new(StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);

        Styling.MGControlTemplate Resolve(string name)
        {
            if (cache.TryGetValue(name, out Styling.MGControlTemplate cachedTemplate))
            {
                return cachedTemplate;
            }

            if (!definitionsByName.TryGetValue(name, out ControlTemplateDefinition definition))
            {
                return ResolveExternalTemplate?.Invoke(name);
            }

            if (!visiting.Add(name))
            {
                throw new InvalidOperationException($"Circular control template inheritance detected for '{name}'.");
            }

            try
            {
                Action<Styling.MGControlTemplateContext> applyDefaults = null;
                Styling.MGControlTemplate baseTemplate = null;
                if (!string.IsNullOrWhiteSpace(definition.BasedOn))
                {
                    baseTemplate = Resolve(definition.BasedOn);
                    if (baseTemplate == null)
                    {
                        throw new InvalidOperationException($"Control template '{definition.Name}' declares BasedOn='{definition.BasedOn}' but no matching base template was found.");
                    }

                    applyDefaults = baseTemplate.ApplyDefaults;
                }

                Styling.MGControlTemplate template;
                if (baseTemplate?.SupportsStructure == true && !DeclaresStructure(definition))
                {
                    // A variant that declares no root and no detached roots keeps the structure of its base: instantiating its own, empty,
                    // structure would miss every part the control requires.
                    // Sharing the structure template also lets an element switch between the base and the variant, as a theme mapping does,
                    // without rebuilding its parts.
                    template = Styling.MGControlTemplate.CreateStructureVariant(definition.Name, baseTemplate, applyDefaults);
                }
                else
                {
                    template = CreateTemplate(definition, applyDefaults);
                }
                cache[name] = template;
                return template;
            }
            finally
            {
                visiting.Remove(name);
            }
        }

        foreach (string name in definitionsByName.Keys)
        {
            _ = Resolve(name);
        }

        return cache;
    }

    public static IReadOnlyDictionary<string, Styling.MGControlTemplate> LoadAndRegister(MGResources Resources, XamlDocumentSource Source,
        bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        => LoadAndRegister(Resources, Source, XamlLoaderMode.Compatibility, SanitizeXAMLString, ReplaceLinebreakLiterals);

    public static IReadOnlyDictionary<string, Styling.MGControlTemplate> LoadAndRegister(MGResources Resources, XamlDocumentSource Source,
        XamlLoaderMode Mode, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
    {
        if (Resources == null)
        {
            throw new ArgumentNullException(nameof(Resources));
        }

        IReadOnlyList<ControlTemplateDefinition> definitions = ParseDefinitions(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
        IReadOnlyDictionary<string, Styling.MGControlTemplate> templates = BuildTemplates(definitions,
            name => Resources.TryGetControlTemplate(name, out Styling.MGControlTemplate template) ? template : null);
        foreach (KeyValuePair<string, Styling.MGControlTemplate> item in templates)
        {
            Resources.RemoveControlTemplate(item.Key);
            Resources.AddControlTemplate(item.Value);
        }

        return templates;
    }

    public static Styling.MGControlTemplate CreateTemplate(ControlTemplateDefinition Definition,
        Action<Styling.MGControlTemplateContext> ApplyDefaults)
    {
        if (Definition == null)
        {
            throw new ArgumentNullException(nameof(Definition));
        }

        // Backlog task 15: the pilot attributes of the structure are values of the template, not of the application.
        Definition.Root?.MarkAsTemplateStructure(Definition.Name);
        foreach (Element detachedRoot in Definition.DetachedRoots ?? new List<Element>())
        {
            detachedRoot?.MarkAsTemplateStructure(Definition.Name);
        }

        return new Styling.MGControlTemplate(
            Definition.Name,
            context => BuildStructure(Definition, context),
            null,
            ApplyDefaults ?? (_ => { }));
    }

    private static Styling.MGControlTemplate CreateTemplate(ControlTemplateDefinition Definition)
    {
        return CreateTemplate(Definition, null);
    }

    private static bool DeclaresStructure(ControlTemplateDefinition Definition)
        => Definition.Root != null || (Definition.DetachedRoots != null && Definition.DetachedRoots.Any());

    private static Styling.MGControlTemplateStructure BuildStructure(ControlTemplateDefinition Definition, Styling.MGControlTemplateContext Context)
    {
        if (Definition.Root == null && (Definition.DetachedRoots == null || !Definition.DetachedRoots.Any()))
        {
            return new Styling.MGControlTemplateStructure(null);
        }

        if (Context.Window == null)
        {
            throw new InvalidOperationException($"XAML control template '{Definition.Name}' requires a live window to instantiate its structure.");
        }

        MGElement root = Definition.Root?.ToElement<MGElement>(Context.Window, Context.Owner);
        List<MGElement> detachedRoots = Definition.DetachedRoots?
            .Where(x => x != null)
            .Select(x => x.ToElement<MGElement>(Context.Window, Context.Owner))
            .Where(x => x != null)
            .ToList() ?? new();

        Styling.MGControlTemplateStructure structure = new(root, null, detachedRoots);

        Dictionary<string, MGElement> namedElements = new(StringComparer.Ordinal);

        void collectNamedElements(MGElement candidateRoot)
        {
            if (candidateRoot == null)
            {
                return;
            }

            foreach (MGElement element in candidateRoot
                         .TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder)
                         .Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                namedElements.TryAdd(element.Name, element);
            }
        }

        collectNamedElements(root);
        foreach (MGElement detachedRoot in detachedRoots)
        {
            collectNamedElements(detachedRoot);
        }

        foreach (TemplatePartDefinition part in Definition.Parts)
        {
            string elementName = string.IsNullOrWhiteSpace(part.ElementName) ? part.Name : part.ElementName;
            if (!string.IsNullOrWhiteSpace(elementName) && namedElements.TryGetValue(elementName, out MGElement element))
            {
                structure.AddPart(part.Name, element);
            }
        }

        foreach (MGElement element in namedElements.Values.Distinct())
        {
            element.Name = null;
        }

        // Backlog task 15: the values the definition declared on its elements, recorded as Template contributions named "<template>:<element>.<property>"
        // by Element.ResolveXamlSource, are kept with the structure so that MGElement.ApplyControlTemplate applies them again after the defaults of
        // the base applicator. The prefix excludes the template values of the elements' own templates (a Window part applies "Window.*" keys).
        string declaredPrefix = Definition.Name + ":";
        IEnumerable<MGElement> structureElements = (root == null ? Enumerable.Empty<MGElement>() : root.TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder))
            .Concat(detachedRoots.SelectMany(x => x.TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder)))
            .Distinct();
        foreach (MGElement element in structureElements)
        {
            foreach (Styling.UITemplateDeclaredValue declared in MGElement.EnumerateTemplateContributions(element, declaredPrefix))
            {
                structure.AddDeclaredValue(declared);
            }
        }

        return structure;
    }
}