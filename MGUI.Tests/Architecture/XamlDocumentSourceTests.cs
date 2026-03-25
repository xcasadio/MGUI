using MGUI.Core.UI.XAML;
using System.IO;

namespace MGUI.Tests.Architecture;

public class XamlDocumentSourceTests
{
    [Fact]
    public void StringSource_LoadsProvidedMarkup()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString("<Grid />", "Inline");

        Assert.Equal(XamlDocumentSourceKind.String, source.Kind);
        Assert.Equal("<Grid />", source.LoadContent());
        Assert.Equal("Inline", source.DisplayName);
    }

    [Fact]
    public void FileSource_LoadsMarkupFromFile()
    {
        string filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, "<Grid />");

            XamlDocumentSource source = XamlDocumentSource.FromFile(filePath);

            Assert.Equal(XamlDocumentSourceKind.File, source.Kind);
            Assert.Equal(Path.GetFullPath(filePath), source.FilePath);
            Assert.Equal("<Grid />", source.LoadContent());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Designer_UsesDocumentSourceAbstraction()
    {
        string designerSource = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGXAMLDesigner.cs");

        Assert.Contains("XamlDocumentSource.FromFile", designerSource);
        Assert.Contains("XamlDocumentSource.FromString", designerSource);
        Assert.Contains("UIToolingService.LoadPreview(SelfOrParentWindow, Source", designerSource);
    }

    [Fact]
    public void Parser_CanExposeReusableDefinitionPhase()
    {
        Element parsed = XAMLParser.ParseElementDefinition(XamlDocumentSource.FromString("<Grid />"), null, true, true);

        Assert.NotNull(parsed);
        Assert.Equal("Grid", parsed.GetType().Name);
    }

    [Fact]
    public void Parser_Recognizes_WrapPanel_Alias()
    {
        Element parsed = XAMLParser.ParseElementDefinition(XamlDocumentSource.FromString("<WrapPanel Orientation=\"Horizontal\" Spacing=\"6\" />"), null, true, true);

        Assert.NotNull(parsed);
        Assert.Equal("WrapPanel", parsed.GetType().Name);
    }

    [Fact]
    public void Parser_Recognizes_Canvas_Alias()
    {
        Element parsed = XAMLParser.ParseElementDefinition(XamlDocumentSource.FromString("<Canvas />"), null, true, true);

        Assert.NotNull(parsed);
        Assert.Equal("Canvas", parsed.GetType().Name);
    }

    [Fact]
    public void Canvas_Wrapper_Preserves_Attached_Coordinates_When_Adding_Children()
    {
        string containersSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\XAML\Containers.cs");

        Assert.Contains("Canvas.TryAddChild(ChildElement, Child.CanvasLeft, Child.CanvasTop, Child.CanvasRight, Child.CanvasBottom);", containersSource);
    }

    [Fact]
    public void Parser_Recognizes_NumericUpDown_Alias()
    {
        Element parsed = XAMLParser.ParseElementDefinition(XamlDocumentSource.FromString("<NumericUpDown Minimum=\"0\" Maximum=\"10\" Value=\"5\" />"), null, true, true);

        Assert.NotNull(parsed);
        Assert.Equal("NumericUpDown", parsed.GetType().Name);
    }
}