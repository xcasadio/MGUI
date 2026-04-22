using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using System;
using System.Collections.Generic;
using System.Text;

namespace MGUI.Core.Tooling
{
    public static class UIToolingService
    {
        /// <summary>Returns the stable desktop root segment used by diagnostic paths.</summary>
        public static string GetStableDiagnosticId(MGDesktop desktop)
        {
            if (desktop == null)
            {
                throw new ArgumentNullException(nameof(desktop));
            }

            return "desktop";
        }

        /// <summary>Returns a stable path-based identifier derived from window ancestry, element names, template-part names, and sibling order.</summary>
        public static string GetStableDiagnosticId(MGElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            List<string> segments = new();
            AppendStableDiagnosticSegments(element, segments);

            StringBuilder result = new();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    result.Append('/');
                }

                result.Append(segments[i]);
            }

            return result.ToString();
        }

        public static UIVisualTreeSnapshot CaptureVisualTree(MGElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return CreateSnapshot(root, 0);
        }

        private static UIVisualTreeSnapshot CreateSnapshot(MGElement element, int depth)
        {
            MGWindow window = element.SelfOrParentWindow ?? throw new InvalidOperationException(
                $"Unable to capture a visual tree snapshot for an element that is not attached to a {nameof(MGWindow)}.");

            List<UIVisualTreeSnapshot> children = new();
            IReadOnlyList<MGElement> visualChildren = element.GetVisualTreeChildren(true, true);
            for (int i = 0; i < visualChildren.Count; i++)
            {
                children.Add(CreateSnapshot(visualChildren[i], depth + 1));
            }

            Dictionary<string, string> templateParts = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MGElement> templatePart in element.TemplateParts)
            {
                templateParts[templatePart.Key] = templatePart.Value?.GetType().Name ?? nameof(MGElement);
            }

            return new(
                GetStableDiagnosticId(element),
                GetStableDiagnosticId(window),
                element.UniqueId,
                element.Name,
                element.ElementType,
                element.LayoutBounds,
                element.ActualLayoutBounds,
                element.AppliedControlTemplateName,
                templateParts,
                element.LastControlTemplateError,
                depth,
                children);
        }

        public static MGElement LoadPreview(MGWindow window, XamlDocumentSource source, object dataContext = null,
            bool sanitizeXamlString = false, bool replaceLinebreakLiterals = true)
        {
            return XAMLParser.LoadPreview(window, source, dataContext, sanitizeXamlString, replaceLinebreakLiterals);
        }

        private static void AppendStableDiagnosticSegments(MGElement element, List<string> segments)
        {
            if (element.Parent != null)
            {
                AppendStableDiagnosticSegments(element.Parent, segments);
                segments.Add(CreateChildSegment(element.Parent, element));
                return;
            }

            if (element is MGWindow nestedWindow && nestedWindow.ParentWindow != null)
            {
                AppendStableDiagnosticSegments(nestedWindow.ParentWindow, segments);
                segments.Add(CreateChildSegment(nestedWindow.ParentWindow, nestedWindow));
                return;
            }

            if (element is MGWindow rootWindow)
            {
                segments.Add(GetStableDiagnosticId(rootWindow.Desktop));
                segments.Add(CreateRootWindowSegment(rootWindow));
                return;
            }

            throw new InvalidOperationException(
                $"Unable to compute a stable diagnostic id for detached element '{element.GetType().Name}'.");
        }

        private static string CreateRootWindowSegment(MGWindow window)
        {
            if (ReferenceEquals(window, window.Desktop.OverlayHost?.SelfOrParentWindow))
            {
                return "overlay-window";
            }

            string typeToken = GetElementTypeToken(window);
            if (!string.IsNullOrWhiteSpace(window.Name))
            {
                return $"{typeToken}:{NormalizeIdentifierToken(window.Name)}";
            }

            int ordinal = GetRootWindowOrdinal(window);
            return ordinal >= 0
                ? $"{typeToken}[{ordinal}]"
                : $"{typeToken}:detached";
        }

        private static string CreateChildSegment(MGElement parent, MGElement child)
        {
            string typeToken = GetElementTypeToken(child);
            if (!string.IsNullOrWhiteSpace(child.Name))
            {
                return $"{typeToken}:{NormalizeIdentifierToken(child.Name)}";
            }

            if (TryGetTemplatePartName(parent, child, out string templatePartName))
            {
                return $"part:{NormalizeIdentifierToken(templatePartName)}";
            }

            return $"{typeToken}[{GetSiblingTypeOrdinal(parent, child)}]";
        }

        private static bool TryGetTemplatePartName(MGElement parent, MGElement child, out string templatePartName)
        {
            foreach (KeyValuePair<string, MGElement> templatePart in parent.TemplateParts)
            {
                if (ReferenceEquals(templatePart.Value, child) && !string.IsNullOrWhiteSpace(templatePart.Key))
                {
                    templatePartName = templatePart.Key;
                    return true;
                }
            }

            templatePartName = null;
            return false;
        }

        private static int GetRootWindowOrdinal(MGWindow window)
        {
            int ordinal = 0;
            for (int i = 0; i < window.Desktop.Windows.Count; i++)
            {
                MGWindow candidate = window.Desktop.Windows[i];
                if (ReferenceEquals(candidate, window))
                {
                    return ordinal;
                }

                if (candidate.ElementType == window.ElementType)
                {
                    ordinal++;
                }
            }

            return -1;
        }

        private static int GetSiblingTypeOrdinal(MGElement parent, MGElement child)
        {
            int ordinal = 0;
            IReadOnlyList<MGElement> siblings = parent.GetVisualTreeChildren(true, true);
            for (int i = 0; i < siblings.Count; i++)
            {
                MGElement sibling = siblings[i];
                if (ReferenceEquals(sibling, child))
                {
                    return ordinal;
                }

                if (sibling.ElementType == child.ElementType)
                {
                    ordinal++;
                }
            }

            return ordinal;
        }

        private static string GetElementTypeToken(MGElement element)
            => NormalizeIdentifierToken(element.ElementType.ToString());

        private static string NormalizeIdentifierToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            StringBuilder token = new(value.Length);
            bool lastWasSeparator = false;
            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    token.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    token.Append('-');
                    lastWasSeparator = true;
                }
            }

            while (token.Length > 0 && token[token.Length - 1] == '-')
            {
                token.Length--;
            }

            return token.Length > 0 ? token.ToString() : "unnamed";
        }
    }
}