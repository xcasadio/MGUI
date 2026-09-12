using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using XamlDocumentSource = MGUI.Core.UI.XAML.XamlDocumentSource;
using XAMLParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Backlog task 15 (styling-theme-tasks.md): a pilot value declared as an attribute on an element of a XAML <c>ControlTemplate</c> is a value of the
/// template, recorded as <see cref="UIValueSourceKind.Template"/> and named after the template, not a value of the application. It wins over the
/// defaults of the base applicator on the same part, survives a theme refresh of the same template, and is not carried to the part of another
/// template when a theme change rebuilds the structure.
/// </summary>
public class XamlTemplatePartValuesTests
{
    private const string PaddedTemplateName = "Test.PaddedWindow";

    /// <summary>The structure of <c>Dark.Window</c> with a border thickness, a title bar padding and a title bar background declared by the XAML.</summary>
    private const string PaddedWindowXaml = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Test.PaddedWindow"" TargetType=""Window"" BasedOn=""Window.Default"">
  <ControlTemplate.DetachedRoots>
    <Border Name=""PART_Border"" BorderThickness=""3"" />
    <ResizeGrip Name=""PART_ResizeGrip"" />
    <DockPanel Name=""PART_TitleBar"" Padding=""9"" Background=""Red"">
      <Button Name=""PART_CloseButton"" Dock=""Right"" />
      <TextBlock Name=""PART_TitleBarText"" Dock=""Left"" />
    </DockPanel>
  </ControlTemplate.DetachedRoots>
  <ControlTemplate.Parts>
    <TemplatePart Name=""PART_Border"" />
    <TemplatePart Name=""PART_ResizeGrip"" />
    <TemplatePart Name=""PART_TitleBar"" />
    <TemplatePart Name=""PART_CloseButton"" />
    <TemplatePart Name=""PART_TitleBarText"" />
  </ControlTemplate.Parts>
</ControlTemplate>";

    [Fact]
    public void Part_Attributes_Are_Template_Values_That_Win_Over_The_Base_Applicator_And_Survive_A_Refresh()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(PaddedWindowXaml));
        MGTheme padded = CreateTheme(harness, PaddedTemplateName);
        MGWindow window = new(harness.Desktop, 0, 0, 300, 200, padded);
        harness.Show(window);
        Assert.Equal(PaddedTemplateName, window.AppliedControlTemplateName);
        Assert.NotEqual(new Thickness(9), padded.Window.TitleBarPadding);
        Assert.NotEqual(new Thickness(3), padded.Window.BorderThickness);

        MGDockPanel titleBar = Part<MGDockPanel>(window, MGWindow.TitleBarPartName);
        MGBorder border = Part<MGBorder>(window, MGWindow.BorderPartName);

        // The declared values are Template contributions named after the template, and they win over the Window.* defaults applied to the same parts.
        Assert.Equal(new Thickness(9), titleBar.Padding);
        AssertDeclared<Thickness>(titleBar, UIPilotProperty.Padding, UIValueSlot.Whole, "Test.PaddedWindow:PART_TitleBar.Padding");
        Assert.False(titleBar.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue, out UIResolvedValue<Thickness> _));
        Assert.Equal(Color.Red, SolidColor(titleBar.BackgroundBrush.NormalValue));
        AssertDeclared<IFillBrush>(titleBar, UIPilotProperty.Background, UIValueSlot.Normal, "Test.PaddedWindow:PART_TitleBar.Background");
        Assert.Equal(new Thickness(3), window.BorderThickness);
        AssertDeclared<Thickness>(border, UIPilotProperty.BorderThickness, UIValueSlot.Whole, "Test.PaddedWindow:PART_Border.BorderThickness");
        // The base applicator's own default is still recorded, below the declaration.
        Assert.True(border.TryGetResolvedContribution(UIPilotProperty.BorderThickness, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> themeThickness));
        Assert.Equal(padded.Window.BorderThickness, themeThickness.Value);

        // A theme refresh that keeps the template (another theme instance with the same mapping and other Window defaults) keeps the declaration.
        MGTheme refreshed = CreateTheme(harness, PaddedTemplateName);
        refreshed.Window.TitleBarPadding = new Thickness(1);
        refreshed.Window.BorderThickness = new Thickness(1);
        refreshed.TitleBackground.Value = new VisualStateFillBrush(new MGSolidFillBrush(Color.Blue));
        window.GetResources().DefaultTheme = refreshed;
        harness.Frame(3);
        Assert.Same(titleBar, Part<MGDockPanel>(window, MGWindow.TitleBarPartName));
        Assert.Equal(new Thickness(9), titleBar.Padding);
        Assert.Equal(Color.Red, SolidColor(titleBar.BackgroundBrush.NormalValue));
        Assert.Equal(new Thickness(3), window.BorderThickness);
    }

    [Fact]
    public void A_Theme_Change_To_Another_Template_Rebuilds_The_Parts_Without_The_Declared_Values()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(PaddedWindowXaml));
        MGWindow window = new(harness.Desktop, 0, 0, 300, 200, CreateTheme(harness, PaddedTemplateName));
        harness.Show(window);
        MGDockPanel declaredTitleBar = Part<MGDockPanel>(window, MGWindow.TitleBarPartName);
        Assert.Equal(new Thickness(9), declaredTitleBar.Padding);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        window.GetResources().DefaultTheme = dark;
        harness.Frame(3);

        // Dark maps Window to Dark.Window: the structure is rebuilt, and the values the replaced parts held for their template are not carried.
        Assert.Equal("Dark.Window", window.AppliedControlTemplateName);
        MGDockPanel titleBar = Part<MGDockPanel>(window, MGWindow.TitleBarPartName);
        Assert.NotSame(declaredTitleBar, titleBar);
        Assert.Equal(dark.Window.TitleBarPadding, titleBar.Padding);
        Assert.Equal(SolidColor(dark.TitleBackground.GetValue(true).NormalValue), SolidColor(titleBar.BackgroundBrush.NormalValue));
        Assert.Equal(dark.Window.BorderThickness, window.BorderThickness);
        Assert.Empty(MGElement.EnumerateTemplateContributions(titleBar, "Test.PaddedWindow:"));
        Assert.Empty(MGElement.EnumerateTemplateContributions(Part<MGBorder>(window, MGWindow.BorderPartName), "Test.PaddedWindow:"));

        // Back to the padded template: a new structure with its declaration.
        window.GetResources().DefaultTheme = CreateTheme(harness, PaddedTemplateName);
        harness.Frame(4);
        Assert.Equal(PaddedTemplateName, window.AppliedControlTemplateName);
        Assert.Equal(new Thickness(9), Part<MGDockPanel>(window, MGWindow.TitleBarPartName).Padding);
        Assert.Equal(new Thickness(3), window.BorderThickness);
    }

    [Fact]
    public void Elements_Held_By_Properties_Outside_The_Children_Are_Stamped_Too()
    {
        // The nested Border facade of a Button DTO transfers its BorderThickness; a ListViewColumn is a helper object holding its header element.
        const string NestedTemplateName = "Test.NestedWindow";
        string xaml = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                 xmlns:System=""clr-namespace:System;assembly=mscorlib""
                 Name=""Test.NestedWindow"" TargetType=""Window"" BasedOn=""Window.Default"">
  <ControlTemplate.DetachedRoots>
    <Border Name=""PART_Border"" />
    <ResizeGrip Name=""PART_ResizeGrip"" />
    <DockPanel Name=""PART_TitleBar"">
      <Button Name=""PART_CloseButton"" Dock=""Right"" BorderThickness=""2"" />
      <TextBlock Name=""PART_TitleBarText"" Dock=""Left"" />
    </DockPanel>
    <ListView Name=""PART_Extra"" ItemType=""{x:Type System:String}"">
      <ListView.Columns>
        <ListViewColumn Width=""80"">
          <ListViewColumn.Header>
            <TextBlock Text=""H"" Padding=""4"" />
          </ListViewColumn.Header>
        </ListViewColumn>
      </ListView.Columns>
    </ListView>
  </ControlTemplate.DetachedRoots>
  <ControlTemplate.Parts>
    <TemplatePart Name=""PART_Border"" />
    <TemplatePart Name=""PART_ResizeGrip"" />
    <TemplatePart Name=""PART_TitleBar"" />
    <TemplatePart Name=""PART_CloseButton"" />
    <TemplatePart Name=""PART_TitleBarText"" />
    <TemplatePart Name=""PART_Extra"" />
  </ControlTemplate.Parts>
</ControlTemplate>";
        Harness harness = Harness.Create();
        harness.Desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(xaml));
        MGWindow window = new(harness.Desktop, 0, 0, 300, 200, CreateTheme(harness, NestedTemplateName));
        harness.Show(window);
        Assert.Equal(NestedTemplateName, window.AppliedControlTemplateName);

        MGButton closeButton = Part<MGButton>(window, MGWindow.CloseButtonPartName);
        Assert.Equal(new Thickness(2), closeButton.BorderThickness);
        // The border pilots live on the button's inner border (TryGetResolvedContribution does not delegate, unlike TryGetResolvedPilotValue).
        AssertDeclared<Thickness>(closeButton.GetBorder(), UIPilotProperty.BorderThickness, UIValueSlot.Whole, "Test.NestedWindow:PART_CloseButton.BorderThickness");

        MGListView<string> listView = Part<MGListView<string>>(window, "PART_Extra");
        MGElement header = Assert.Single(listView.Columns).Header;
        Assert.Equal(new Thickness(4), header.Padding);
        AssertDeclared<Thickness>(header, UIPilotProperty.Padding, UIValueSlot.Whole, "Test.NestedWindow:TextBlock.Padding");
        Assert.False(header.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue, out UIResolvedValue<Thickness> _));
    }

    [Fact]
    public void Attributes_Outside_A_Template_Stay_Local_Values()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""300"" Height=""200"">
    <Border Name=""B"" Padding=""7"" BorderThickness=""2"" />
</Window>";
        MGWindow window = XAMLParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        MGBorder border = window.GetElementByName<MGBorder>("B");

        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.Equal(UIValueSourceKind.LocalValue, padding.Source.Kind);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> thickness));
        Assert.Equal(UIValueSourceKind.LocalValue, thickness.Source.Kind);
    }

    private static MGTheme CreateTheme(Harness harness, string windowTemplateName)
    {
        MGTheme theme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        theme.SetControlTemplateMapping(MGElementType.Window, windowTemplateName);
        return theme;
    }

    private static void AssertDeclared<T>(MGElement element, UIPilotProperty property, UIValueSlot slot, string name)
    {
        Assert.True(element.TryGetResolvedContribution(property, slot, UIValueSourceKind.Template, out UIResolvedValue<T> contribution), $"{name} is not a Template contribution");
        Assert.Equal(name, contribution.Source.Name);
        Assert.True(element.TryGetResolvedValueSource(property, slot, out UIValueResolutionSource winner));
        Assert.Equal(UIValueSourceKind.Template, winner.Kind);
    }

    private static T Part<T>(MGElement owner, string name) where T : MGElement
    {
        Assert.True(owner.TryGetTemplatePart(name, out MGElement part), $"{owner.GetType().Name} has no part {name}");
        return Assert.IsAssignableFrom<T>(part);
    }

    private static Color SolidColor(IFillBrush brush) => Assert.IsType<MGSolidFillBrush>(brush).Color;

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            Harness harness = new(runtime, desktop);
            harness.Frame(0);
            return harness;
        }

        public void Show(MGWindow window)
        {
            if (!Desktop.Windows.Contains(window))
                Desktop.Windows.Add(window);
            Frame(1);
            Frame(2);
        }

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
