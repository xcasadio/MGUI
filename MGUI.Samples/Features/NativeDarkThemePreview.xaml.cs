using System;
using System.Collections.Generic;
using MGUI.Core.UI;
using MGUI.Samples.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features;

public sealed class NativeDarkThemePreviewSample : SampleBase
{
    private const string EditorThemeName = "Dark";

    private static string _statusMessage = "Native MGUI dark theme preview is active.";

    private readonly MGTheme _editorTheme;
    private MGTheme _previousDesktopTheme;
    private bool _isDesktopThemeOverridden;

    public NativeDarkThemePreviewSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "NativeDarkThemePreview.xaml")
    {
        ApplyScenarioId("SCN-EDITOR-THEME-001");

        _editorTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily);

        if (_editorTheme != null)
        {
            Window.GetResources().DefaultTheme = _editorTheme;
        }

        MGTextBlock themeStatusText = Window.GetElementByName<MGTextBlock>("ThemeStatusText");
        MGTextBox assetPathsText = Window.GetElementByName<MGTextBox>("AssetPathsText");
        MGComboBox<string> sampleComboBox = Window.GetElementByName<MGComboBox<string>>("SampleComboBox");
        MGListBox<string> sampleListBox = Window.GetElementByName<MGListBox<string>>("SampleListBox");
        MGListView<Person> sampleListView = Window.GetElementByName<MGListView<Person>>("SampleListView");
        MGButton openContextMenuButton = Window.GetElementByName<MGButton>("OpenContextMenuButton");
        MGContextMenu sampleContextMenu = Window.GetElementByName<MGContextMenu>("SampleContextMenu");
        MGButton toggleOverlayButton = Window.GetElementByName<MGButton>("ToggleOverlayButton");
        MGOverlay sampleOverlay = Window.GetElementByName<MGOverlay>("SampleOverlay");

        sampleComboBox.SelectedIndex = 1;
        sampleListBox.SetItemsSource(new[]
        {
            "Warehouse_Materials.asset",
            "Props_Crates.asset",
            "UI_HUD.asset",
            "LightingProfile.asset",
        });

        sampleListView.Columns[1].CellTemplate = person => new MGTextBlock(Window, person.FirstName, person.IsMale ? Color.CornflowerBlue : Color.HotPink);
        sampleListView.Columns[2].CellTemplate = person => new MGTextBlock(Window, person.LastName, person.IsMale ? Color.CornflowerBlue : Color.HotPink);
        sampleListView.SetItemsSource(new List<Person>
        {
            new(1, "Metal", "Override", true),
            new(2, "Stone", "Inherited", false),
            new(3, "Glass", "Default", true),
            new(4, "Fabric", "Override", false),
        });

        openContextMenuButton.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            sampleContextMenu.TryOpenContextMenu(openContextMenuButton.LayoutBounds);
            e.SetHandledBy(openContextMenuButton, false);
        };

        toggleOverlayButton.Command = _ =>
        {
            sampleOverlay.IsOpen = !sampleOverlay.IsOpen;
            toggleOverlayButton.SetContent(sampleOverlay.IsOpen ? "Hide Overlay" : "Show Overlay");
            return true;
        };
        toggleOverlayButton.SetContent(sampleOverlay.IsOpen ? "Hide Overlay" : "Show Overlay");

        VisibilityChanged += (_, isVisible) =>
        {
            if (_editorTheme == null)
            {
                return;
            }

            if (isVisible)
            {
                if (!_isDesktopThemeOverridden)
                {
                    _previousDesktopTheme = Desktop.Resources.DefaultTheme;
                    Desktop.Resources.DefaultTheme = _editorTheme;
                    _isDesktopThemeOverridden = true;
                }
            }
            else if (_isDesktopThemeOverridden && _previousDesktopTheme != null)
            {
                Desktop.Resources.DefaultTheme = _previousDesktopTheme;
                _isDesktopThemeOverridden = false;
            }
        };

        themeStatusText.SetText(BuildThemeStatusMessage(_editorTheme != null));
        assetPathsText.SetText(BuildAssetReport(_editorTheme != null));
    }

    private string BuildThemeStatusMessage(bool themeResolved)
    {
        if (!themeResolved)
        {
            return _statusMessage;
        }

        return "MGUI built-in Dark theme is active. Use this preview to inspect the native chrome, spacing, and contrast. While this window is visible, the sample desktop also switches to the built-in Dark theme so DockingDemo can be inspected in the same visual system.";
    }

    private string BuildAssetReport(bool themeResolved)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Theme resolved: {themeResolved}",
            "Theme source: MGUI.Core built-in theme definitions",
            "Control template source: MGUI.Core built-in control templates",
            $"Expected theme name: {EditorThemeName}",
            $"Requested font family: {_editorTheme?.FontSettings.DefaultFontFamily ?? "not resolved"}",
            $"Desktop default font family: {Desktop.DefaultFontFamily}",
        });
    }
}