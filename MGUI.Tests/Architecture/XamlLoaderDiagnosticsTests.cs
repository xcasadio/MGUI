using System;
using MGUI.Core.UI.XAML;
using Xunit;

namespace MGUI.Tests.Architecture;

public class XamlLoaderDiagnosticsTests
{
    [Fact]
    public void XamlParser_StrictMode_Reports_Unknown_Element_Type()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(
            "<DoesNotExist xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" />",
            "UnknownElement.xaml");

        XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
            XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));

        Assert.Equal(XamlLoaderDiagnosticCode.UnknownType, exception.Diagnostic.Code);
        Assert.Equal("UnknownElement.xaml", exception.Diagnostic.SourceName);
        Assert.Contains("DoesNotExist", exception.Message);
    }

    [Fact]
    public void XamlParser_CompatibilityMode_Preserves_Legacy_Exception_Surface()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(
            "<DoesNotExist xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" />");

        Exception exception = Assert.ThrowsAny<Exception>(() =>
            XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Compatibility, false, true));

        Assert.IsNotType<XamlLoaderException>(exception);
    }

    [Fact]
    public void XamlParser_StrictMode_Reports_Invalid_Setter()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(
            "<Button xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" MissingPadding=\"4\" />",
            "InvalidSetter.xaml");

        XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
            XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, exception.Diagnostic.Code);
        Assert.Equal("InvalidSetter.xaml", exception.Diagnostic.SourceName);
    }

    [Fact]
    public void ControlTemplateLoader_StrictMode_Reports_Unsupported_Document_Root()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(
            "<Grid xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" />",
            "BrokenTemplate.xaml");

        XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
            ControlTemplateLoader.ParseDefinitions(source, XamlLoaderMode.Strict, false, true));

        Assert.Equal(XamlLoaderDiagnosticCode.UnsupportedDocumentRoot, exception.Diagnostic.Code);
        Assert.Equal("BrokenTemplate.xaml", exception.Diagnostic.SourceName);
        Assert.Contains("control template document", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ControlTemplateLoader_StrictMode_Rejects_Missing_Required_Template_Part()
    {
        const string markup = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Overlay.Chrome""
                 TargetType=""Overlay"">
  <Border Name=""ChromeRoot"" />
  <ControlTemplateDefinition.Parts>
    <TemplatePart Name=""PART_Missing"" />
  </ControlTemplateDefinition.Parts>
</ControlTemplate>";

        XamlDocumentSource source = XamlDocumentSource.FromString(markup, "MissingPart.xaml");

        XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
            ControlTemplateLoader.ParseDefinitions(source, XamlLoaderMode.Strict, false, true));

        Assert.Equal(XamlLoaderDiagnosticCode.MissingTemplatePart, exception.Diagnostic.Code);
        Assert.Equal("MissingPart.xaml", exception.Diagnostic.SourceName);
        Assert.Contains("PART_Missing", exception.Message);
    }

    // -- ADR-0013: a name declared twice in one element or window document ----

    private const string TwiceMarkup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="300">
            <StackPanel Orientation="Vertical">
                <Button Name="Same" Content="First" />
                <Button Name="Same" Content="Second" />
            </StackPanel>
        </Window>
        """;

    [Fact]
    public void XamlParser_StrictMode_Reports_A_Name_Declared_Twice_At_The_Second_Declaration()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(TwiceMarkup, "Twice.xaml");

        XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
            XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, exception.Diagnostic.Code);
        Assert.Equal("Twice.xaml", exception.Diagnostic.SourceName);

        // The reported position is the second declaration, the one to rename; the first one is named in the message.
        Assert.Equal(4, exception.Diagnostic.LineNumber);
        Assert.Equal(10, exception.Diagnostic.LinePosition);
        Assert.Contains("'Same'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Button'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("already declared at line 3, column 10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void XamlParser_CompatibilityMode_Does_Not_Report_A_Name_Declared_Twice()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(TwiceMarkup, "Twice.xaml");

        // Parsing alone builds no element, so nothing can collide yet: the failure of the compatibility path stays where it
        // has always been, in the window's index, once the tree is built and attached.
        Element parsed = XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Compatibility, false, true);

        Assert.NotNull(parsed);
    }

    /// <summary>A name declared inside a template is declared once, whatever the number of elements the template will generate: the
    /// document is valid and the loader must not refuse it. What that name costs at runtime is
    /// <see cref="MGUI.Core.UI.MGDuplicateElementNameException"/>'s job to explain.</summary>
    [Fact]
    public void XamlParser_StrictMode_Accepts_A_Name_Declared_Once_Inside_A_Template()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="300">
                <ListBox Name="Options">
                    <ListBox.ItemTemplate>
                        <ContentTemplate>
                            <TextBlock Name="ItemLabel" Text="Item" />
                        </ContentTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Window>
            """, "Template.xaml");

        Assert.NotNull(XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));
    }

    /// <summary>Only names of elements count. A <c>Style</c> name is a key of the resource table, a visual state name is a key of the
    /// animation tables: neither ever reaches a window's element index, so neither may collide with an element's name.</summary>
    [Fact]
    public void XamlParser_StrictMode_Ignores_Names_That_Are_Not_Element_Names()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="300">
                <Window.Styles>
                    <Style Name="Shared" TargetType="TextBlock">
                        <Setter Property="IsBold" Value="True" />
                    </Style>
                </Window.Styles>
                <Button Name="Shared" Content="OK" />
            </Window>
            """, "Styles.xaml");

        Assert.NotNull(XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));
    }

    /// <summary>The repository's own control template document repeats twenty-four part names from one definition to the next. It is an
    /// object definition, which the uniqueness rule deliberately does not cover: this pins that it keeps loading in strict mode.</summary>
    [Fact]
    public void ControlTemplateLoader_StrictMode_Accepts_The_BuiltIn_Templates_And_Their_Repeated_Part_Names()
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "MGUI.Core", "UI", "Templates", "BuiltInControlTemplates.xaml"));
        Assert.True(System.IO.File.Exists(path), $"Not found: {path}");

        string markup = System.IO.File.ReadAllText(path);
        Assert.Contains("PART_Border", markup, StringComparison.Ordinal);

        IReadOnlyList<ControlTemplateDefinition> definitions = ControlTemplateLoader.ParseDefinitions(
            XamlDocumentSource.FromString(markup, "BuiltInControlTemplates.xaml"), XamlLoaderMode.Strict, false, true);

        Assert.NotEmpty(definitions);
    }
}