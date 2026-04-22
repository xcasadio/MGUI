using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MGUI.Core.UI.XAML
{
    public static class ThemeDefinitionLoader
    {
        public static IReadOnlyList<ThemeDefinition> ParseDefinitions(XamlDocumentSource Source,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ParseDefinitions(Source, XamlLoaderMode.Compatibility, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public static IReadOnlyList<ThemeDefinition> ParseDefinitions(XamlDocumentSource Source, XamlLoaderMode Mode,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            return XamlLoaderDiagnostics.Execute<IReadOnlyList<ThemeDefinition>>(Source, Mode, "theme document", () =>
            {
                if (Source == null)
                {
                    throw new ArgumentNullException(nameof(Source));
                }

                string markup = Source.LoadContent();
                XDocument documentRoot = XDocument.Parse(markup, LoadOptions.SetLineInfo);
                string rootName = documentRoot.Root?.Name.LocalName;
                if (rootName == nameof(ThemeDefinitionsDocument))
                {
                    ThemeDefinitionsDocument document = XAMLParser.ParseObjectDefinition<ThemeDefinitionsDocument>(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
                    return document?.Themes ?? new List<ThemeDefinition>();
                }

                if (rootName == nameof(ThemeDefinition))
                {
                    ThemeDefinition definition = XAMLParser.ParseObjectDefinition<ThemeDefinition>(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
                    return definition == null ? Array.Empty<ThemeDefinition>() : new[] { definition };
                }

                if (Mode == XamlLoaderMode.Strict)
                {
                    throw XamlLoaderDiagnostics.CreateUnsupportedDocumentRootException(
                        Source,
                        "theme document",
                        rootName,
                        $"'{nameof(ThemeDefinition)}' or '{nameof(ThemeDefinitionsDocument)}'",
                        documentRoot.Root is null ? null : ((System.Xml.IXmlLineInfo)documentRoot.Root).LineNumber,
                        documentRoot.Root is null ? null : ((System.Xml.IXmlLineInfo)documentRoot.Root).LinePosition);
                }

                throw new InvalidOperationException($"Unsupported theme document root '{rootName}'. Expected '{nameof(ThemeDefinition)}' or '{nameof(ThemeDefinitionsDocument)}'.");
            });
        }

        public static IReadOnlyDictionary<string, MGTheme> BuildThemes(IEnumerable<ThemeDefinition> Definitions,
            Func<string, MGTheme> ResolveExternalTheme, string DefaultFontFamily)
        {
            if (Definitions == null)
            {
                throw new ArgumentNullException(nameof(Definitions));
            }

            Dictionary<string, ThemeDefinition> DefinitionMap = Definitions
                .Where(x => x != null)
                .ToDictionary(x => x.Name ?? throw new InvalidOperationException($"{nameof(ThemeDefinition)} requires a non-null {nameof(ThemeDefinition.Name)}."));

            Dictionary<string, MGTheme> Result = new(StringComparer.Ordinal);
            HashSet<string> Visiting = new(StringComparer.Ordinal);

            foreach (string Name in DefinitionMap.Keys)
            {
                Resolve(Name, DefinitionMap, Result, Visiting, ResolveExternalTheme, DefaultFontFamily);
            }

            return Result;
        }

        public static IReadOnlyDictionary<string, MGTheme> LoadAndRegister(MGResources Resources, XamlDocumentSource Source,
            string DefaultFontFamily = null, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => LoadAndRegister(Resources, Source, DefaultFontFamily, XamlLoaderMode.Compatibility, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public static IReadOnlyDictionary<string, MGTheme> LoadAndRegister(MGResources Resources, XamlDocumentSource Source,
            string DefaultFontFamily, XamlLoaderMode Mode, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            return XamlLoaderDiagnostics.Execute<IReadOnlyDictionary<string, MGTheme>>(Source, Mode, "theme document", () =>
            {
                if (Resources == null)
                {
                    throw new ArgumentNullException(nameof(Resources));
                }

                IReadOnlyList<ThemeDefinition> Definitions = ParseDefinitions(Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);
                string FontFamily = DefaultFontFamily ?? Resources.DefaultTheme?.FontSettings?.DefaultFontFamily;
                IReadOnlyDictionary<string, MGTheme> Themes = BuildThemes(
                    Definitions,
                    Name =>
                    {
                        if (Resources.TryGetTheme(Name, out MGTheme ExistingTheme))
                        {
                            return ExistingTheme;
                        }

                        if (MGTheme.TryCreateBuiltInTheme(Name, FontFamily, out MGTheme BuiltInTheme))
                        {
                            return BuiltInTheme;
                        }

                        return null;
                    },
                    FontFamily);

                foreach (KeyValuePair<string, MGTheme> Item in Themes)
                {
                    if (Resources.RemoveTheme(Item.Key))
                    {
                    }
                    Resources.AddTheme(Item.Key, Item.Value);
                }

                return Themes;
            });
        }

        private static MGTheme Resolve(string Name, IReadOnlyDictionary<string, ThemeDefinition> Definitions,
            IDictionary<string, MGTheme> Cache, ISet<string> Visiting, Func<string, MGTheme> ResolveExternalTheme, string DefaultFontFamily)
        {
            if (Cache.TryGetValue(Name, out MGTheme Existing))
            {
                return Existing;
            }

            if (!Definitions.TryGetValue(Name, out ThemeDefinition Definition))
            {
                MGTheme ExternalTheme = ResolveExternalTheme?.Invoke(Name);
                if (ExternalTheme == null)
                {
                    throw new InvalidOperationException($"No theme named '{Name}' was found while resolving {nameof(ThemeDefinition)} inheritance.");
                }

                return ExternalTheme;
            }

            if (!Visiting.Add(Name))
            {
                throw new InvalidOperationException($"A cycle was detected while resolving theme '{Name}'.");
            }

            MGTheme BaseTheme = null;
            if (!string.IsNullOrWhiteSpace(Definition.BasedOn))
            {
                BaseTheme = Resolve(Definition.BasedOn, Definitions, Cache, Visiting, ResolveExternalTheme, DefaultFontFamily);
            }

            MGTheme Result = ThemeDefinitionBuilder.Build(Definition, DefaultFontFamily, BaseTheme);
            Cache[Name] = Result;
            Visiting.Remove(Name);
            return Result;
        }
    }
}