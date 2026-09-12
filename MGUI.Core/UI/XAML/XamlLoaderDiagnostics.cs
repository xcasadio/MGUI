using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace MGUI.Core.UI.XAML
{
    public enum XamlLoaderMode
    {
        Compatibility,
        Strict
    }

    public enum XamlLoaderDiagnosticCode
    {
        ParseFailure,
        UnsupportedDocumentRoot,
        UnknownType,
        InvalidSetter,
        InvalidValueConversion,
        MissingTemplatePart,
        MissingResource,
        ThemeInheritanceCycle
    }

    public sealed record XamlLoaderDiagnostic(
        XamlLoaderDiagnosticCode Code,
        string DocumentKind,
        string SourceName,
        string FilePath,
        string Message,
        int? LineNumber = null,
        int? LinePosition = null);

    public sealed class XamlLoaderException : InvalidOperationException
    {
        public XamlLoaderDiagnostic Diagnostic { get; }

        public XamlLoaderException(XamlLoaderDiagnostic diagnostic, Exception innerException = null)
            : base(diagnostic?.Message, innerException)
        {
            Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        }
    }

    internal static class XamlLoaderDiagnostics
    {
        internal static T Execute<T>(XamlDocumentSource source, XamlLoaderMode mode, string documentKind, Func<T> action)
        {
            if (mode == XamlLoaderMode.Compatibility)
            {
                return action();
            }

            try
            {
                return action();
            }
            catch (XamlLoaderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateException(source, documentKind, ex);
            }
        }

        internal static void ValidateKnownElementNames(string markup, XamlDocumentSource source, string documentKind, XamlLoaderMode mode)
        {
            if (mode != XamlLoaderMode.Strict)
            {
                return;
            }

            XDocument document = XDocument.Parse(markup, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants())
            {
                string localName = element.Name.LocalName;
                if (localName.Contains('.', StringComparison.Ordinal))
                {
                    continue;
                }

                Type elementType = XAMLParser.ResolveElementType(localName);
                if (elementType != null)
                {
                    ValidateKnownAttributes(element, elementType, source, documentKind);
                    continue;
                }

                throw CreateException(
                    source,
                    documentKind,
                    XamlLoaderDiagnosticCode.UnknownType,
                    $"Unknown XAML element '{localName}'.",
                    TryGetLineNumber(element),
                    TryGetLinePosition(element));
            }
        }

        internal static void ValidateRequiredTemplateParts(IEnumerable<ControlTemplateDefinition> definitions, XamlDocumentSource source, XamlLoaderMode mode)
        {
            if (mode != XamlLoaderMode.Strict || definitions == null)
            {
                return;
            }

            foreach (ControlTemplateDefinition definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                HashSet<string> names = new(StringComparer.Ordinal);
                CollectNames(definition.Root, names);
                if (definition.DetachedRoots != null)
                {
                    foreach (Element detachedRoot in definition.DetachedRoots)
                    {
                        CollectNames(detachedRoot, names);
                    }
                }

                if (definition.Parts == null)
                {
                    continue;
                }

                foreach (TemplatePartDefinition part in definition.Parts)
                {
                    if (part == null || !part.IsRequired)
                    {
                        continue;
                    }

                    string elementName = string.IsNullOrWhiteSpace(part.ElementName) ? part.Name : part.ElementName;
                    if (!string.IsNullOrWhiteSpace(elementName) && names.Contains(elementName))
                    {
                        continue;
                    }

                    string templateName = string.IsNullOrWhiteSpace(definition.Name) ? "<unnamed>" : definition.Name;
                    throw CreateException(
                        source,
                        "control template document",
                        XamlLoaderDiagnosticCode.MissingTemplatePart,
                        $"Required template part '{part?.Name ?? "<unnamed>"}' references '{elementName ?? "<null>"}' but no element with that name exists in template '{templateName}'.");
                }
            }
        }

        internal static XamlLoaderException CreateUnsupportedDocumentRootException(
            XamlDocumentSource source,
            string documentKind,
            string rootName,
            string expectedRoots,
            int? lineNumber = null,
            int? linePosition = null)
        {
            return CreateException(
                source,
                documentKind,
                XamlLoaderDiagnosticCode.UnsupportedDocumentRoot,
                $"Unsupported {documentKind} root '{rootName}'. Expected {expectedRoots}.",
                lineNumber,
                linePosition);
        }

        internal static XamlLoaderException CreateException(
            XamlDocumentSource source,
            string documentKind,
            XamlLoaderDiagnosticCode code,
            string message,
            int? lineNumber = null,
            int? linePosition = null,
            Exception innerException = null)
        {
            XamlLoaderDiagnostic diagnostic = new(
                code,
                documentKind,
                source?.DisplayName,
                source?.FilePath,
                message,
                lineNumber,
                linePosition);

            return new XamlLoaderException(diagnostic, innerException);
        }

        internal static XamlLoaderException CreateException(XamlDocumentSource source, string documentKind, Exception exception)
        {
            return CreateException(
                source,
                documentKind,
                Classify(exception),
                GetDiagnosticMessage(exception),
                TryGetLineNumber(exception),
                TryGetLinePosition(exception),
                exception);
        }

        private static void CollectNames(Element element, ISet<string> names)
        {
            if (element == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(element.Name))
            {
                names.Add(element.Name);
            }

            foreach (Element child in element.GetChildren())
            {
                CollectNames(child, names);
            }
        }

        private static void ValidateKnownAttributes(XElement element, Type elementType, XamlDocumentSource source, string documentKind)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(attribute.Name.NamespaceName))
                {
                    continue;
                }

                string attributeName = attribute.Name.LocalName;
                if (attributeName.Contains('.', StringComparison.Ordinal))
                {
                    continue;
                }

                if (elementType.GetProperty(attributeName, BindingFlags.Instance | BindingFlags.Public) != null)
                {
                    continue;
                }

                throw CreateException(
                    source,
                    documentKind,
                    XamlLoaderDiagnosticCode.InvalidSetter,
                    $"Unknown XAML setter '{attributeName}' on element '{elementType.Name}'.",
                    TryGetLineNumber(attribute),
                    TryGetLinePosition(attribute));
            }
        }

        private static XamlLoaderDiagnosticCode Classify(Exception exception)
        {
            foreach (Exception current in EnumerateExceptionChain(exception))
            {
                string message = current?.Message ?? string.Empty;
                if (message.IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0
                    && message.IndexOf("document root", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.UnsupportedDocumentRoot;
                }

                if (message.IndexOf("template part", StringComparison.OrdinalIgnoreCase) >= 0
                    && message.IndexOf("no element", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.MissingTemplatePart;
                }

                if (message.IndexOf("cycle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.ThemeInheritanceCycle;
                }

                if (message.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("No theme named", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.MissingResource;
                }

                if (message.IndexOf("convert", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.InvalidValueConversion;
                }

                if (message.IndexOf("member", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("setter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.InvalidSetter;
                }

                if (message.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return XamlLoaderDiagnosticCode.UnknownType;
                }
            }

            return XamlLoaderDiagnosticCode.ParseFailure;
        }

        private static string GetDiagnosticMessage(Exception exception)
        {
            string outermost = null;
            foreach (Exception current in EnumerateExceptionChain(exception))
            {
                string message = current?.Message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                if (string.Equals(message, "Exception has been thrown by the target of an invocation.", StringComparison.Ordinal))
                {
                    continue;
                }

                outermost ??= message;

                //  A framework setter that validates its value (animation transition property, duration, easing: ADR-0006, S7) throws an
                //  InvalidOperationException that the XAML writer wraps in its own "set property ... threw" message: the root cause is the
                //  message worth surfacing, appended to the wrapper's so both the property and the reason are reported.
                if (current is InvalidOperationException && current.InnerException == null && !ReferenceEquals(message, outermost))
                {
                    return outermost + " " + message;
                }
            }

            return outermost ?? exception?.Message ?? "Unknown XAML loader failure.";
        }

        private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                yield return current;
                current = current.InnerException;
            }
        }

        private static int? TryGetLineNumber(Exception exception) => TryGetExceptionIntProperty(exception, "LineNumber");
        private static int? TryGetLinePosition(Exception exception) => TryGetExceptionIntProperty(exception, "LinePosition");

        private static int? TryGetExceptionIntProperty(Exception exception, string propertyName)
        {
            if (exception == null)
            {
                return null;
            }

            PropertyInfo property = exception.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType == typeof(int))
            {
                int value = (int)property.GetValue(exception);
                return value > 0 ? value : null;
            }

            return null;
        }

        private static int? TryGetLineNumber(XObject value)
        {
            if (value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                return lineInfo.LineNumber;
            }

            return null;
        }

        private static int? TryGetLinePosition(XObject value)
        {
            if (value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                return lineInfo.LinePosition;
            }

            return null;
        }
    }
}