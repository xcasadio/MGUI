using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S9 diagnostic read (ADR-0005, base of backlog task 5):
/// <see cref="MGElement.TryGetResolvedValueSource"/> (type-agnostic report of the current winner's source) and
/// <see cref="MGElement.EnumerateResolvedContributions"/> (raw store contents, no read-time fall-back), plus the
/// two XAML provenance gaps this slice closes in <c>Controls.cs</c> (<c>GroupBox</c>'s <c>BorderBrush</c>/
/// <c>BorderThickness</c> transfer and <c>Window</c>'s post-<c>WindowStyle</c> <c>Padding</c> re-application).
/// </summary>
public class ResolvedValueSourceDiagnosticsTests
{
    private static readonly UIInvalidationKind MeasureArrange = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

    /// <summary>Test-only <see cref="MGElement"/> subclass with no pilot writes of its own beyond the base
    /// constructor's Margin/Padding/Background/DefaultTextForeground defaults.</summary>
    private sealed class DiagnosticsProbeElement : MGElement
    {
        public DiagnosticsProbeElement(MGWindow window)
            : base(window, MGElementType.Custom)
        {
        }
    }

    /// <summary>(1) Pins the eight <see cref="UIPilotProperty"/> keys (in order -- the store indexes by ordinal)
    /// carrying the seven ADR-0005 pilots, and the six <see cref="UIValueSlot"/> members.</summary>
    [Fact]
    public void Pilot_Keys_Are_Exactly_The_Seven_Pilots_Carried_By_Eight_Keys()
    {
        UIPilotProperty[] properties = (UIPilotProperty[])Enum.GetValues(typeof(UIPilotProperty));
        Assert.Equal(new[]
        {
            UIPilotProperty.Margin,
            UIPilotProperty.Padding,
            UIPilotProperty.MinHeight,
            UIPilotProperty.BorderBrush,
            UIPilotProperty.BorderThickness,
            UIPilotProperty.Background,
            UIPilotProperty.Foreground,
            UIPilotProperty.DefaultTextForeground,
        }, properties);

        UIValueSlot[] slots = (UIValueSlot[])Enum.GetValues(typeof(UIValueSlot));
        Assert.Equal(new[]
        {
            UIValueSlot.Whole,
            UIValueSlot.Normal,
            UIValueSlot.Selected,
            UIValueSlot.Disabled,
            UIValueSlot.Focused,
            UIValueSlot.FocusedColor,
        }, slots);
    }

    /// <summary>(2) Pins the property -&gt; <see cref="UIInvalidationKind"/> map that backlog task 7 will reuse:
    /// Margin, Padding, MinHeight, BorderThickness carry Measure|Arrange; BorderBrush, Background (Whole),
    /// Foreground (Whole), DefaultTextForeground (Whole) carry Draw. Read back both through
    /// <see cref="MGElement.EnumerateResolvedContributions"/> and <see cref="MGElement.TryGetResolvedValueSource"/>
    /// on real templated elements of each family.</summary>
    [Fact]
    public void Framework_Writes_Carry_The_Documented_Invalidation_Per_Key()
    {
        Harness harness = Harness.Create();

        DiagnosticsProbeElement element = new(harness.Window);
        AssertInvalidation(element, UIPilotProperty.Margin, MeasureArrange);
        AssertInvalidation(element, UIPilotProperty.Padding, MeasureArrange);
        AssertInvalidation(element, UIPilotProperty.Background, UIInvalidationKind.Draw);
        AssertInvalidation(element, UIPilotProperty.DefaultTextForeground, UIInvalidationKind.Draw);

        // MinHeight has no constructor default (ResolvedScalarPilotsTests): tag a write to observe its invalidation.
        element.SetMinHeight(10, UIValueResolutionSource.Theme(MeasureArrange));
        AssertInvalidation(element, UIPilotProperty.MinHeight, MeasureArrange);

        MGBorder border = new(harness.Window);
        AssertInvalidation(border, UIPilotProperty.BorderBrush, UIInvalidationKind.Draw);
        AssertInvalidation(border, UIPilotProperty.BorderThickness, MeasureArrange);

        MGTextBlock textBlock = new(harness.Window, "x");
        AssertInvalidation(textBlock, UIPilotProperty.Foreground, UIInvalidationKind.Draw);

        MGButton button = new(harness.Window);
        AssertInvalidation(button, UIPilotProperty.BorderBrush, UIInvalidationKind.Draw);

        static void AssertInvalidation(MGElement owner, UIPilotProperty property, UIInvalidationKind expected)
        {
            IReadOnlyList<UIResolvedContribution> contributions = owner.EnumerateResolvedContributions(property, UIValueSlot.Whole);
            Assert.NotEmpty(contributions);
            Assert.Equal(expected, contributions[0].Source.Invalidation);

            Assert.True(owner.TryGetResolvedValueSource(property, UIValueSlot.Whole, out UIValueResolutionSource source));
            Assert.Equal(expected, source.Invalidation);
        }
    }

    /// <summary>(3) For every (key, slot) pair the framework writes, <see cref="MGElement.TryGetResolvedValueSource"/>
    /// reports the same <see cref="UIValueResolutionSource"/> as the typed <see cref="MGElement.TryGetResolvedPilotValue{T}"/>;
    /// for never-written pairs it returns false.</summary>
    [Fact]
    public void TryGetResolvedValueSource_Reports_The_Same_Source_As_The_Typed_Read()
    {
        Harness harness = Harness.Create();
        DiagnosticsProbeElement element = new(harness.Window);
        MGBorder border = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        harness.Show(element);
        harness.Show(border);
        harness.Show(textBlock);

        // Written pairs.
        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> margin));
        Assert.True(element.TryGetResolvedValueSource(UIPilotProperty.Margin, UIValueSlot.Whole, out UIValueResolutionSource marginSource));
        Assert.Equal(margin.Source, marginSource);

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.True(element.TryGetResolvedValueSource(UIPilotProperty.Padding, UIValueSlot.Whole, out UIValueResolutionSource paddingSource));
        Assert.Equal(padding.Source, paddingSource);

        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> borderBrush));
        Assert.True(border.TryGetResolvedValueSource(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIValueResolutionSource borderBrushSource));
        Assert.Equal(borderBrush.Source, borderBrushSource);

        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> borderThickness));
        Assert.True(border.TryGetResolvedValueSource(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIValueResolutionSource borderThicknessSource));
        Assert.Equal(borderThickness.Source, borderThicknessSource);

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> backgroundWhole));
        Assert.True(element.TryGetResolvedValueSource(UIPilotProperty.Background, UIValueSlot.Whole, out UIValueResolutionSource backgroundWholeSource));
        Assert.Equal(backgroundWhole.Source, backgroundWholeSource);

        element.SetBackgroundSlot(UIValueSlot.Normal, MGUniformBorderBrush.Black.Brush, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> backgroundNormal));
        Assert.True(element.TryGetResolvedValueSource(UIPilotProperty.Background, UIValueSlot.Normal, out UIValueResolutionSource backgroundNormalSource));
        Assert.Equal(backgroundNormal.Source, backgroundNormalSource);

        Assert.True(textBlock.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Whole, out UIResolvedValue<VisualStateSetting<Color?>> foregroundWhole));
        Assert.True(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.Whole, out UIValueResolutionSource foregroundWholeSource));
        Assert.Equal(foregroundWhole.Source, foregroundWholeSource);

        textBlock.Foreground.NormalValue = Color.Blue; // non-tagged sub-field write, attributed LocalValue
        Assert.True(textBlock.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> foregroundNormal));
        Assert.True(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIValueResolutionSource foregroundNormalSource));
        Assert.Equal(foregroundNormal.Source, foregroundNormalSource);

        // Never-written pairs.
        Assert.False(element.TryGetResolvedValueSource(UIPilotProperty.Margin, UIValueSlot.Normal, out _));
        Assert.False(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.FocusedColor, out _));
        Assert.False(element.TryGetResolvedValueSource(UIPilotProperty.DefaultTextForeground, UIValueSlot.FocusedColor, out _));
        Assert.False(element.TryGetResolvedValueSource(UIPilotProperty.Padding, UIValueSlot.Selected, out _));
    }

    /// <summary>(4) Mirrors the S6 <c>ResolvedTextForegroundPilotTests.Local_Beats_Inherited_Beats_Theme_End_To_End</c>
    /// setup: an <see cref="MGTextBlock"/> without its own foreground inside a parent carrying a
    /// <see cref="MGElement.DefaultTextForeground"/> reports <c>Inherited</c>; with nothing set anywhere it reports
    /// <c>Theme</c>; with an own local foreground it reports <c>LocalValue</c>.</summary>
    [Fact]
    public void TryGetResolvedValueSource_Honors_MGTextBlock_Inherited_And_Theme_Fallbacks()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        border.SetContent(textBlock);
        harness.Show(border);

        Assert.True(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIValueResolutionSource themeSource));
        Assert.Equal(UIValueSourceKind.Theme, themeSource.Kind);

        border.DefaultTextForeground.NormalValue = Color.Red;
        Assert.True(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIValueResolutionSource inheritedSource));
        Assert.Equal(UIValueSourceKind.Inherited, inheritedSource.Kind);

        textBlock.Foreground.NormalValue = Color.Blue;
        Assert.True(textBlock.TryGetResolvedValueSource(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIValueResolutionSource localSource));
        Assert.Equal(UIValueSourceKind.LocalValue, localSource.Kind);
    }

    /// <summary>(5) <see cref="MGElement.EnumerateResolvedContributions"/> lists every contribution highest
    /// precedence first, boxed, with no read-time fall-back; on a composite it delegates to the inner border like
    /// <see cref="MGElement.TryGetResolvedPilotValue{T}"/> does; on an uninitialized element it returns false/empty
    /// without throwing.</summary>
    [Fact]
    public void EnumerateResolvedContributions_Lists_All_Contributions_Highest_Precedence_First_And_Delegates_To_The_Border()
    {
        Harness harness = Harness.Create();
        DiagnosticsProbeElement element = new(harness.Window);
        harness.Show(element);

        // The constructor already recorded DefaultValue(0) on Padding; add Theme(20) then LocalValue(90):
        // LocalValue must win and come first, Theme second, the constructor's DefaultValue last.
        element.SetPadding(new Thickness(5), UIValueResolutionSource.Theme(MeasureArrange));
        element.Padding = new Thickness(10); // public setter => LocalValue

        IReadOnlyList<UIResolvedContribution> paddingContributions = element.EnumerateResolvedContributions(UIPilotProperty.Padding, UIValueSlot.Whole);
        Assert.Equal(3, paddingContributions.Count);
        Assert.Equal(UIValueSourceKind.LocalValue, paddingContributions[0].Kind);
        Assert.Equal(new Thickness(10), paddingContributions[0].Value);
        Assert.Equal(UIValueSourceKind.Theme, paddingContributions[1].Kind);
        Assert.Equal(new Thickness(5), paddingContributions[1].Value);

        MGButton button = new(harness.Window);
        harness.Show(button);
        MGBorder buttonBorder = button.GetBorder();
        Assert.NotNull(buttonBorder);

        IReadOnlyList<UIResolvedContribution> fromComposite = button.EnumerateResolvedContributions(UIPilotProperty.BorderBrush, UIValueSlot.Whole);
        IReadOnlyList<UIResolvedContribution> fromBorder = buttonBorder.EnumerateResolvedContributions(UIPilotProperty.BorderBrush, UIValueSlot.Whole);
        Assert.Equal(fromBorder.Count, fromComposite.Count);
        for (int i = 0; i < fromBorder.Count; i++)
        {
            Assert.Equal(fromBorder[i].Kind, fromComposite[i].Kind);
        }

        MGElement uninitialized = (MGElement)FormatterServices.GetUninitializedObject(typeof(DiagnosticsProbeElement));
        Assert.False(uninitialized.TryGetResolvedValueSource(UIPilotProperty.Margin, UIValueSlot.Whole, out _));
        Assert.Empty(uninitialized.EnumerateResolvedContributions(UIPilotProperty.Margin, UIValueSlot.Whole));
        Assert.Empty(uninitialized.EnumerateResolvedContributions(UIPilotProperty.BorderBrush, UIValueSlot.Whole));
    }

    /// <summary>(6) The S9 GroupBox fix: an implicit style setting <c>BorderBrush</c>/<c>BorderThickness</c>
    /// resolves as <see cref="UIValueSourceKind.ImplicitStyle"/> (closes the S7 gap where the DTO used a direct
    /// facade assignment, always LocalValue); a direct attribute resolves as <see cref="UIValueSourceKind.LocalValue"/>.</summary>
    [Fact]
    public void GroupBox_BorderBrush_And_BorderThickness_From_An_Implicit_Style_Resolve_As_ImplicitStyle()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""GroupBox"">
            <Setter Property=""BorderBrush"" Value=""Purple"" />
            <Setter Property=""BorderThickness"" Value=""3"" />
        </Style>
    </Window.Styles>
    <StackPanel Orientation=""Vertical"">
        <GroupBox Name=""Styled"" />
        <GroupBox Name=""Local"" BorderBrush=""Orange"" BorderThickness=""4"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGGroupBox styled = window.GetElementByName<MGGroupBox>("Styled");
        Assert.True(styled.TryGetResolvedValueSource(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIValueResolutionSource styledBrushSource));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledBrushSource.Kind);
        Assert.True(styled.TryGetResolvedValueSource(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIValueResolutionSource styledThicknessSource));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledThicknessSource.Kind);
        Assert.Equal(new Thickness(3), styled.BorderThickness);

        MGGroupBox local = window.GetElementByName<MGGroupBox>("Local");
        Assert.True(local.TryGetResolvedValueSource(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIValueResolutionSource localBrushSource));
        Assert.Equal(UIValueSourceKind.LocalValue, localBrushSource.Kind);
        Assert.True(local.TryGetResolvedValueSource(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIValueResolutionSource localThicknessSource));
        Assert.Equal(UIValueSourceKind.LocalValue, localThicknessSource.Kind);
        Assert.Equal(new Thickness(4), local.BorderThickness);
    }

    /// <summary>(7) The S9 Window fix: <c>Window.Padding</c>'s post-<c>WindowStyle</c> re-application now goes
    /// through <c>ResolveXamlSource</c> instead of a direct facade assignment, so a styled Padding resolves as its
    /// XAML provenance instead of always <see cref="UIValueSourceKind.LocalValue"/> (which is what the pre-S9 code
    /// produced -- this test fails on that code and passes after the fix). A direct attribute still resolves as
    /// <see cref="UIValueSourceKind.LocalValue"/>, and the <c>WindowStyle</c> rewrite must not win over it.</summary>
    [Fact]
    public void Window_Padding_ReApplied_After_WindowStyle_Resolves_As_Its_Xaml_Provenance()
    {
        Harness harness = Harness.Create();
        string styledXaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" WindowStyle=""None"">
    <Window.Styles>
        <Style TargetType=""Window"">
            <Setter Property=""Padding"" Value=""6"" />
        </Style>
    </Window.Styles>
    <TextBlock Text=""x"" />
</Window>";
        MGWindow styledWindow = MGUIXamlParser.LoadRootWindow(harness.Desktop, styledXaml, false, true);
        harness.Desktop.Windows.Add(styledWindow);
        harness.Desktop.Update();

        Assert.True(styledWindow.TryGetResolvedValueSource(UIPilotProperty.Padding, UIValueSlot.Whole, out UIValueResolutionSource styledSource));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledSource.Kind);
        Assert.Equal(new Thickness(6), styledWindow.Padding);

        string directXaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" WindowStyle=""None"" Padding=""7"">
    <TextBlock Text=""y"" />
</Window>";
        MGWindow directWindow = MGUIXamlParser.LoadRootWindow(harness.Desktop, directXaml, false, true);
        harness.Desktop.Windows.Add(directWindow);
        harness.Desktop.Update();

        Assert.True(directWindow.TryGetResolvedValueSource(UIPilotProperty.Padding, UIValueSlot.Whole, out UIValueResolutionSource directSource));
        Assert.Equal(UIValueSourceKind.LocalValue, directSource.Kind);
        Assert.Equal(new Thickness(7), directWindow.Padding);
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
            harness.Frame(0, Point.Zero);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
                Desktop.Windows.Add(Window);
            Frame(0, Point.Zero);
            Frame(1, Point.Zero);
        }

        public void Frame(int frameIndex, Point mousePosition)
        {
            MouseState mouse = new(mousePosition.X, mousePosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
