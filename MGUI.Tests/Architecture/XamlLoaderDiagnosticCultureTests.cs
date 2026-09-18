using System.Globalization;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

/// <summary>These tests change <see cref="CultureInfo.CurrentUICulture"/>, the ambient state the XAML library reads to word its exceptions:
/// the collection below is executed on its own, so no other test runs while it is changed.</summary>
[CollectionDefinition(UICultureCollection.Name, DisableParallelization = true)]
public sealed class UICultureCollection
{
    public const string Name = "CultureInfo.CurrentUICulture";
}

/// <summary>A <see cref="XamlLoaderDiagnosticCode"/> must not depend on the language of the machine. System.Xaml ships satellite resources,
/// so under a French UI culture its exceptions are worded in French (<see cref="FrenchUICulture_LocalisesTheXamlLibraryMessages"/> proves
/// the tests below really exercise that): every case runs under both cultures and expects the same code.</summary>
[Collection(UICultureCollection.Name)]
public class XamlLoaderDiagnosticCultureTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    public static TheoryData<string> Cultures => new() { "en-US", "fr-FR" };

    private static XamlLoaderDiagnostic LoadStrictExpectingFailure(string culture, string markup)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            XamlLoaderException exception = Assert.Throws<XamlLoaderException>(() =>
                XAMLParser.ParseElementDefinition(XamlDocumentSource.FromString(markup, "Culture.xaml"), null, XamlLoaderMode.Strict, false, true));
            return exception.Diagnostic;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static string Transition(string attributes) =>
        $"<Button xmlns=\"{Ns}\" Content=\"Hi\"><Button.Transitions><Transition {attributes} /></Button.Transitions></Button>";

    /// <summary>Guard against a vacuous pass: if the French resources of the XAML library were missing, every message would stay in English
    /// and the tests below would prove nothing about localisation.</summary>
    [Fact]
    public void FrenchUICulture_LocalisesTheXamlLibraryMessages()
    {
        string markup = $"<Button xmlns=\"{Ns}\" Width=\"abc\" />";

        XamlLoaderDiagnostic english = LoadStrictExpectingFailure("en-US", markup);
        XamlLoaderDiagnostic french = LoadStrictExpectingFailure("fr-FR", markup);

        Assert.NotEqual(english.Message, french.Message);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnconvertibleValue_IsInvalidValueConversion(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" Width=\"abc\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnconvertibleEnumValue_IsInvalidValueConversion(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" HorizontalAlignment=\"Nope\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, diagnostic.Code);
    }

    /// <summary>The Thickness converter rejects three values with a bare <see cref="ArgumentException"/> whose message is the value itself:
    /// neither the exception type nor its text says "conversion", only the converter on the call stack does.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValueRejectedByAnMguiConverter_IsInvalidValueConversion(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" Padding=\"1,2,3\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ThrowingSetter_IsInvalidSetter(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, Transition("Property=\"\" Duration=\"0.1\""));

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, diagnostic.Code);
        Assert.Contains("A Transition needs a Property path.", diagnostic.Message);
    }

    /// <summary>"A VisualStateDefinition needs a Name." holds none of the words the classification looks for: only the set accessor on the
    /// call stack says a setter threw.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void ThrowingSetter_WithoutAnyKnownWord_IsInvalidSetter(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture,
            $"<Button xmlns=\"{Ns}\" Content=\"Hi\"><Button.VisualStates><VisualStateDefinition Name=\"\" /></Button.VisualStates></Button>");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, diagnostic.Code);
        Assert.Contains("A VisualStateDefinition needs a Name.", diagnostic.Message);
    }

    /// <summary>A setter that rejects its value with MGUI's own "Cannot convert ..." is a conversion failure: that specific reason wins over
    /// the XAML library's generic "set property ... threw" wrapper, whatever the language of the wrapper.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void SetterRejectingItsValueAsUnconvertible_IsInvalidValueConversion(string culture)
    {
        XamlLoaderDiagnostic easing = LoadStrictExpectingFailure(culture, Transition("Property=\"Opacity\" Duration=\"0.1\" Easing=\"Wobble\""));
        XamlLoaderDiagnostic duration = LoadStrictExpectingFailure(culture, Transition("Property=\"Opacity\" Duration=\"fast\""));
        XamlLoaderDiagnostic property = LoadStrictExpectingFailure(culture, Transition("Property=\"Nope\" Duration=\"0.1\""));

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, easing.Code);
        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, duration.Code);
        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, property.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnknownAttribute_IsInvalidSetter(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" MissingPadding=\"4\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, diagnostic.Code);
    }

    /// <summary>A dotted name skips the loader's own attribute validation and reaches the XAML library, which reports it as an unknown member.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnknownPropertyElement_IsInvalidSetter(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\"><Button.Nope>1</Button.Nope></Button>");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void DuplicateMember_IsInvalidSetter(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture,
            $"<Button xmlns=\"{Ns}\" Width=\"10\"><Button.Width>20</Button.Width></Button>");

        Assert.Equal(XamlLoaderDiagnosticCode.InvalidSetter, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnknownElementType_IsUnknownType(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<DoesNotExist xmlns=\"{Ns}\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.UnknownType, diagnostic.Code);
    }

    /// <summary>A markup extension sits in an attribute value, which the loader's own element validation does not look into: the unknown type
    /// is reported by the XAML library.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void UnknownMarkupExtensionType_IsUnknownType(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" Content=\"{{Nope}}\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.UnknownType, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void MalformedXml_IsParseFailure(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" Width=\"10\">");

        Assert.Equal(XamlLoaderDiagnosticCode.ParseFailure, diagnostic.Code);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void MalformedMarkupExtension_IsParseFailure(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"<Button xmlns=\"{Ns}\" Content=\"{{MGBinding Path=\" />");

        Assert.Equal(XamlLoaderDiagnosticCode.ParseFailure, diagnostic.Code);
    }

    /// <summary>ADR-0013. The duplicate-name rule is MGUI's own and reads no library message, so it must hold identically under
    /// either UI culture -- position included, since the position comes from the XML reader and not from an exception's text.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void NameDeclaredTwice_IsDuplicateElementName(string culture)
    {
        XamlLoaderDiagnostic diagnostic = LoadStrictExpectingFailure(culture, $"""
            <StackPanel xmlns="{Ns}" Orientation="Vertical">
            <Button Name="Same" />
            <Button Name="Same" />
            </StackPanel>
            """);

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, diagnostic.Code);
        Assert.Equal(3, diagnostic.LineNumber);
        Assert.Equal(2, diagnostic.LinePosition);
        Assert.Contains("'Same'", diagnostic.Message, StringComparison.Ordinal);
    }
}
