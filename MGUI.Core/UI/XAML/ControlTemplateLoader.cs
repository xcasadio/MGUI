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
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            string markup = Source.LoadContent();
            string rootName = XDocument.Parse(markup).Root?.Name.LocalName;
            if (rootName == nameof(ControlTemplatesDocument) || rootName == nameof(ControlTemplates))
            {
                ControlTemplatesDocument document = XAMLParser.ParseObjectDefinition<ControlTemplatesDocument>(Source, SanitizeXAMLString, ReplaceLinebreakLiterals);
                return document?.Templates ?? new List<ControlTemplateDefinition>();
            }

            if (rootName == nameof(ControlTemplateDefinition) || rootName == nameof(ControlTemplate))
            {
                ControlTemplateDefinition definition = XAMLParser.ParseObjectDefinition<ControlTemplateDefinition>(Source, SanitizeXAMLString, ReplaceLinebreakLiterals);
                return definition == null ? Array.Empty<ControlTemplateDefinition>() : new[] { definition };
            }

            throw new InvalidOperationException($"Unsupported control template document root '{rootName}'. Expected '{nameof(ControlTemplate)}' or '{nameof(ControlTemplatesDocument)}'.");
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
        {
            if (Resources == null)
            {
                throw new ArgumentNullException(nameof(Resources));
            }

            IReadOnlyDictionary<string, Styling.MGControlTemplate> templates = BuildTemplates(ParseDefinitions(Source, SanitizeXAMLString, ReplaceLinebreakLiterals));
            foreach (KeyValuePair<string, Styling.MGControlTemplate> item in templates)
            {
                Resources.RemoveControlTemplate(item.Key);
                Resources.AddControlTemplate(item.Value);
            }

            return templates;
        }

        private static Styling.MGControlTemplate CreateTemplate(ControlTemplateDefinition Definition)
        {
            return new Styling.MGControlTemplate(
                Definition.Name,
                context => BuildStructure(Definition, context),
                null,
                _ => { });
        }

        private static Styling.MGControlTemplateStructure BuildStructure(ControlTemplateDefinition Definition, Styling.MGControlTemplateContext Context)
        {
            if (Definition.Root == null)
            {
                return new Styling.MGControlTemplateStructure(null);
            }

            if (Context.Window == null)
            {
                throw new InvalidOperationException($"XAML control template '{Definition.Name}' requires a live window to instantiate its structure.");
            }

            MGElement root = Definition.Root.ToElement<MGElement>(Context.Window, Context.Owner);
            Styling.MGControlTemplateStructure structure = new(root);

            Dictionary<string, MGElement> namedElements = root
                .TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder)
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(root.Name))
            {
                namedElements[root.Name] = root;
            }

            foreach (TemplatePartDefinition part in Definition.Parts)
            {
                string elementName = string.IsNullOrWhiteSpace(part.ElementName) ? part.Name : part.ElementName;
                if (!string.IsNullOrWhiteSpace(elementName) && namedElements.TryGetValue(elementName, out MGElement element))
                {
                    structure.AddPart(part.Name, element);
                }
            }

            return structure;
        }
    }
}