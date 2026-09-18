using System.Runtime.CompilerServices;

// Allow MGUI.Tests to reach internal helpers of the editor, such as the diagnostic-to-index
// conversion of XamlEditorTextPane, whose guard branches (a diagnostic with no line or column,
// a position outside the current text) cannot be forced through the public path.
[assembly: InternalsVisibleTo("MGUI.Tests")]
