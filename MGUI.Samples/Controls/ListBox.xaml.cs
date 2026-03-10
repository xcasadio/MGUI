using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Shared.Helpers;

namespace MGUI.Samples.Controls
{
    public class ListBoxSamples : SampleBase
    {
        public ListBoxSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "ListBox.xaml")
        {
            MGListBox<string> ClickTestListBox = Window.GetElementByName<MGListBox<string>>("ClickTestListBox");
            MGTextBlock ClickStatus = Window.GetElementByName<MGTextBlock>("ListBoxClickStatus");
            MGTextBlock DoubleClickStatus = Window.GetElementByName<MGTextBlock>("ListBoxDoubleClickStatus");

            ClickTestListBox.SetItemsSource(new List<string>()
            {
                "Alpha",
                "Bravo",
                "Charlie",
                "Delta",
                "Echo",
                "Foxtrot"
            });

            ClickTestListBox.SelectionChanged += (sender, selectedItems) =>
            {
                string SelectedValue = selectedItems.FirstOrDefault()?.Data ?? "none";
                string NewText = $"Last click: [b]{SelectedValue}[/b]";
                ClickStatus.SetText(NewText, NewText.Length == ClickStatus.Text.Length);
            };

            ClickTestListBox.MouseHandler.LMBDoubleClickedInside += (sender, e) =>
            {
                string ClickedValue = ClickTestListBox.ReleasedItem?.Data ?? ClickTestListBox.SelectedValue ?? "none";
                string NewText = $"Last double-click: [b]{ClickedValue}[/b]";
                DoubleClickStatus.SetText(NewText, NewText.Length == DoubleClickStatus.Text.Length);
            };
        }
    }
}
