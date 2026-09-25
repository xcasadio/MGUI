using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Controls
{
    /// <summary>Shows <see cref="MGMessageBox"/>: an OK message, a Yes / No question, a three-button save question, and two
    /// questions in a row where the second is opened from the first one's callback (ADR-0018).</summary>
    public class MessageBoxSamples : SampleBase
    {
        private static readonly string[] OkLabels = { "OK" };
        private static readonly string[] YesNoLabels = { "Yes", "No" };
        private static readonly string[] SaveLabels = { "Save", "Don't Save", "Cancel" };

        private readonly MGTextBlock _resultText;

        public MessageBoxSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "MessageBox.xaml")
        {
            if (Window == null)
            {
                return;
            }

            _resultText = Window.GetElementByName<MGTextBlock>("ResultText");

            Window.GetElementByName<MGButton>("ShowOkButton").OnLeftClicked += (sender, e) =>
                MGMessageBox.Show(Desktop, "Error", "Failed to open project:\nThe project file was not found.", OkLabels, 0, 0,
                    index => ShowAnswer("OK message", OkLabels, index));

            Window.GetElementByName<MGButton>("AskYesNoButton").OnLeftClicked += (sender, e) =>
                MGMessageBox.Show(Desktop, "Content Browser", "Delete 'HudScreen.xaml'?", YesNoLabels, 0, 1,
                    index => ShowAnswer("Yes / No", YesNoLabels, index));

            Window.GetElementByName<MGButton>("AskSaveButton").OnLeftClicked += (sender, e) =>
                MGMessageBox.Show(Desktop, "Close Screen", "Save changes to 'DialogueScreen' before closing?", SaveLabels, 0, 2,
                    index => ShowAnswer("Save question", SaveLabels, index));

            Window.GetElementByName<MGButton>("AskTwiceButton").OnLeftClicked += (sender, e) =>
                MGMessageBox.Show(Desktop, "Close Screen", "Save changes to 'HudScreen' before closing?", SaveLabels, 0, 2, first =>
                    MGMessageBox.Show(Desktop, "Close Screen", "Save changes to 'InventoryScreen' before closing?", SaveLabels, 0, 2,
                        second => _resultText.Text = $"HudScreen: {SaveLabels[first]}, InventoryScreen: {SaveLabels[second]}"));
        }

        private void ShowAnswer(string question, string[] labels, int index)
            => _resultText.Text = $"{question}: {labels[index]}";
    }
}
