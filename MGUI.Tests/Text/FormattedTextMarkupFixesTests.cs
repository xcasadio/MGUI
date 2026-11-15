using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

/// <summary>
/// The markup defects of the legacy pipeline that were fixed after the allocation-free scanner had reproduced them: the offset of a shadow written with a comma, and a closing tag with no
/// opening one. Each case is read by the legacy pipeline (<see cref="MGTextBlock.ForceLegacyTextPath"/>) and by the scanner.
/// </summary>
public class FormattedTextMarkupFixesTests
{
    private static MGTextRunConfig FirstSettings(bool legacy, string text, Action<MGTextBlock> configure = null)
    {
        FormattedTextHarness harness = new(300, legacy, configure: configure);
        harness.TextBlock.Text = text;
        Assert.Equal(!legacy, harness.TextBlock.AreFormattedRunsKnown);
        return ((MGTextRunText)harness.TextBlock.Runs[0]).Settings;
    }

    [Theory]
    [InlineData("[shadow=Red 1,2]x[/shadow]", 1, 2)]
    [InlineData("[shadow=Red 1 2]x[/shadow]", 1, 2)]
    [InlineData("[s=Red -12,-34]x[/s]", -12, -34)]
    [InlineData("[shadow=#00000080 3,4]x[/shadow]", 3, 4)]
    [InlineData("[shadow=Red]x[/shadow]", 1, 1)]
    public void TheOffsetOfAShadow_IsReadWithASpaceOrAComma(string text, int x, int y)
    {
        foreach (bool legacy in new[] { true, false })
        {
            MGTextRunConfig settings = FirstSettings(legacy, text);
            Assert.True(settings.IsShadowed);
            Assert.Equal(new Vector2(x, y), settings.Shadow.ShadowOffset);
        }
    }

    [Fact]
    public void AClosingTagWithNoOpeningOne_KeepsTheSettingsOfTheBlock()
    {
        void Configure(MGTextBlock block)
        {
            block.IsUnderlined = true;
            block.IsShadowed = true;
            block.ShadowColor = Color.Lime;
            block.ShadowOffset = new Point(3, 3);
        }

        foreach (bool legacy in new[] { true, false })
        {
            MGTextRunConfig expected = FirstSettings(legacy, "x", Configure);
            Assert.True(expected.Underline.IsEnabled);
            Assert.True(expected.IsShadowed);

            foreach (string text in new[] { "[/u]x", "[/underline]x", "[/shadow]x", "[/s]x", "[/o]x", "[/color]x", "[/bg]x", "[/u][/u][/shadow]x" })
            {
                Assert.Equal(expected, FirstSettings(legacy, text, Configure));
            }
        }
    }

    [Fact]
    public void AClosingTag_StillRevertsToTheValueBeforeItsOpeningOne()
    {
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy, configure: block => block.IsUnderlined = true);
            harness.TextBlock.Text = "[u=3]a[/u]b[/u]c";
            List<MGTextRunConfig> settings = harness.TextBlock.Runs.OfType<MGTextRunText>().Select(x => x.Settings).ToList();
            Assert.Equal(3, settings.Count);
            Assert.Equal(3, settings[0].Underline.Height);
            //  The first closing tag gives back the underline of the block; the second one, which closes nothing, changes nothing
            Assert.Equal(settings[1], settings[2]);
            Assert.True(settings[1].Underline.IsEnabled);
            Assert.Equal(1, settings[1].Underline.Height);
        }
    }
}
