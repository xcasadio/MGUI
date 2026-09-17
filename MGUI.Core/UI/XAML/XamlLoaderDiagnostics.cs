using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

#if UseWPF
using System.Xaml;
#else
using Portable.Xaml;
#endif

namespace MGUI.Core.UI.XAML;

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

        var document = XDocument.Parse(markup, LoadOptions.SetLineInfo);
        foreach (var element in document.Descendants())
        {
            var localName = element.Name.LocalName;
            if (localName.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            var elementType = XAMLParser.ResolveElementType(localName);
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

        foreach (var definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            CollectNames(definition.Root, names);
            if (definition.DetachedRoots != null)
            {
                foreach (var detachedRoot in definition.DetachedRoots)
                {
                    CollectNames(detachedRoot, names);
                }
            }

            if (definition.Parts == null)
            {
                continue;
            }

            foreach (var part in definition.Parts)
            {
                if (part == null || !part.IsRequired)
                {
                    continue;
                }

                var elementName = string.IsNullOrWhiteSpace(part.ElementName) ? part.Name : part.ElementName;
                if (!string.IsNullOrWhiteSpace(elementName) && names.Contains(elementName))
                {
                    continue;
                }

                var templateName = string.IsNullOrWhiteSpace(definition.Name) ? "<unnamed>" : definition.Name;
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

        foreach (var child in element.GetChildren())
        {
            CollectNames(child, names);
        }
    }

    private static void ValidateKnownAttributes(XElement element, Type elementType, XamlDocumentSource source, string documentKind)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(attribute.Name.NamespaceName))
            {
                continue;
            }

            var attributeName = attribute.Name.LocalName;
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
        //  The XAML library words its exceptions in the language of CultureInfo.CurrentUICulture (System.Xaml ships satellite resources),
        //  so the text of a parser exception is never read: the code must not depend on the language of the machine. Only the messages
        //  MGUI writes, and those of the base class library, which .NET does not localise, are matched. They name the specific reason of
        //  a failure and are matched first, so that reason wins over the library's generic wrapper: a setter that rejects its value with
        //  "Cannot convert ..." is a conversion failure, whatever "set property ... threw" text surrounds it.
        foreach (var current in EnumerateExceptionChain(exception))
        {
            if (!IsParserException(current) && TryClassifyByMessage(current.Message ?? string.Empty, out var code))
            {
                return code;
            }
        }

        foreach (var current in EnumerateExceptionChain(exception))
        {
            if (TryClassifyParserException(current, out var code))
            {
                return code;
            }
        }

        return XamlLoaderDiagnosticCode.ParseFailure;
    }

    private static bool IsParserException(Exception exception) => exception is XamlException or XmlException;

    private static bool TryClassifyByMessage(string message, out XamlLoaderDiagnosticCode code)
    {
        if (message.IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0
            && message.IndexOf("document root", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.UnsupportedDocumentRoot;
            return true;
        }

        if (message.IndexOf("template part", StringComparison.OrdinalIgnoreCase) >= 0
            && message.IndexOf("no element", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.MissingTemplatePart;
            return true;
        }

        if (message.IndexOf("cycle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.ThemeInheritanceCycle;
            return true;
        }

        if (message.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("No theme named", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.MissingResource;
            return true;
        }

        if (message.IndexOf("convert", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.InvalidValueConversion;
            return true;
        }

        if (message.IndexOf("member", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("setter", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.InvalidSetter;
            return true;
        }

        if (message.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            code = XamlLoaderDiagnosticCode.UnknownType;
            return true;
        }

        code = default;
        return false;
    }

    private static bool TryClassifyParserException(Exception exception, out XamlLoaderDiagnosticCode code)
    {
        switch (exception)
        {
            case XmlException:
            case XamlParseException:
                code = XamlLoaderDiagnosticCode.ParseFailure;
                return true;

            case XamlDuplicateMemberException:
                code = XamlLoaderDiagnosticCode.InvalidSetter;
                return true;

            //  A mere wrapper around another parser exception: the wrapped one is classified on its own, further down the chain.
            case XamlObjectWriterException when IsParserException(exception.InnerException):
                break;

            case XamlObjectWriterException when exception.InnerException != null:
                code = ClassifyMemberValueFailure(exception.InnerException);
                return true;

            case XamlObjectWriterException:
                return TryClassifyWriterCall(exception, out code);
        }

        code = default;
        return false;
    }

    /// <summary>A value the library could not convert and a setter that threw surface as the same exception type around an arbitrary inner
    /// exception. What tells them apart is what the library was calling, read from the stack trace of <paramref name="cause"/> through
    /// public contracts only: text is converted by a <see cref="TypeConverter"/>, a member is assigned through its set accessor. The
    /// outermost such frame wins, so a setter that runs a converter of its own is still a setter failure.</summary>
    private static XamlLoaderDiagnosticCode ClassifyMemberValueFailure(Exception cause)
    {
        foreach (var current in EnumerateExceptionChain(cause))
        {
            var frames = new StackTrace(current, false).GetFrames() ?? Array.Empty<StackFrame>();
            for (var i = frames.Length - 1; i >= 0; i--)
            {
                var method = frames[i].GetMethod();
                if (method == null)
                {
                    continue;
                }

                if (typeof(TypeConverter).IsAssignableFrom(method.DeclaringType))
                {
                    return XamlLoaderDiagnosticCode.InvalidValueConversion;
                }

                if (method.IsSpecialName && method.Name.StartsWith("set_", StringComparison.Ordinal))
                {
                    return XamlLoaderDiagnosticCode.InvalidSetter;
                }
            }
        }

        return XamlLoaderDiagnosticCode.InvalidSetter;
    }

    /// <summary>Without an inner exception, an unknown type and an unknown member are the same exception type: the public
    /// <see cref="XamlObjectWriter"/> method the library was running tells them apart.</summary>
    private static bool TryClassifyWriterCall(Exception exception, out XamlLoaderDiagnosticCode code)
    {
        foreach (var frame in new StackTrace(exception, false).GetFrames() ?? Array.Empty<StackFrame>())
        {
            var method = frame.GetMethod();
            if (method == null || !typeof(XamlObjectWriter).IsAssignableFrom(method.DeclaringType))
            {
                continue;
            }

            switch (method.Name)
            {
                case nameof(XamlObjectWriter.WriteStartObject):
                    code = XamlLoaderDiagnosticCode.UnknownType;
                    return true;

                case nameof(XamlObjectWriter.WriteStartMember):
                    code = XamlLoaderDiagnosticCode.InvalidSetter;
                    return true;
            }
        }

        code = default;
        return false;
    }

    private static string GetDiagnosticMessage(Exception exception)
    {
        string outermost = null;
        foreach (var current in EnumerateExceptionChain(exception))
        {
            var message = current?.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (string.Equals(message, "Exception has been thrown by the target of an invocation.", StringComparison.Ordinal))
            {
                continue;
            }

            outermost ??= message;

            //  A framework setter that validates its value (animation transition property, duration, easing: ADR-0006) throws an
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
        var current = exception;
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

        var property = exception.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType == typeof(int))
        {
            var value = (int)property.GetValue(exception);
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