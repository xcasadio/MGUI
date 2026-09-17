using System.Xml;
using System.Xml.Linq;
using MGUI.Core.Tooling;
using MGUI.Core.UI.TextEditing;

namespace MGUI.Editor.Document;

/// <summary>A single parse error of <see cref="XamlDocumentModel.TryParse(string, out XamlDocumentModel, out XamlDocumentParseError)"/>:
/// the underlying XML message plus the 1-based line and column it was reported at.</summary>
public sealed record XamlDocumentParseError(string Message, int LineNumber, int LinePosition);

/// <summary>One attribute of a <see cref="XamlDocumentNode"/>: its name as written (prefix included), its decoded value, the ranges of
/// its name and of its raw value (the text between the quotes, quotes excluded), the quote character used, and the range of the whole
/// attribute including the whitespace that precedes it (what a removal must delete). Every range is an <see cref="MGTextRange"/> into
/// the same LF text the owning <see cref="XamlDocumentModel"/> was built from. Namespace declarations (<c>xmlns</c>, <c>xmlns:x</c>) never
/// become an attribute of a node.</summary>
public sealed record XamlDocumentAttribute(string Name, string Value, MGTextRange NameRange, MGTextRange ValueRange, char QuoteChar, MGTextRange FullRange);

/// <summary>One XML element of the document, in document order. Every XML element becomes a node -- property-element syntax
/// (<c>Button.Content</c>) and objects that are not <see cref="MGUI.Core.UI.XAML.Element"/> DTOs (styles, setters, brushes, templates)
/// are walked and kept in the tree, they simply carry no <see cref="Ordinal"/> and no <see cref="DtoType"/>.</summary>
public sealed class XamlDocumentNode
{
    /// <summary>The element's local name, as written (no namespace prefix).</summary>
    public string LocalName { get; }

    /// <summary>The DTO type <see cref="LocalName"/> resolves to via <see cref="UIToolingService.TryResolveXamlElementType(string, out Type)"/>,
    /// or null when it does not resolve to a type deriving from <see cref="MGUI.Core.UI.XAML.Element"/> -- which is also true for
    /// <see cref="IsPropertyElement"/> nodes and for names that resolve to a non-<see cref="MGUI.Core.UI.XAML.Element"/> DTO (a style, a
    /// setter, a brush, a content template).</summary>
    public Type DtoType { get; }

    /// <summary>The zero-based, document-order rank of this node among the nodes whose <see cref="DtoType"/> is not null -- the same
    /// definition, and (on the same document) the same value, as <see cref="MGUI.Core.UI.XAML.XamlSourcePosition.Ordinal"/>. Null for a
    /// property element or a non-<see cref="MGUI.Core.UI.XAML.Element"/> object.</summary>
    public int? Ordinal { get; }

    /// <summary>The node this one is nested directly under an XML element of, or null for <see cref="XamlDocumentModel.Root"/>.</summary>
    public XamlDocumentNode Parent { get; }

    private readonly List<XamlDocumentNode> _children = new();
    /// <summary>The XML elements nested directly under this one's start tag, in document order.</summary>
    public IReadOnlyList<XamlDocumentNode> Children => _children;

    /// <summary><see langword="true"/> when <see cref="LocalName"/> contains a dot, i.e. this is property-element syntax
    /// (<c>Button.Content</c>) rather than an object element.</summary>
    public bool IsPropertyElement { get; }

    /// <summary><see langword="true"/> when the node was written as a self-closing tag (<c>&lt;Button /&gt;</c>).</summary>
    public bool IsSelfClosing { get; internal set; }

    /// <summary>From the opening <c>&lt;</c> to the <c>&gt;</c> that ends the start tag, that closing character included.</summary>
    public MGTextRange StartTagRange { get; internal set; }

    /// <summary>From the opening <c>&lt;</c> of the start tag to the <c>&gt;</c> of the matching end tag, or of the self-closing tag
    /// when <see cref="IsSelfClosing"/> (in which case this equals <see cref="StartTagRange"/>).</summary>
    public MGTextRange FullRange { get; internal set; }

    private readonly List<XamlDocumentAttribute> _attributes = new();
    /// <summary>This node's attributes, in document order, namespace declarations excluded.</summary>
    public IReadOnlyList<XamlDocumentAttribute> Attributes => _attributes;

    /// <summary>Where <see cref="XamlAttributeEdit.SetAttribute(XamlDocumentNode, string, string)"/> inserts a new attribute: right
    /// before the final <c>&gt;</c> of the start tag, or, for a self-closing tag, right before the run of whitespace that precedes the
    /// <c>/</c> (so that <c>&lt;Button /&gt;</c> becomes <c>&lt;Button Width="120" /&gt;</c>, not <c>&lt;Button Width="120"/&gt;</c>).</summary>
    internal int AttributeInsertionIndex { get; set; }

    internal XamlDocumentNode(string localName, Type dtoType, int? ordinal, XamlDocumentNode parent, bool isPropertyElement)
    {
        LocalName = localName;
        DtoType = dtoType;
        Ordinal = ordinal;
        Parent = parent;
        IsPropertyElement = isPropertyElement;
    }

    internal void AddChild(XamlDocumentNode child) => _children.Add(child);
    internal void AddAttribute(XamlDocumentAttribute attribute) => _attributes.Add(attribute);
}

/// <summary>A model of an edited XAML document's text: every XML node (object elements, property elements, styles, setters, ...), where
/// its start tag, its full extent and its attributes sit in the text. Built by <see cref="TryParse(string, out XamlDocumentModel, out XamlDocumentParseError)"/>,
/// which never throws -- a malformed document simply yields an error with its line and column, and no model.<para/>
/// Every <see cref="MGTextRange"/> this class hands out is a range into the exact <c>text</c> string <see cref="TryParse"/> was given
/// (already normalised to LF), never into a file's raw bytes.</summary>
public sealed class XamlDocumentModel
{
    /// <summary>The document's root node.</summary>
    public XamlDocumentNode Root { get; }

    /// <summary>Every node of the document, in document order (the order their start tags appear in the text).</summary>
    public IReadOnlyList<XamlDocumentNode> Nodes { get; }

    private readonly Dictionary<int, XamlDocumentNode> _nodesByOrdinal;

    private XamlDocumentModel(XamlDocumentNode root, List<XamlDocumentNode> nodes)
    {
        Root = root;
        Nodes = nodes;

        _nodesByOrdinal = new Dictionary<int, XamlDocumentNode>();
        foreach (var node in nodes)
        {
            if (node.Ordinal.HasValue)
            {
                _nodesByOrdinal[node.Ordinal.Value] = node;
            }
        }
    }

    /// <summary>The node whose <see cref="XamlDocumentNode.Ordinal"/> is <paramref name="ordinal"/>, if any.</summary>
    public bool TryGetNodeByOrdinal(int ordinal, out XamlDocumentNode node) => _nodesByOrdinal.TryGetValue(ordinal, out node);

    /// <summary>The deepest node whose <see cref="XamlDocumentNode.FullRange"/> contains <paramref name="textIndex"/> -- inside its
    /// start tag, inside a text child, or inside a nested element. Null when <paramref name="textIndex"/> falls outside <see cref="Root"/>.</summary>
    public XamlDocumentNode FindDeepestNodeAt(int textIndex)
    {
        if (Root == null || !Root.FullRange.ContainsIndex(textIndex))
        {
            return null;
        }

        var current = Root;
        while (true)
        {
            XamlDocumentNode next = null;
            foreach (var child in current.Children)
            {
                if (child.FullRange.ContainsIndex(textIndex))
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
            {
                return current;
            }

            current = next;
        }
    }

    /// <summary>Parses <paramref name="text"/> (the editor pane's text, already normalised to LF) into a <see cref="XamlDocumentModel"/>.
    /// Never throws: null or empty text, and malformed XML, both come back as <see langword="false"/> with <paramref name="error"/> set
    /// and <paramref name="model"/> null.</summary>
    public static bool TryParse(string text, out XamlDocumentModel model, out XamlDocumentParseError error)
    {
        model = null;
        error = null;

        var normalizedText = text ?? string.Empty;

        XDocument document;
        try
        {
            document = XDocument.Parse(normalizedText, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            error = new XamlDocumentParseError(ex.Message, ex.LineNumber > 0 ? ex.LineNumber : 1, ex.LinePosition > 0 ? ex.LinePosition : 1);
            return false;
        }

        if (document.Root == null)
        {
            error = new XamlDocumentParseError("The document has no root element.", 1, 1);
            return false;
        }

        var lineStarts = BuildLineStartTable(normalizedText);
        var nodes = new List<XamlDocumentNode>();
        var ordinalCounter = 0;
        var root = BuildNode(document.Root, null, normalizedText, lineStarts, nodes, ref ordinalCounter);
        AssignTagRanges(normalizedText, nodes);

        model = new XamlDocumentModel(root, nodes);
        return true;
    }

    private static List<int> BuildLineStartTable(string text)
    {
        List<int> lineStarts = new() { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        return lineStarts;
    }

    private static int ToIndex(IReadOnlyList<int> lineStarts, int line, int column)
    {
        var lineIndex = Math.Clamp(line - 1, 0, lineStarts.Count - 1);
        return lineStarts[lineIndex] + Math.Max(0, column - 1);
    }

    private static XamlDocumentNode BuildNode(XElement element, XamlDocumentNode parent, string text, IReadOnlyList<int> lineStarts,
        List<XamlDocumentNode> nodes, ref int ordinalCounter)
    {
        var lineInfo = (IXmlLineInfo)element;
        var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
        var column = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1;
        var nameStart = ToIndex(lineStarts, line, column);

        var localName = element.Name.LocalName;
        var isPropertyElement = localName.Contains('.', StringComparison.Ordinal);
        var dtoType = !isPropertyElement && UIToolingService.TryResolveXamlElementType(localName, out var resolved) ? resolved : null;
        int? ordinal = dtoType != null ? ordinalCounter++ : null;

        var node = new XamlDocumentNode(localName, dtoType, ordinal, parent, isPropertyElement)
        {
            //  Placeholder until AssignTagRanges walks the raw text; only ever observed if that pass somehow never reaches this
            //  node (can't happen for a document XDocument.Parse accepted), kept so StartTagRange/FullRange are never default.
            StartTagRange = MGTextRange.EmptyAt(nameStart > 0 ? nameStart - 1 : 0),
        };
        node.FullRange = node.StartTagRange;

        nodes.Add(node);
        parent?.AddChild(node);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            node.AddAttribute(BuildAttribute(attribute, text, lineStarts));
        }

        foreach (var child in element.Elements())
        {
            BuildNode(child, node, text, lineStarts, nodes, ref ordinalCounter);
        }

        return node;
    }

    private static XamlDocumentAttribute BuildAttribute(XAttribute attribute, string text, IReadOnlyList<int> lineStarts)
    {
        var lineInfo = (IXmlLineInfo)attribute;
        var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
        var column = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1;
        var nameStart = ToIndex(lineStarts, line, column);

        var nameEnd = nameStart;
        while (nameEnd < text.Length && text[nameEnd] != '=' && !char.IsWhiteSpace(text[nameEnd]))
        {
            nameEnd++;
        }

        var cursor = nameEnd;
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        //  cursor is now at '='.
        cursor++;
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        var quoteChar = text[cursor];
        var valueStart = cursor + 1;
        var valueEnd = text.IndexOf(quoteChar, valueStart);
        if (valueEnd < 0)
        {
            valueEnd = text.Length;
        }

        var wsStart = nameStart;
        while (wsStart > 0 && char.IsWhiteSpace(text[wsStart - 1]))
        {
            wsStart--;
        }

        var name = text.Substring(nameStart, nameEnd - nameStart);
        var nameRange = new MGTextRange(nameStart, nameEnd);
        var valueRange = new MGTextRange(valueStart, valueEnd);
        var fullRange = new MGTextRange(wsStart, valueEnd + 1);

        return new XamlDocumentAttribute(name, attribute.Value, nameRange, valueRange, quoteChar, fullRange);
    }

    /// <summary>Walks the raw text once, tag by tag, matching every start tag to <paramref name="nodes"/> (in the same document order
    /// both were built in) and every end tag to the innermost still-open start tag (a plain stack -- correct even for nested elements
    /// that share a local name). Comments, processing instructions and CDATA sections are skipped outright; quoted attribute values are
    /// skipped as opaque runs so a literal <c>&gt;</c> inside one (legal, unlike <c>&lt;</c>) never ends a start tag early. Valid XML by
    /// construction: this only runs once <see cref="TryParse"/>'s own <see cref="XDocument.Parse(string, LoadOptions)"/> already
    /// accepted the same text.</summary>
    private static void AssignTagRanges(string text, List<XamlDocumentNode> nodes)
    {
        var stack = new Stack<XamlDocumentNode>();
        var nodeCursor = 0;
        var length = text.Length;
        var i = 0;

        while (i < length)
        {
            if (text[i] != '<')
            {
                i++;
                continue;
            }

            if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0)
            {
                var end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? length : end + 3;
                continue;
            }

            if (string.CompareOrdinal(text, i, "<![CDATA[", 0, 9) == 0)
            {
                var end = text.IndexOf("]]>", i + 9, StringComparison.Ordinal);
                i = end < 0 ? length : end + 3;
                continue;
            }

            if (i + 1 < length && text[i + 1] == '?')
            {
                var end = text.IndexOf("?>", i + 2, StringComparison.Ordinal);
                i = end < 0 ? length : end + 2;
                continue;
            }

            if (i + 1 < length && text[i + 1] == '/')
            {
                var end = text.IndexOf('>', i + 2);
                if (end < 0)
                {
                    break;
                }

                if (stack.Count > 0)
                {
                    var closing = stack.Pop();
                    closing.FullRange = new MGTextRange(closing.StartTagRange.StartIndex, end + 1);
                }

                i = end + 1;
                continue;
            }

            //  Start tag (object element, property element, self-closing or not): scan to the matching un-quoted '>'.
            var j = i + 1;
            char? quote = null;
            while (j < length)
            {
                var c = text[j];
                if (quote.HasValue)
                {
                    if (c == quote.Value)
                    {
                        quote = null;
                    }
                }
                else if (c is '"' or '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    break;
                }

                j++;
            }

            if (j >= length)
            {
                break;
            }

            var isSelfClosing = j > i && text[j - 1] == '/';
            if (nodeCursor < nodes.Count)
            {
                var node = nodes[nodeCursor++];
                node.StartTagRange = new MGTextRange(i, j + 1);
                node.IsSelfClosing = isSelfClosing;

                if (isSelfClosing)
                {
                    var cursor = j - 2; // char right before '/'
                    while (cursor >= i && char.IsWhiteSpace(text[cursor]))
                    {
                        cursor--;
                    }

                    node.AttributeInsertionIndex = cursor + 1;
                    node.FullRange = node.StartTagRange;
                }
                else
                {
                    node.AttributeInsertionIndex = j;
                    stack.Push(node);
                }
            }

            i = j + 1;
        }
    }
}
