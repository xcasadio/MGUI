using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class StyleThemeRefactorSample : SampleBase
    {
        private MGTheme.BuiltInTheme CurrentThemeType { get; set; } = MGTheme.BuiltInTheme.Dark_Blue;

        public StyleThemeRefactorSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, nameof(Features), "StyleThemeRefactor.xaml")
        {
            MGButton darkThemeButton = Window.GetElementByName<MGButton>("ApplyDarkBlueThemeButton");
            MGButton lightThemeButton = Window.GetElementByName<MGButton>("ApplyLightThemeButton");
            MGButton openContextMenuButton = Window.GetElementByName<MGButton>("OpenContextMenuButton");
            MGTextBlock activeThemeText = Window.GetElementByName<MGTextBlock>("ActiveThemeText");
            MGTextBox exampleXamlText = Window.GetElementByName<MGTextBox>("ExampleXamlText");
            MGContextMenu sampleContextMenu = Window.GetElementByName<MGContextMenu>("SampleContextMenu");
            MGListBox<string> sampleListBox = Window.GetElementByName<MGListBox<string>>("SampleListBox");
            MGComboBox<string> sampleComboBox = Window.GetElementByName<MGComboBox<string>>("SampleComboBox");

            sampleListBox.SetItemsSource(new[]
            {
                "Template parts",
                "Resource scopes",
                "Dynamic resources",
                "Runtime theme switch"
            });
            sampleComboBox.SelectedIndex = 1;

            darkThemeButton.Command = _ =>
            {
                ApplyTheme(MGTheme.BuiltInTheme.Dark_Blue, "Dark Blue", activeThemeText);
                return true;
            };
            lightThemeButton.Command = _ =>
            {
                ApplyTheme(MGTheme.BuiltInTheme.Light_Gray, "Light Gray", activeThemeText);
                return true;
            };
            openContextMenuButton.MouseHandler.LMBReleasedInside += (_, e) =>
            {
                sampleContextMenu.TryOpenContextMenu(openContextMenuButton.LayoutBounds);
                e.SetHandledBy(openContextMenuButton, false);
            };

            exampleXamlText.SetText(
@"<ComboBox ItemType=""{x:Type System:String}""
          ControlTemplate=""ComboBox.Default"" />

<ListBox ItemType=""{x:Type System:String}""
         ControlTemplate=""ListBox.Default"" />

<TabControl ControlTemplate=""TabControl.Default"" />");
        }

        private void ApplyTheme(MGTheme.BuiltInTheme builtInTheme, string label, MGTextBlock statusText)
        {
            if (builtInTheme == CurrentThemeType)
            {
                return;
            }

            string fontFamily = Desktop.Theme.FontSettings.DefaultFontFamily;
            Window.GetResources().DefaultTheme = new MGTheme(builtInTheme, fontFamily);
            CurrentThemeType = builtInTheme;
            statusText.SetText($"Active theme: {label}");
        }
    }
}
