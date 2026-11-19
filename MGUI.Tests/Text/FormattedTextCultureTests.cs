using System.Globalization;
using MGUI.Core.UI.Text;

namespace MGUI.Tests.Text;

/// <summary>
/// What is a tag does not depend on the culture of the process. Under a Turkish or an Azerbaijani culture an uppercase <c>I</c> is not the letter <c>i</c> for a
/// culture-sensitive comparison: the patterns of the tokenizer used to ignore case by the culture that was current when it was created, so <c>[I]</c> was a literal there.
/// </summary>
public class FormattedTextCultureTests
{
    private static MGTextRunConfig Settings()
        => new(false, false, 1, null, new MGTextRunUnderlineConfig(false), default, default);

    private static FTTokenizer CreateTokenizerUnder(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return new FTTokenizer();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static List<FTTokenType> TokenTypes(FTTokenizer tokenizer, string text)
    {
        var tokens = tokenizer.Tokenize(text, false);
        List<FTTokenType> types = new();
        foreach (var token in tokens)
        {
            types.Add(token.TokenType);
        }

        return types;
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void ATokenizerOfAnyCulture_ReadsAnUpperIAsTheItalicTag(string culture)
    {
        Assert.Contains(FTTokenType.ItalicOpenTagType, TokenTypes(CreateTokenizerUnder(culture), "[I]x"));
    }

    [Theory]
    [InlineData("[I]x[/I]")]
    [InlineData("[ITALIC]x[/ITALIC] y")]
    [InlineData("[IMG=Icon 16x16]")]
    [InlineData("[Image=Icon 16x16]")]
    [InlineData("[OPACITY=0.5]x")]
    [InlineData("[UNDERLINE]x")]
    [InlineData("[TOOLTIP=a]x")]
    [InlineData("[ACTION=a]x")]
    [InlineData("[b]I[/b] I")]
    public void ATokenizerOfATurkishCulture_ReadsWhatTheOneOfTheInvariantCultureReads(string text)
    {
        List<FTTokenType> expected = TokenTypes(CreateTokenizerUnder(""), text);

        Assert.Equal(expected, TokenTypes(CreateTokenizerUnder("tr-TR"), text));
        Assert.Equal(expected, TokenTypes(CreateTokenizerUnder("az-Latn-AZ"), text));
    }

    [Theory]
    [InlineData("[I]x")]
    [InlineData("[I]x[/I]")]
    [InlineData("[i]x[/I]")]
    [InlineData("[ITALIC]x[/ITALIC] y")]
    [InlineData("[Italic]x")]
    [InlineData("[IMG=Icon 16x16]")]
    [InlineData("[Image=Icon 16x16]")]
    [InlineData("[OPACITY=0.5]x")]
    [InlineData("[UNDERLINE]x")]
    [InlineData("[TOOLTIP=a]x")]
    [InlineData("[ACTION=a]x")]
    public void ATagTypeWithAnUpperI_IsReadByTheScanner(string text)
    {
        FormattedTextHarness harness = new(400, legacy: false);
        FormattedTextRuns runs = new();

        Assert.True(new FormattedTextScanner().TryScan(text.AsSpan(), Settings(), harness.Desktop, runs));
    }
}
