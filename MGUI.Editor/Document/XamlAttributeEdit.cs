using System.Text;
using MGUI.Core.UI.TextEditing;

namespace MGUI.Editor.Document;

/// <summary>One text edit: replace <see cref="Range"/> (which may be empty, for an insertion) with <see cref="Text"/>. Produced by
/// <see cref="XamlAttributeEdit"/>, applied with <see cref="XamlAttributeEdit.Apply(string, XamlTextEdit)"/>.</summary>
public readonly record struct XamlTextEdit(MGTextRange Range, string Text);

/// <summary>Pure functions that compute the minimal <see cref="XamlTextEdit"/> for changing one attribute of a <see cref="XamlDocumentNode"/>,
/// without ever touching the document itself: the caller applies the edit to the pane's text and re-parses. Values are escaped
/// (<c>&amp;</c>, <c>&lt;</c>, and whichever quote character is in use); <c>&gt;</c> is left as-is, matching what the rest of the
/// document already does.</summary>
public static class XamlAttributeEdit
{
    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/> on <paramref name="node"/>: replaces the existing attribute's
    /// value (keeping its current quote character) when present, otherwise inserts <c> Name="value"</c> (double-quoted) right before the
    /// <c>&gt;</c> or <c>/&gt;</c> that ends the start tag -- before the whitespace that precedes <c>/&gt;</c> when the tag is
    /// self-closing, so <c>&lt;Button /&gt;</c> becomes <c>&lt;Button Width="120" /&gt;</c>.</summary>
    public static XamlTextEdit SetAttribute(XamlDocumentNode node, string name, string value)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name cannot be null or whitespace.", nameof(name));
        }

        var text = value ?? string.Empty;
        var existing = FindAttribute(node, name);
        if (existing != null)
        {
            return new XamlTextEdit(existing.ValueRange, Escape(text, existing.QuoteChar));
        }

        var insertText = $" {name}=\"{Escape(text, '"')}\"";
        return new XamlTextEdit(MGTextRange.EmptyAt(node.AttributeInsertionIndex), insertText);
    }

    /// <summary>Removes the attribute named <paramref name="name"/> from <paramref name="node"/>, together with the whitespace that
    /// precedes it. Returns <see langword="false"/> (and a default <paramref name="edit"/>) when the attribute is not present.</summary>
    public static bool TryRemoveAttribute(XamlDocumentNode node, string name, out XamlTextEdit edit)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var existing = FindAttribute(node, name);
        if (existing == null)
        {
            edit = default;
            return false;
        }

        edit = new XamlTextEdit(existing.FullRange, string.Empty);
        return true;
    }

    /// <summary>Applies <paramref name="edit"/> to <paramref name="text"/> and returns the resulting string. A helper for tests and
    /// callers that build up several edits: <see cref="XamlTextEdit.Range"/> is clamped to <paramref name="text"/>'s length first, so an
    /// edit computed against slightly different text (a stale document model) never throws.</summary>
    public static string Apply(string text, XamlTextEdit edit)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var range = edit.Range.Clamp(text.Length);
        return string.Concat(text.AsSpan(0, range.StartIndex), edit.Text, text.AsSpan(range.EndIndex));
    }

    private static XamlDocumentAttribute FindAttribute(XamlDocumentNode node, string name)
    {
        foreach (var attribute in node.Attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    private static string Escape(string value, char quoteChar)
    {
        StringBuilder builder = new(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '"' when quoteChar == '"':
                    builder.Append("&quot;");
                    break;
                case '\'' when quoteChar == '\'':
                    builder.Append("&apos;");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
