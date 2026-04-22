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
}