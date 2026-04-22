using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MGUI.Core.UI.XAML
{
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
        {
            if (Definitions == null)
            {
                throw new ArgumentNullException(nameof(Definitions));
            }

            Dictionary<string, Styling.MGControlTemplate> result = new(StringComparer.Ordinal);
            foreach (ControlTemplateDefinition definition in Definitions.Where(x => x != null))
            {
                string name = definition.Name ?? throw new InvalidOperationException($"{nameof(ControlTemplateDefinition)} requires a non-null {nameof(ControlTemplateDefinition.Name)}.");
                result[name] = CreateTemplate(definition);
            }

            return result;
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

            IReadOnlyDictionary<string, Styling.MGControlTemplate> templates = BuildTemplates(ParseDefinitions(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals));
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

            return structure;
        }
    }
}