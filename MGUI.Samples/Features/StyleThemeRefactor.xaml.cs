using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Samples.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;

namespace MGUI.Samples.Features
{
    public class StyleThemeRefactorSample : SampleBase
    {
                private const string SampleTemplateResourceName = "StyleThemeRefactorSample.ControlTemplates.xaml";
                private const string SampleThemeResourceName = "StyleThemeRefactorSample.Themes.xaml";

                private const string SampleTemplatesXaml = @"
<ControlTemplates xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
    <ControlTemplate Name=""ListView.HeadersBottom"" TargetType=""ListView"">
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
        <ThemeDefinition.ControlTemplates>
            <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.Default"" />
        </ThemeDefinition.ControlTemplates>
    </ThemeDefinition>
    <ThemeDefinition Name=""LedgerSkin"" BasedOn=""Light_Gray"">
        <ThemeDefinition.ControlTemplates>
            <ThemeControlTemplateDefinition ElementType=""ListView"" TemplateName=""ListView.HeadersBottom"" />
        </ThemeDefinition.ControlTemplates>
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
            MGListBox<string> sampleListBox = Window.GetElementByName<MGListBox<string>>("SampleListBox");
            MGComboBox<string> sampleComboBox = Window.GetElementByName<MGComboBox<string>>("SampleComboBox");
                        MGListView<Person> sampleListView = Window.GetElementByName<MGListView<Person>>("SampleListView");

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
                                ApplyTheme("BlueprintSkin", "Blueprint", activeThemeText);
                return true;
            };
                        ledgerSkinButton.Command = _ =>
            {
                                ApplyTheme("LedgerSkin", "Ledger", activeThemeText);
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

                        ApplyTheme("BlueprintSkin", "Blueprint", activeThemeText);
        }

                private void ApplyTheme(string themeName, string label, MGTextBlock statusText)
        {
                        MGTheme theme = Window.GetResources().GetThemeOrDefault(themeName, null, false);
                        if (theme != null)
                        {
                                Window.GetResources().DefaultTheme = theme;
                                statusText.SetText($"Active skin: {label}");
                        }
        }
    }
}
