using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features;

/// <summary>
/// Backlog task 13 (<c>Docs/Tasks/styling-theme-tasks.md</c>), scenario <c>SCN-THEME-001</c>: the "Editor Compact" preset, a declarative theme loaded from
/// <c>EditorCompact.Themes.xaml</c> into this window's resource scope. Switching between Dark and the preset only changes the default theme of that scope.
/// Task 14 made the densities the preset could not reach (tooltip, text boxes, item paddings, tab headers, docking sizes) theme settings.
/// </summary>
public sealed class EditorCompactPresetSample : SampleBase
{
    private const string PresetResourceName = "MGUI.Samples.Features.EditorCompact.Themes.xaml";
    private const string PresetThemeName = "EditorCompact";

    private const string LimitsText = @"Reached by the preset (declarative theme settings, followed by a theme change):
- font sizes (FontSettings), window title bar and close button, overlay and context menu paddings and margins
- combo box padding, minimum height, dropdown padding, item spacing and item padding; list box minimum height, title padding, item and content paddings
- tree view panel padding and spacing, tab control header spacing and header paddings, property grid spacings and row padding, check box size, tree indent
- since task 14: tooltip padding, border and minimum size (ToolTip group); text box and numeric up/down padding, minimum height and spinner widths
- since task 14: docking tab header height, tab and drawer buttons, tab title padding, drawer header height, auto-hide strip thickness, drop zone size

Not reachable declaratively (template values or control constants):
- TabControl header border thicknesses (they encode the docked edge), tab group compact buttons 24, DockTabGroupNode minimum-height heuristic 30
- Drag detection bands (host edge 40, proximity 30), drop zone glyphs 24, auto-hide strip button sizes 60/20 and font 11
- XAML control templates carry no template values: a theme can only map a control to another template
- Implicit styles reach the pilot properties when XAML is parsed or through RefreshStyles, never through a theme change; docking controls are MGElementType.Custom";

    public EditorCompactPresetSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "EditorCompactPreset.xaml")
    {
        ApplyScenarioId("SCN-THEME-001");

        string presetXaml = GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), PresetResourceName);
        MGTheme compactTheme = Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromString(presetXaml, PresetResourceName))[PresetThemeName];
        MGTheme darkTheme = new(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily);

        MGTextBlock activeThemeText = Window.GetElementByName<MGTextBlock>("ActiveThemeText");
        MGButton applyDarkButton = Window.GetElementByName<MGButton>("ApplyDarkButton");
        MGButton applyCompactButton = Window.GetElementByName<MGButton>("ApplyCompactButton");
        MGComboBox<string> sampleComboBox = Window.GetElementByName<MGComboBox<string>>("SampleComboBox");
        MGListBox<string> sampleListBox = Window.GetElementByName<MGListBox<string>>("SampleListBox");
        MGButton openContextMenuButton = Window.GetElementByName<MGButton>("OpenContextMenuButton");
        MGContextMenu sampleContextMenu = Window.GetElementByName<MGContextMenu>("SampleContextMenu");
        MGBorder dockHostContainer = Window.GetElementByName<MGBorder>("DockHostContainer");
        MGTextBox limitsText = Window.GetElementByName<MGTextBox>("LimitsText");

        sampleComboBox.SelectedIndex = 1;
        sampleListBox.SetItemsSource(new[] { "Warehouse_Materials.asset", "Props_Crates.asset", "UI_HUD.asset", "LightingProfile.asset" });

        DockTabGroupNode dockGroup = new();
        dockGroup.AddPanel(new DockPanelNode { Title = "Scene", ContentFactory = () => new MGTextBlock(Window, "Scene view") }, -1);
        dockGroup.AddPanel(new DockPanelNode { Title = "Assets", ContentFactory = () => new MGTextBlock(Window, "Asset browser") }, -1);
        dockHostContainer.SetContent(new MGDockHost(Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(dockGroup),
        });

        //  SampleContextMenu is the button's ContextMenu, never a panel child; the anchor is in screen space, just below the button.
        openContextMenuButton.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            Microsoft.Xna.Framework.Rectangle buttonScreenBounds = openContextMenuButton.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, openContextMenuButton.LayoutBounds);
            if (sampleContextMenu.TryOpenContextMenu(new Point(buttonScreenBounds.Left, buttonScreenBounds.Bottom)))
            {
                e.SetHandledBy(sampleContextMenu, false);
            }
        };

        void ApplyTheme(MGTheme theme, string label)
        {
            Window.GetResources().DefaultTheme = theme;
            activeThemeText.SetText($"Active theme: {label}");
        }

        applyDarkButton.Command = _ =>
        {
            ApplyTheme(darkTheme, "Dark");
            return true;
        };
        applyCompactButton.Command = _ =>
        {
            ApplyTheme(compactTheme, "Editor Compact");
            return true;
        };

        limitsText.SetText(LimitsText);
        ApplyTheme(compactTheme, "Editor Compact");
    }
}
