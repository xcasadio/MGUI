namespace MGUI.Editor;

/// <summary>Holds the state of one edited XAML document: its text, the display name later given to
/// <c>XamlDocumentSource.FromString(markup, displayName)</c>, and the design-time data context used when previewing it.<para/>
/// This class does no file access and has no dependency on graphics beyond MGUI.Core types.</summary>
public class XamlEditorSession
{
    /// <summary>Raised whenever <see cref="Text"/> actually changes.</summary>
    public event EventHandler TextChanged;
    /// <summary>Raised whenever <see cref="SourceName"/> actually changes.</summary>
    public event EventHandler SourceNameChanged;
    /// <summary>Raised whenever <see cref="DesignDataContext"/> actually changes.</summary>
    public event EventHandler DesignDataContextChanged;

    private string _text = string.Empty;
    /// <summary>The current text of the edited document. Never null: assigning null stores <see cref="string.Empty"/> instead.</summary>
    public string Text
    {
        get => _text;
        set
        {
            string newValue = value ?? string.Empty;
            if (!string.Equals(_text, newValue, StringComparison.Ordinal))
            {
                _text = newValue;
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private string _sourceName = CreateDefaultSourceName();
    /// <summary>The display name later given to <c>XamlDocumentSource.FromString(markup, displayName)</c>.<para/>
    /// Defaults to a name unique to this session instance, for a document that is not backed by a file.<para/>
    /// Assigning null or whitespace throws <see cref="ArgumentException"/>.</summary>
    public string SourceName
    {
        get => _sourceName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("SourceName cannot be null or whitespace.", nameof(value));
            }

            if (!string.Equals(_sourceName, value, StringComparison.Ordinal))
            {
                _sourceName = value;
                SourceNameChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private object _designDataContext;
    /// <summary>The design-time data context used to preview this document. Null by default.</summary>
    public object DesignDataContext
    {
        get => _designDataContext;
        set
        {
            if (!ReferenceEquals(_designDataContext, value))
            {
                _designDataContext = value;
                DesignDataContextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private static string CreateDefaultSourceName() => "untitled-" + Guid.NewGuid().ToString("N") + ".xaml";
}
