using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Backlog task 11 (styling-theme-tasks.md): the remaining visual values of <see cref="MGTextBox"/> (content alignments, margin, font size and corner of the
/// character count, markup of its texts) come from the <c>TextBox.Default</c> control template, for the text box and for the controls deriving from it,
/// while local values set by the application outlive a theme change.
/// </summary>
public class TextBoxChromeTemplateTests
{
    private const string LimitedFormat = "[b]{{CharacterCount}}[/b] / [b]{{CharacterLimit}}[/b]";
    private const string LimitlessFormat = "[b]{{CharacterCount}}[/b] character(s)";

    [Fact]
    public void The_Character_Count_Chrome_Comes_From_The_Template()
    {
        Harness harness = Harness.Create();
        MGTextBox textBox = new(harness.Window, 100, ShowCharacterCount: true);
        harness.Show(textBox);

        MGTextBlock counter = textBox.CharacterCountComponent.Element;
        Assert.Equal(new Thickness(0, 0, 8, 4), counter.Margin);
        Assert.True(counter.TryGetResolvedValueSource(UIPilotProperty.Margin, UIValueSlot.Whole, out UIValueResolutionSource marginSource));
        Assert.Equal(UIValueSourceKind.Template, marginSource.Kind);
        Assert.Equal(9, counter.FontSize);
        Assert.Equal(HorizontalAlignment.Right, counter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, counter.VerticalAlignment);
        AssertInQuadrant(counter, textBox, right: true, bottom: true);
    }

    [Fact]
    public void The_Character_Count_Is_Laid_Out_With_The_Alignments_Of_Its_Part()
    {
        Harness harness = Harness.Create();
        MGTextBox textBox = new(harness.Window, 100, ShowCharacterCount: true);
        harness.Show(textBox);
        MGTextBlock counter = textBox.CharacterCountComponent.Element;

        counter.HorizontalAlignment = HorizontalAlignment.Left;
        counter.VerticalAlignment = VerticalAlignment.Top;
        harness.Frame(2);

        AssertInQuadrant(counter, textBox, right: false, bottom: false);
    }

    [Fact]
    public void Content_Alignments_And_Count_Formats_Are_Template_Defaults_That_Local_Values_Outlive()
    {
        Harness harness = Harness.Create();
        MGTextBox limited = new(harness.Window, 100, ShowCharacterCount: true);
        MGTextBox limitless = new(harness.Window, null, ShowCharacterCount: true);
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(limited);
        panel.TryAddChild(limitless);
        harness.Show(panel);

        Assert.Equal(HorizontalAlignment.Left, limited.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, limited.VerticalContentAlignment);
        Assert.Equal(LimitedFormat, limited.LimitedCharacterCountFormatString);
        Assert.Equal(LimitlessFormat, limited.LimitlessCharacterCountFormatString);

        limitless.HorizontalContentAlignment = HorizontalAlignment.Center;
        limitless.LimitlessCharacterCountFormatString = "{{CharacterCount}} chars";
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);

        Assert.Equal(HorizontalAlignment.Left, limited.HorizontalContentAlignment);
        Assert.Equal(LimitedFormat, limited.LimitedCharacterCountFormatString);
        Assert.Equal(HorizontalAlignment.Center, limitless.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, limitless.VerticalContentAlignment);
        Assert.Equal("{{CharacterCount}} chars", limitless.LimitlessCharacterCountFormatString);

        limited.Text = "abc";
        limitless.Text = "abcd";
        Assert.Equal("[b]3[/b] / [b]100[/b]", limited.CharacterCountComponent.Element.Text);
        Assert.Equal("4 chars", limitless.CharacterCountComponent.Element.Text);
    }

    [Fact]
    public void Password_Boxes_Numeric_Up_Downs_And_Rich_Text_Boxes_Keep_The_Text_Box_Chrome()
    {
        Harness harness = Harness.Create();
        MGPasswordBox passwordBox = new(harness.Window);
        MGNumericUpDown numericUpDown = new(harness.Window);
        MGRichTextBox richTextBox = new(harness.Window);
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(passwordBox);
        panel.TryAddChild(numericUpDown);
        panel.TryAddChild(richTextBox);
        harness.Show(panel);

        foreach (MGTextBox textBox in new MGTextBox[] { passwordBox, numericUpDown, richTextBox })
        {
            Assert.Null(textBox.LastControlTemplateError);
            Assert.Equal(HorizontalAlignment.Left, textBox.HorizontalContentAlignment);
            Assert.Equal(LimitedFormat, textBox.LimitedCharacterCountFormatString);
            MGTextBlock counter = textBox.CharacterCountComponent.Element;
            Assert.Equal(new Thickness(0, 0, 8, 4), counter.Margin);
            Assert.Equal(9, counter.FontSize);
            Assert.Equal(HorizontalAlignment.Right, counter.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Bottom, counter.VerticalAlignment);
        }

        // The rich text box aligns its content to the top once its template applied, and keeps it through a theme change.
        Assert.Equal(VerticalAlignment.Center, passwordBox.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, numericUpDown.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Top, richTextBox.VerticalContentAlignment);
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        Assert.Equal(VerticalAlignment.Top, richTextBox.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, passwordBox.VerticalContentAlignment);
    }

    private static void AssertInQuadrant(MGElement part, MGElement owner, bool right, bool bottom)
    {
        Point partCenter = part.LayoutBounds.Center;
        Point ownerCenter = owner.LayoutBounds.Center;
        Assert.True(right ? partCenter.X > ownerCenter.X : partCenter.X < ownerCenter.X, $"{part.LayoutBounds} in {owner.LayoutBounds}");
        Assert.True(bottom ? partCenter.Y > ownerCenter.Y : partCenter.Y < ownerCenter.Y, $"{part.LayoutBounds} in {owner.LayoutBounds}");
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0);
            return harness;
        }

        /// <summary>Sets the window content, shows the window and runs two frames so the layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
            {
                Desktop.Windows.Add(Window);
            }

            Frame(0);
            Frame(1);
        }

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
