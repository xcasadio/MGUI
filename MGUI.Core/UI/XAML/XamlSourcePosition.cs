namespace MGUI.Core.UI.XAML;

/// <summary>Identifies the XAML node a parsed <see cref="Element"/> DTO was created from, so tooling (the XAML editor) can relate a
/// runtime <see cref="MGUI.Core.UI.MGElement"/> back to the exact place it was declared in the document's text.</summary>
/// <param name="SourceName">The <see cref="XamlDocumentSource.DisplayName"/> of the document this position was captured from -- the
/// same value the loader already carries in <see cref="XamlLoaderDiagnostic.SourceName"/>. Used to reject a position that does not
/// come from the document currently being edited.</param>
/// <param name="Ordinal">The zero-based, document-order rank of this element among the XML object elements whose name resolves to a
/// DTO type deriving from <see cref="Element"/>. Property elements (for example <c>Button.Content</c>) and objects that are not
/// <see cref="Element"/> DTOs (styles, setters, brushes, templates, markup extensions) do not count. <c>(SourceName, Ordinal)</c>
/// identifies a document node, not an instance: a <see cref="ContentTemplate"/> declared in the document is deep-cloned once per
/// generated item, and every clone keeps the ordinal of the template's own node.</param>
/// <param name="LineNumber">1-based line of the element's start tag, in the prepared markup (the document text after the literal
/// <c>\n</c> replacement performed by <c>XAMLParser.PrepareMarkup</c>).</param>
/// <param name="LinePosition">1-based column, in the prepared markup, of the first character of the element's name, immediately
/// after the opening <c>&lt;</c>.</param>
public readonly record struct XamlSourcePosition(string SourceName, int Ordinal, int LineNumber, int LinePosition);
