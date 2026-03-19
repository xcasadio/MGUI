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
    <ThemeDefinition Name=""LedgerSkin"" BasedOn=""Light_Gray"">
        <ThemeDefinition.Backgrounds>
            <ThemeBackgroundDefinition ElementType=""Window"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(72,72,72)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""ScrollViewer"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(96,96,96)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""Button"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(228,231,235)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""ComboBox"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(236,239,242)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""ContextMenu"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(236,239,242)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""ListBox"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(224,228,232)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""ListView"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(230,234,238)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""TabControl"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(218,222,226)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
            <ThemeBackgroundDefinition ElementType=""TextBox"">
                <ThemeBackgroundDefinition.Value>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(242,245,248)"" />
                </ThemeBackgroundDefinition.Value>
            </ThemeBackgroundDefinition>
        </ThemeDefinition.Backgrounds>
        <ThemeDefinition.Window>
            <ThemeWindowSettingsDefinition BorderBrush=""rgb(72,72,72)"" />
        </ThemeDefinition.Window>
        <ThemeDefinition.ContextMenu>
            <ThemeContextMenuSettingsDefinition BorderBrush=""rgb(96,96,96)""
                                               BorderThickness=""1"" />
        </ThemeDefinition.ContextMenu>
        <ThemeDefinition.ContextMenuItem>
            <ThemeContextMenuItemSettingsDefinition>
                <ThemeContextMenuItemSettingsDefinition.HeaderBackground>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(236,239,242)""
                                                         FocusedValue=""rgb(220,224,228)""
                                                         SelectedValue=""rgb(220,224,228)""
                                                         DisabledValue=""rgb(236,239,242) * 0.7"" />
                </ThemeContextMenuItemSettingsDefinition.HeaderBackground>
            </ThemeContextMenuItemSettingsDefinition>
        </ThemeDefinition.ContextMenuItem>
        <ThemeDefinition.ListBox>
            <ThemeListBoxSettingsDefinition TitleBorderBrush=""rgb(96,96,96)""
                                            TitleBorderThickness=""1,1,1,0""
                                            InnerBorderBrush=""rgb(96,96,96)""
                                            InnerBorderThickness=""1""
                                            ItemsPanelBorderBrush=""rgb(96,96,96)""
                                            ItemsPanelBorderThickness=""1"">
                <ThemeListBoxSettingsDefinition.OuterBackground>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(224,228,232)"" />
                </ThemeListBoxSettingsDefinition.OuterBackground>
                <ThemeListBoxSettingsDefinition.TitleForeground>
                    <ThemeVisualStateColorSettingDefinition NormalValue=""Black""
                                                           SelectedValue=""Black""
                                                           FocusedValue=""Black""
                                                           DisabledValue=""Black"" />
                </ThemeListBoxSettingsDefinition.TitleForeground>
            </ThemeListBoxSettingsDefinition>
        </ThemeDefinition.ListBox>
        <ThemeDefinition.ControlTemplates>
            <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.HeadersBottom"" />
        </ThemeDefinition.ControlTemplates>
        <ThemeDefinition.Properties>
            <ThemePropertyDefinition Target=""ScrollBarOuterBrush"">
                <ThemePropertyDefinition.VisualStateFillBrush>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(72,72,72)""
                                                         FocusedValue=""rgb(84,84,84)""
                                                         SelectedValue=""rgb(84,84,84)""
                                                         DisabledValue=""rgb(72,72,72) * 0.6"" />
                </ThemePropertyDefinition.VisualStateFillBrush>
            </ThemePropertyDefinition>
            <ThemePropertyDefinition Target=""ScrollBarInnerBrush"">
                <ThemePropertyDefinition.VisualStateFillBrush>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(208,212,216)""
                                                         FocusedValue=""rgb(220,224,228)""
                                                         SelectedValue=""rgb(220,224,228)""
                                                         DisabledValue=""rgb(208,212,216) * 0.7"" />
                </ThemePropertyDefinition.VisualStateFillBrush>
            </ThemePropertyDefinition>
            <ThemePropertyDefinition Target=""ListBoxItemBackground"">
                <ThemePropertyDefinition.VisualStateFillBrush>
                    <ThemeVisualStateFillBrushDefinition NormalValue=""rgb(236,239,242)""
                                                         FocusedValue=""rgb(220,224,228)""
                                                         SelectedValue=""rgb(220,224,228)""
                                                         DisabledValue=""rgb(236,239,242) * 0.7"" />
                </ThemePropertyDefinition.VisualStateFillBrush>
            </ThemePropertyDefinition>
        </ThemeDefinition.Properties>
    </ThemeDefinition>
</ThemeDefinitionsDocument>
";

        public StyleThemeRefactorSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, nameof(Features), "StyleThemeRefactor.xaml")
        {
                        Window.GetResources().LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(SampleTemplatesXaml, SampleTemplateResourceName));
                        Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromString(SampleThemesXaml, SampleThemeResourceName));

                        MGButton blueprintSkinButton = Window.GetElementByName<MGButton>("ApplyBlueprintSkinButton");
                        MGButton ledgerSkinButton = Window.GetElementByName<MGButton>("ApplyLedgerSkinButton");
            MGButton openContextMenuButton = Window.GetElementByName<MGButton>("OpenContextMenuButton");
            MGTextBlock activeThemeText = Window.GetElementByName<MGTextBlock>("ActiveThemeText");
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
                                    sampleListView, exampleXamlText, openContextMenuButton, rootScrollViewer,
                                    selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                                    selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                                    selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                                    blueprintSkinButton, ledgerSkinButton);
                return true;
            };
                        ledgerSkinButton.Command = _ =>
            {
                                ApplyTheme("LedgerSkin", "Ledger", activeThemeText, sampleContextMenu, sampleListBox,
                                    sampleListView, exampleXamlText, openContextMenuButton, rootScrollViewer,
                                    selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                                    selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                                    selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                                    blueprintSkinButton, ledgerSkinButton);
                return true;
            };
            openContextMenuButton.MouseHandler.LMBReleasedInside += (_, e) =>
            {
                sampleContextMenu.TryOpenContextMenu(openContextMenuButton.LayoutBounds);
                e.SetHandledBy(openContextMenuButton, false);
            };

            exampleXamlText.SetText(
@"<ThemeDefinition Name=""LedgerSkin"" BasedOn=""Light_Gray"">
    <ThemeDefinition.ControlTemplates>
        <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.HeadersBottom"" />
    </ThemeDefinition.ControlTemplates>
</ThemeDefinition>

<ListView ItemType=""{x:Type controls:Person}"" />");

                        ApplyTheme("BlueprintSkin", "Blueprint", activeThemeText, sampleContextMenu, sampleListBox,
                            sampleListView, exampleXamlText, openContextMenuButton, rootScrollViewer,
                            selectionControlsPanelBackground, compositeControlsPanelBackground, migrationNotesPanelBackground,
                            selectionControlsPanelBorderBrush, compositeControlsPanelBorderBrush, migrationNotesPanelBorderBrush,
                            selectionControlsPanelBorder, compositeControlsPanelBorder, migrationNotesPanelBorder,
                            blueprintSkinButton, ledgerSkinButton);
        }

                    private void ApplyTheme(string themeName, string label, MGTextBlock statusText,
                        MGContextMenu sampleContextMenu, MGListBox<string> sampleListBox,
                        MGListView<Person> sampleListView, MGTextBox exampleXamlText, MGButton openContextMenuButton, MGScrollViewer rootScrollViewer,
                        VisualStateFillBrush selectionControlsPanelBackground, VisualStateFillBrush compositeControlsPanelBackground, VisualStateFillBrush migrationNotesPanelBackground,
                        IBorderBrush selectionControlsPanelBorderBrush, IBorderBrush compositeControlsPanelBorderBrush, IBorderBrush migrationNotesPanelBorderBrush,
                        MGBorder selectionControlsPanelBorder, MGBorder compositeControlsPanelBorder, MGBorder migrationNotesPanelBorder,
                        MGButton blueprintSkinButton, MGButton ledgerSkinButton)
        {
                        MGTheme theme = Window.GetResources().GetThemeOrDefault(themeName, null, false);
                        if (theme != null)
                        {
                                Window.GetResources().DefaultTheme = theme;
                            Color fallbackTextColor = theme.TextBlockFallbackForeground.GetValue(true).NormalValue;
                            var darkGrayBorderBrush = new MGSolidFillBrush(new Color(72, 72, 72)).AsUniformBorderBrush();
                            bool isLedgerSkin = themeName == "LedgerSkin";
                            var ledgerSelectionPanelBrush = theme.GetBackgroundBrush(MGElementType.ListBox);
                            var ledgerCompositePanelBrush = theme.GetBackgroundBrush(MGElementType.TabControl);
                            var ledgerNotesPanelBrush = theme.GetBackgroundBrush(MGElementType.TextBox);

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

                            exampleXamlText.BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            exampleXamlText.GetBorder().BackgroundBrush = theme.GetBackgroundBrush(MGElementType.TextBox);
                            exampleXamlText.DefaultTextForeground.SetAll(fallbackTextColor);

                            openContextMenuButton.DefaultTextForeground.SetAll(fallbackTextColor);
                            blueprintSkinButton.DefaultTextForeground.SetAll(fallbackTextColor);
                            ledgerSkinButton.DefaultTextForeground.SetAll(fallbackTextColor);

                            selectionControlsPanelBorder.BackgroundBrush = isLedgerSkin ? ledgerSelectionPanelBrush : selectionControlsPanelBackground?.Copy();
                            compositeControlsPanelBorder.BackgroundBrush = isLedgerSkin ? ledgerCompositePanelBrush : compositeControlsPanelBackground?.Copy();
                            migrationNotesPanelBorder.BackgroundBrush = isLedgerSkin ? ledgerNotesPanelBrush : migrationNotesPanelBackground?.Copy();

                            selectionControlsPanelBorder.BorderBrush = isLedgerSkin ? darkGrayBorderBrush.Copy() : selectionControlsPanelBorderBrush?.Copy();
                            compositeControlsPanelBorder.BorderBrush = isLedgerSkin ? darkGrayBorderBrush.Copy() : compositeControlsPanelBorderBrush?.Copy();
                            migrationNotesPanelBorder.BorderBrush = isLedgerSkin ? darkGrayBorderBrush.Copy() : migrationNotesPanelBorderBrush?.Copy();

                                statusText.SetText($"Active skin: {label}");
                        }
        }
    }
}
