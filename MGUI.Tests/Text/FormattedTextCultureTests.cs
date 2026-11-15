using System.Globalization;
using MGUI.Core.UI.Text;

namespace MGUI.Tests.Text;

/// <summary>
/// The legacy tokenizer ignores the case of a tag type by the rules of the culture that was current when it was created. Under a Turkish or an Azerbaijani culture an uppercase <c>I</c> is not
/// the letter <c>i</c>: <c>[I]</c> is a literal. <see cref="FormattedTextScanner"/> folds case by ASCII, so it has to hand such a text to the legacy code.
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

    [Theory]
    [InlineData("", true)]
    [InlineData("en-US", true)]
    [InlineData("fr-FR", true)]
    [InlineData("tr-TR", false)]
    [InlineData("az-Latn-AZ", false)]
    public void LegacyFoldsUpperI_AnswersWhatTheTokenizerOfTheCultureDoes(string culture, bool expected)
    {
        Assert.Equal(expected, FormattedTextScanner.LegacyFoldsUpperI(CreateTokenizerUnder(culture)));
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
    public void ATagTypeWithAnUpperI_IsLeftToTheLegacyCode_WhenItsTokenizerDoesNotFoldIt(string text)
    {
        FormattedTextHarness harness = new(400, legacy: false);
        FormattedTextRuns runs = new();

        Assert.False(new FormattedTextScanner(FoldsUpperI: false).TryScan(text.AsSpan(), Settings(), harness.Desktop, runs));
        Assert.True(new FormattedTextScanner(FoldsUpperI: true).TryScan(text.AsSpan(), Settings(), harness.Desktop, runs));
    }

    [Theory]
    [InlineData("[i]x[/i]")]
    [InlineData("[italic]x[/italic]")]
    [InlineData("[img=Icon 16x16]")]
    [InlineData("[b]I[/b] I")]
    [InlineData("[BOLD]x[/BOLD]")]
    [InlineData("[tooltip=I]x[/tooltip]")]
    [InlineData("[color=Indigo]x")]
    public void ATextWithoutAnUpperIInATagType_IsStillRead_WhenTheTokenizerDoesNotFoldIt(string text)
    {
        FormattedTextHarness harness = new(400, legacy: false);
        FormattedTextRuns runs = new();

        Assert.True(new FormattedTextScanner(FoldsUpperI: false).TryScan(text.AsSpan(), Settings(), harness.Desktop, runs));
    }
}
