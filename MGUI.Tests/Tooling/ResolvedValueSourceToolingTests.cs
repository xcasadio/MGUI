using System;
using System.Linq;
using System.Runtime.Serialization;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Tooling;

/// <summary>
/// Backlog task 5 (styling-theme-tasks.md): the public, property-path based diagnostic
/// <see cref="UIToolingService.TryGetResolvedValueSource"/>, built on the internal store read
/// <c>MGElement.TryGetResolvedValueSource(UIPilotProperty, UIValueSlot, ...)</c> delivered by ADR-0005/S9.
/// </summary>
public class ResolvedValueSourceToolingTests
{
    private static readonly UIInvalidationKind MeasureArrange = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

    [Fact]
    public void A_Template_Part_Value_Reports_The_Template_Source_And_Its_Invalidation()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 40, 40, 300, 200);
        Assert.True(window.TryGetTemplatePart(MGWindow.TitleBarPartName, out MGElement titleBar));

        Assert.True(UIToolingService.TryGetResolvedValueSource(titleBar, "Padding", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Template, source.Kind);
        Assert.Equal(UIValuePrecedence.Template, source.Precedence);
        Assert.Equal(MeasureArrange, source.Invalidation);
    }

    [Fact]
    public void A_Control_Theme_Default_Reports_Theme_And_A_Local_Write_Reports_LocalValue()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 40, 40, 300, 200);

        Assert.True(UIToolingService.TryGetResolvedValueSource(window, "Padding", out UIValueResolutionSource themeSource));
        Assert.Equal(UIValueSourceKind.Theme, themeSource.Kind);

        window.Padding = new Thickness(9);
        Assert.True(UIToolingService.TryGetResolvedValueSource(window, "Padding", out UIValueResolutionSource localSource));
        Assert.Equal(UIValueSourceKind.LocalValue, localSource.Kind);
        Assert.Equal(MeasureArrange, localSource.Invalidation);
    }

    [Fact]
    public void Xaml_Property_Names_Report_The_Same_Source_As_The_Clr_Paths_They_Target()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <StackPanel Orientation=""Vertical"">
        <Border Name=""B"" Background=""Red"" TextForeground=""Yellow"" />
        <TextBlock Name=""T"" Text=""x"" Foreground=""Blue"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        MGBorder border = window.GetElementByName<MGBorder>("B");
        MGTextBlock textBlock = window.GetElementByName<MGTextBlock>("T");

        AssertSameSource(border, "Background", "BackgroundBrush.NormalValue");
        AssertSameSource(border, "TextForeground", "DefaultTextForeground.NormalValue");
        AssertSameSource(textBlock, "Foreground", "Foreground.NormalValue");

        static void AssertSameSource(MGElement element, string xamlName, string clrPath)
        {
            Assert.True(UIToolingService.TryGetResolvedValueSource(element, xamlName, out UIValueResolutionSource fromXamlName));
            Assert.True(UIToolingService.TryGetResolvedValueSource(element, clrPath, out UIValueResolutionSource fromClrPath));
            Assert.Equal(UIValueSourceKind.LocalValue, fromXamlName.Kind);
            Assert.Equal(fromClrPath, fromXamlName);
        }
    }

    [Fact]
    public void The_Result_Is_The_Store_Winner_Of_The_Targeted_Pilot_Slot()
    {
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        button.SetBackgroundSlot(UIValueSlot.Selected, new MGSolidFillBrush(Color.Green), UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        AssertMatchesStore(button, "Padding", button, UIPilotProperty.Padding, UIValueSlot.Whole);
        AssertMatchesStore(button, "BorderBrush", button.GetBorder(), UIPilotProperty.BorderBrush, UIValueSlot.Whole);
        AssertMatchesStore(button, "BorderThickness", button.GetBorder(), UIPilotProperty.BorderThickness, UIValueSlot.Whole);
        AssertMatchesStore(button, "BackgroundBrush", button, UIPilotProperty.Background, UIValueSlot.Whole);
        AssertMatchesStore(button, "BackgroundBrush.SelectedValue", button, UIPilotProperty.Background, UIValueSlot.Selected);
        AssertMatchesStore(button, "SelectedBackground", button, UIPilotProperty.Background, UIValueSlot.Selected);
        AssertMatchesStore(button, "DefaultTextForeground", button, UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole);
        // No foreground of its own: the text block reports the read-time Theme fall-back, exactly like the store read.
        AssertMatchesStore(textBlock, "Foreground", textBlock, UIPilotProperty.Foreground, UIValueSlot.Normal);

        static void AssertMatchesStore(MGElement element, string path, MGElement owner, UIPilotProperty property, UIValueSlot slot)
        {
            Assert.True(owner.TryGetResolvedValueSource(property, slot, out UIValueResolutionSource expected));
            Assert.True(UIToolingService.TryGetResolvedValueSource(element, path, out UIValueResolutionSource actual));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void The_Covered_Subset_Is_Exactly_The_Pilot_Paths_And_Their_Xaml_Names()
    {
        Assert.Equal(new[]
        {
            "Margin", "Padding", "MinHeight", "BorderBrush", "BorderThickness",
            "BackgroundBrush", "BackgroundBrush.NormalValue", "BackgroundBrush.SelectedValue", "BackgroundBrush.DisabledValue", "BackgroundBrush.FocusedValue", "BackgroundBrush.FocusedColor",
            "DefaultTextForeground", "DefaultTextForeground.NormalValue", "DefaultTextForeground.SelectedValue", "DefaultTextForeground.DisabledValue", "DefaultTextForeground.FocusedValue",
            "Foreground.NormalValue", "Foreground.SelectedValue", "Foreground.DisabledValue", "Foreground.FocusedValue",
            "Background", "SelectedBackground", "DisabledBackground", "TextForeground", "SelectedTextForeground", "DisabledTextForeground", "Foreground",
        }, UIToolingService.ResolvedValueSourcePropertyPaths);

        // Every listed path names a pilot (key, slot) that the store read understands, on a suitable element.
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        foreach (string path in UIToolingService.ResolvedValueSourcePropertyPaths)
        {
            MGElement target = path.StartsWith("Foreground", StringComparison.Ordinal) ? textBlock : button;
            string clrPath = MGUI.Core.UI.XAML.Element.MapBindingTargetPath(path);
            Assert.True(UIPilotPropertyResolver.TryResolve(target, clrPath, out _, out _, out _), path);
        }
    }

    [Fact]
    public void Properties_Outside_The_Pilot_Subset_Are_Not_Covered_And_The_Read_Never_Throws()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);

        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "Opacity", out UIValueResolutionSource source));
        Assert.Equal(default, source);
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "Width", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "Visibility", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "Foreground", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "Padding.Left", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, "BackgroundBrush.NormalValue.A", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(border, " ", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(null, "Padding", out _));

        MGElement uninitialized = (MGElement)FormatterServices.GetUninitializedObject(typeof(MGBorder));
        Assert.False(UIToolingService.TryGetResolvedValueSource(uninitialized, "BorderBrush", out _));
        Assert.False(UIToolingService.TryGetResolvedValueSource(uninitialized, "Background", out _));
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

        public void Frame(int frameIndex, Point mousePosition)
        {
            MouseState mouse = new(mousePosition.X, mousePosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
