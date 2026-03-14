using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.Tooling
{
    public static class UIToolingService
    {
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
            List<UIVisualTreeSnapshot> children = element.GetVisualTreeChildren(true, true)
                .Select(child => CreateSnapshot(child, depth + 1))
                .ToList();

            Dictionary<string, string> templateParts = element.TemplateParts
                .ToDictionary(x => x.Key, x => x.Value?.GetType().Name ?? nameof(MGElement));

            return new(
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
    }
}