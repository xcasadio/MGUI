using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Samples.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace MGUI.Samples.Features
{
    public class StyleThemeRefactorSample : SampleBase
    {
                private const string SampleTemplateResourceName = "StyleThemeRefactorSample.ControlTemplates.xaml";
                private const string SampleThemeResourceName = "StyleThemeRefactorSample.Themes.xaml";
                                private const string SampleCoverageMatrixText = @"100% pilotable en XAML
- ThemeDefinition / ThemeDefinitionsDocument
- BasedOn
- FontSettings
- Backgrounds par MGElementType
- ControlTemplates mappings
- Groupes exposes: Window, Overlay, ContextMenu, ContextMenuItem, ListBox, ListView, ComboBox, TreeViewTemplate, TabControl, Docking
- ThemePropertyTarget

Partiel
- styles XAML a base de Setter
- ControlTemplate XAML des controles deja migres
- controles classes B et C

Pas encore complet
- proprietes MGTheme non exposees par ThemeDefinition
- controles encore imperatifs (ex: CheckBox, Expander, ContextMenuItem, une partie du docking)
- skin 100% XAML de toute la librairie";

                                private const string SampleThemeExcerptText = @"<ControlTemplates xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
    <ControlTemplate Name=""ListView.HeadersBottom"" TargetType=""ListView"">
        <ControlTemplate.Parts>
            <TemplatePart Name=""PART_DockPanel"" />
            <TemplatePart Name=""PART_ScrollViewer"" />
            <TemplatePart Name=""PART_DataGrid"" />
            <TemplatePart Name=""PART_HeaderGridWrapper"" />
            <TemplatePart Name=""PART_HeaderSpacer"" />
            <TemplatePart Name=""PART_HeaderGrid"" />
        </ControlTemplate.Parts>
        <DockPanel Name=""PART_DockPanel"">
            <ScrollViewer Name=""PART_ScrollViewer"" Dock=""Top"">
                <Grid Name=""PART_DataGrid"" />
            </ScrollViewer>
            <DockPanel Name=""PART_HeaderGridWrapper"" Dock=""Bottom"">
                <Border Name=""PART_HeaderSpacer"" Dock=""Right"" />
                <Grid Name=""PART_HeaderGrid"" Dock=""Left"" RowLengths=""Auto"" />
            </DockPanel>
        </DockPanel>
    </ControlTemplate>
</ControlTemplates>

<ThemeDefinition Name=""BlueprintSkin"" BasedOn=""Dark_Blue"">
    <ThemeDefinition.ControlTemplates>
        <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.Default"" />
    </ThemeDefinition.ControlTemplates>
</ThemeDefinition>

MGTheme darkTheme = new(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily);
desktop.Resources.AddTheme(""DarkSkin"", darkTheme);

<ListView ItemType=""{x:Type controls:Person}"" />";

                private const string SampleTemplatesXaml = @"
<ControlTemplates xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
    <ControlTemplate Name=""ListView.HeadersBottom"" TargetType=""ListView"">
                <ControlTemplate.Parts>
                    <TemplatePart Name=""PART_DockPanel"" />
                    <TemplatePart Name=""PART_ScrollViewer"" />
                    <TemplatePart Name=""PART_DataGrid"" />
                    <TemplatePart Name=""PART_HeaderGridWrapper"" />
                    <TemplatePart Name=""PART_HeaderSpacer"" />
                    <TemplatePart Name=""PART_HeaderGrid"" />
                </ControlTemplate.Parts>
        <DockPanel Name=""PART_DockPanel"">
            <ScrollViewer Name=""PART_ScrollViewer"" Dock=""Top"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Disabled"">
                <Grid Name=""PART_DataGrid"" />
            </ScrollViewer>
            <DockPanel Name=""PART_HeaderGridWrapper"" Dock=""Bottom"">
                <Border Name=""PART_HeaderSpacer"" Dock=""Right"" />
                <Grid Name=""PART_HeaderGrid"" Dock=""Left"" RowLengths=""Auto"" />
            </DockPanel>
        </DockPanel>
    </ControlTemplate>
</ControlTemplates>
";

                private const string SampleThemesXaml = @"
<ThemeDefinitionsDocument xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
    <ThemeDefinition Name=""BlueprintSkin"" BasedOn=""Dark_Blue"">
        <ThemeDefinition.Backgrounds>
            <ThemeBackgroundDefinition ElementType=""ScrollViewer"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(64,64,64)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
        </ThemeDefinition.Backgrounds>
        <ThemeDefinition.ControlTemplates>
            <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.Default"" />
        </ThemeDefinition.ControlTemplates>
    </ThemeDefinition>
</ThemeDefinitionsDocument>
";

        public StyleThemeRefactorSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, nameof(Features), "StyleThemeRefactor.xaml")
        {
                        ApplyScenarioId("SCN-THEME-001");

                        Window.GetResources().LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(SampleTemplatesXaml, SampleTemplateResourceName));
                        Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromString(SampleThemesXaml, SampleThemeResourceName));

                        MGButton blueprintSkinButton = Window.GetElementByName<MGButton>("ApplyBlueprintSkinButton");
                        Window.GetResources().AddTheme("DarkSkin", new MGTheme(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily));

                        MGButton darkSkinButton = Window.GetElementByName<MGButton>("ApplyDarkSkinButton");
            MGButton openContextMenuButton = Window.GetElementByName<MGButton>("OpenContextMenuButton");
            MGTextBlock activeThemeText = Window.GetElementByName<MGTextBlock>("ActiveThemeText");
                        MGTextBox xamlCoverageMatrixText = Window.GetElementByName<MGTextBox>("XamlCoverageMatrixText");
            MGTextBox exampleXamlText = Window.GetElementByName<MGTextBox>("ExampleXamlText");
            MGContextMenu sampleContextMenu = Window.GetElementByName<MGContextMenu>("SampleContextMenu");
            MGScrollViewer rootScrollViewer = Window.GetElementByName<MGScrollViewer>("RootScrollViewer");
            MGListBox<string> sampleListBox = Window.GetElementByName<MGListBox<string>>("SampleListBox");
            MGComboBox<string> sampleComboBox = Window.GetElementByName<MGComboBox<string>>("SampleComboBox");
                        MGListView<Person> sampleListView = Window.GetElementByName<MGListView<Person>>("SampleListView");
            MGBorder selectionControlsPanelBorder = Window.GetElementByName<MGBorder>("SelectionControlsPanelBorder");
            MGBorder compositeControlsPanelBorder = Window.GetElementByName<MGBorder>("CompositeControlsPanelBorder");
            MGBorder migrationNotesPanelBorder = Window.GetElementByName<MGBorder>("MigrationNotesPanelBorder");
            VisualStateFillBrush selectionControlsPanelBackground = selectionControlsPanelBorder.BackgroundBrush?.Copy();
            VisualStateFillBrush compositeControlsPanelBackground = compositeControlsPanelBorder.BackgroundBrush?.Copy();
            VisualStateFillBrush migrationNotesPanelBackground = migrationNotesPanelBorder.BackgroundBrush?.Copy();
            IBorderBrush selectionControlsPanelBorderBrush = selectionControlsPanelBorder.BorderBrush?.Copy();
            IBorderBrush compositeControlsPanelBorderBrush = compositeControlsPanelBorder.BorderBrush?.Copy();
            IBorderBrush migrationNotesPanelBorderBrush = migrationNotesPanelBorder.BorderBrush?.Copy();

            sampleListBox.SetItemsSource(new[]
            {
                "Template parts",
                "Resource scopes",
                "Dynamic resources",
                "Runtime theme switch"
            });
            sampleComboBox.SelectedIndex = 1;

                        sampleListView.Columns[1].CellTemplate = person => new MGTextBlock(Window, person.FirstName, person.IsMale ? Color.CornflowerBlue : Color.HotPink);
                        sampleListView.Columns[2].CellTemplate = person => new MGTextBlock(Window, person.LastName, person.IsMale ? Color.CornflowerBlue : Color.HotPink);
                        sampleListView.SetItemsSource(new List<Person>
                        {
                                new(1, "John", "Smith", true),
                                new(2, "Emily", "Stone", false),
                                new(3, "Marcus", "Reed", true),
                                new(4, "Alice", "Wright", false)
                        });

                        blueprintSkinButton.Command = _ =>
            {
                                ApplyTheme("BlueprintSkin", "Blueprint", activeThemeText, sampleContextMenu, sampleListBox,
                                    sampleListView, xamlCoverageMatrixText, exampleXamlText, openContextMenuButton, rootScrollViewer,
                                    selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                                    selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                                    selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                                    blueprintSkinButton, darkSkinButton);
                return true;
            };
                        darkSkinButton.Command = _ =>
            {
                                ApplyTheme("DarkSkin", "Dark", activeThemeText, sampleContextMenu, sampleListBox,
                                    sampleListView, xamlCoverageMatrixText, exampleXamlText, openContextMenuButton, rootScrollViewer,
                                    selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                                    selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                                    selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                                    blueprintSkinButton, darkSkinButton);
                return true;
            };
            openContextMenuButton.MouseHandler.LMBReleasedInside += (_, e) =>
            {
                sampleContextMenu.TryOpenContextMenu(openContextMenuButton.LayoutBounds);
                e.SetHandledBy(openContextMenuButton, false);
            };

            xamlCoverageMatrixText.SetText(SampleCoverageMatrixText);
            exampleXamlText.SetText(SampleThemeExcerptText);

                        ApplyTheme("BlueprintSkin", "Blueprint", activeThemeText, sampleContextMenu, sampleListBox,
                            sampleListView, xamlCoverageMatrixText, exampleXamlText, openContextMenuButton, rootScrollViewer,
                            selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                            selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                            selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                            blueprintSkinButton, darkSkinButton);
        }

                    private void ApplyTheme(string themeName, string label, MGTextBlock statusText,
                        MGContextMenu sampleContextMenu, MGListBox<string> sampleListBox,
                        MGListView<Person> sampleListView, MGTextBox xamlCoverageMatrixText, MGTextBox exampleXamlText,
                        MGButton openContextMenuButton, MGScrollViewer rootScrollViewer,
                        VisualStateFillBrush selectionControlsPanelBackground, VisualStateFillBrush compositeControlsPanelBackground, VisualStateFillBrush migrationNotesPanelBackground,
                        IBorderBrush selectionControlsPanelBorderBrush, IBorderBrush compositeControlsPanelBorderBrush, IBorderBrush migrationNotesPanelBorderBrush,
                        MGBorder selectionControlsPanelBorder, MGBorder compositeControlsPanelBorder, MGBorder migrationNotesPanelBorder,
                        MGButton blueprintSkinButton, MGButton darkSkinButton)
        {
                        MGTheme theme = Window.GetResources().GetThemeOrDefault(themeName, null, false);
                        if (theme != null)
                        {
                                Window.GetResources().DefaultTheme = theme;
                            Color fallbackTextColor = theme.TextBlockFallbackForeground.GetValue(true).NormalValue;
                            var darkGrayBorderBrush = new MGSolidFillBrush(new Color(72, 72, 72)).AsUniformBorderBrush();
                            bool isDarkSkin = themeName == "DarkSkin";
                            var darkSelectionPanelBrush = theme.GetBackgroundBrush(MGElementType.ListBox);
                            var darkCompositePanelBrush = theme.GetBackgroundBrush(MGElementType.TabControl);
                            var darkNotesPanelBrush = theme.GetBackgroundBrush(MGElementType.TextBox);

                            Window.BorderBrush = theme.Window.BorderBrush?.Copy() ?? Window.BorderBrush;
                            Window.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.Window);
                            rootScrollViewer.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.ScrollViewer);

                            sampleContextMenu.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.ContextMenu);
                            sampleContextMenu.BorderBrush = theme.ContextMenu.BorderBrush?.Copy() ?? sampleContextMenu.BorderBrush;
                            sampleContextMenu.ButtonWrapperTemplate = sampleContextMenu.CreateDefaultDropdownButton;
                            foreach (MGWrappedContextMenuItem item in sampleContextMenu.Items.OfType<MGWrappedContextMenuItem>())
                            {
                                item.ContentWrapper.DefaultTextForeground.SetAll(fallbackTextColor);
                                if (item.MenuItemContent is MGTextBlock textBlock)
                                {
                                    textBlock.Foreground.SetAll(fallbackTextColor);
                                }
                            }

                            string[] listBoxItems = sampleListBox.ItemsSource?.ToArray()
                                ?? new[] { "Template parts", "Resource scopes", "Dynamic resources", "Runtime theme switch" };
                            sampleListBox.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.ListBox);
                            sampleListBox.SetItemsSource(listBoxItems);

                            sampleListView.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.ListView);
                            sampleListView.HeaderGrid.BackgroundBrush = theme.TitleBackground.GetValue(true);
                            sampleListView.HeaderGrid.DefaultTextForeground.SetAll(fallbackTextColor);
                            sampleListView.DataGrid.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.ListView);
                            sampleListView.DataGrid.DefaultTextForeground.SetAll(fallbackTextColor);

                            xamlCoverageMatrixText.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            xamlCoverageMatrixText.GetBorder().BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            xamlCoverageMatrixText.DefaultTextForeground.SetAll(fallbackTextColor);

                            exampleXamlText.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            exampleXamlText.GetBorder().BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            exampleXamlText.DefaultTextForeground.SetAll(fallbackTextColor);

                            openContextMenuButton.DefaultTextForeground.SetAll(fallbackTextColor);
                            blueprintSkinButton.DefaultTextForeground.SetAll(fallbackTextColor);
                            darkSkinButton.DefaultTextForeground.SetAll(fallbackTextColor);

                            selectionControlsPanelBorder.BackgroundBrush = isDarkSkin ? darkSelectionPanelBrush : selectionControlsPanelBackground?.Copy();
                            compositeControlsPanelBorder.BackgroundBrush = isDarkSkin ? darkCompositePanelBrush : compositeControlsPanelBackground?.Copy();
                            migrationNotesPanelBorder.BackgroundBrush = isDarkSkin ? darkNotesPanelBrush : migrationNotesPanelBackground?.Copy();

                            selectionControlsPanelBorder.BorderBrush = isDarkSkin ? darkGrayBorderBrush.Copy() : selectionControlsPanelBorderBrush?.Copy();
                            compositeControlsPanelBorder.BorderBrush = isDarkSkin ? darkGrayBorderBrush.Copy() : compositeControlsPanelBorderBrush?.Copy();
                            migrationNotesPanelBorder.BorderBrush = isDarkSkin ? darkGrayBorderBrush.Copy() : migrationNotesPanelBorderBrush?.Copy();

                                statusText.SetText($"Active skin: {label}");
                        }
        }
    }
}
