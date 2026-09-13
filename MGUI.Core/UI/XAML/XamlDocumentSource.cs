using System.Text;

namespace MGUI.Core.UI.XAML;

public enum XamlDocumentSourceKind
{
    String,
    File,
    Stream
}

public class XamlDocumentSource
{
    public XamlDocumentSourceKind Kind { get; }
    public string DisplayName { get; }
    public string FilePath { get; }

    private Func<string> ReadContent { get; }

    private XamlDocumentSource(XamlDocumentSourceKind Kind, Func<string> ReadContent, string DisplayName = null, string FilePath = null)
    {
        this.Kind = Kind;
        this.ReadContent = ReadContent ?? throw new ArgumentNullException(nameof(ReadContent));
        this.DisplayName = DisplayName;
        this.FilePath = FilePath;
    }

    public string LoadContent() => ReadContent();

    public static XamlDocumentSource FromString(string Markup, string DisplayName = null)
    {
        if (Markup == null)
        {
            throw new ArgumentNullException(nameof(Markup));
        }

        return new(XamlDocumentSourceKind.String, () => Markup, DisplayName);
    }

    public static XamlDocumentSource FromFile(string FilePath)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("A valid file path is required.", nameof(FilePath));
        }

        string FullPath = Path.GetFullPath(FilePath);
        return new(XamlDocumentSourceKind.File, () => File.ReadAllText(FullPath), Path.GetFileName(FullPath), FullPath);
    }

    public static XamlDocumentSource FromStream(Func<Stream> StreamFactory, string DisplayName = null)
    {
        if (StreamFactory == null)
        {
            throw new ArgumentNullException(nameof(StreamFactory));
        }

        return new(XamlDocumentSourceKind.Stream, () =>
        {
            using Stream Stream = StreamFactory();
            using StreamReader Reader = new(Stream, Encoding.UTF8, true, 1024, false);
            return Reader.ReadToEnd();
        }, DisplayName);
    }
}