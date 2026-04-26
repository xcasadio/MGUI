using System;
using System.Collections.Generic;
using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Samples.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features;

public sealed class EditorDarkThemePreviewSample : SampleBase
{
    private const string EditorThemeName = "CasaEditor.Dark";
    private const string ThemeRelativePath = @"CasaEngine.Editor\Content\UI\Themes\CasaEditor.Dark.Theme.xaml";
    private const string TemplateRelativePath = @"CasaEngine.Editor\Content\UI\Templates\CasaEditor.Dark.ControlTemplates.xaml";

    private static bool _attemptedInitialization;
    private static string _statusMessage = "Editor theme assets have not been loaded.";
    private static string _themeAssetPath;
    private static string _templateAssetPath;

    public EditorDarkThemePreviewSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "EditorDarkThemePreview.xaml", () => InitializeResources(desktop))
    {
        ApplyScenarioId("SCN-EDITOR-THEME-001");

        MGTheme editorTheme = Window.GetResources().GetThemeOrDefault(EditorThemeName, null, false);
        if (editorTheme != null)
        {
            Window.GetResources().DefaultTheme = editorTheme;
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

        themeStatusText.SetText(_statusMessage);
        assetPathsText.SetText(BuildAssetReport(editorTheme != null));
    }

    private static void InitializeResources(MGDesktop desktop)
    {
        if (_attemptedInitialization)
        {
            return;
        }

        _attemptedInitialization = true;

        string repoRoot = TryFindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot == null)
        {
            _statusMessage = "Editor theme assets were not found. Run this sample from the CasaEngineMonogame workspace to preview CasaEditor.Dark.";
            return;
        }

        string themePath = Path.Combine(repoRoot, ThemeRelativePath);
        string templatePath = Path.Combine(repoRoot, TemplateRelativePath);

        if (File.Exists(templatePath))
        {
            desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(templatePath));
            _templateAssetPath = templatePath;
        }

        if (File.Exists(themePath))
        {
            desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(themePath));
            _themeAssetPath = themePath;
        }

        bool themeLoaded = desktop.Resources.GetThemeOrDefault(EditorThemeName, null, false) != null;
        _statusMessage = themeLoaded
            ? "CasaEditor.Dark loaded from CasaEngine.Editor content assets. Use this window to review runtime chrome, spacing, and contrast."
            : "Editor control template or theme assets could not be fully resolved. Check the asset report below.";
    }

    private static string BuildAssetReport(bool themeResolved)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Theme resolved: {themeResolved}",
            $"Theme file: {_themeAssetPath ?? "not found"}",
            $"Control templates file: {_templateAssetPath ?? "not found"}",
            $"Expected theme name: {EditorThemeName}",
        });
    }

    private static string TryFindRepoRoot(string baseDirectory)
    {
        DirectoryInfo current = new DirectoryInfo(baseDirectory);
        while (current != null)
        {
            string editorDirectory = Path.Combine(current.FullName, "CasaEngine.Editor");
            string mguiDirectory = Path.Combine(current.FullName, "MGUI");

            if (Directory.Exists(editorDirectory) && Directory.Exists(mguiDirectory))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}