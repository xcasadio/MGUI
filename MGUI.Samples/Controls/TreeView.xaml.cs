using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework;

namespace MGUI.Samples.Controls
{
    public class TreeViewSamples : SampleBase
    {
        public TreeViewSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "TreeView.xaml")
        {
            MGTreeView selectionTree = Window.GetElementByName<MGTreeView>("SelectionTree");
            MGTreeView largeIndentTree = Window.GetElementByName<MGTreeView>("LargeIndentTree");
            MGTextBlock selectionStatus = Window.GetElementByName<MGTextBlock>("SelectionStatus");

            var selectionBrush = new VisualStateFillBrush(new MGSolidFillBrush(new Color(214, 188, 88)));
            selectionTree.SelectionBackgroundBrush = selectionBrush;
            selectionTree.SelectionForeground = Color.Black;
            largeIndentTree.SelectionBackgroundBrush = selectionBrush;
            largeIndentTree.SelectionForeground = Color.Black;

            selectionTree.SelectionChanged += (sender, item) =>
            {
                string label = item?.Header?.ToString() ?? "none";
                selectionStatus.Text = $"Selected item: [b]{label}[/b]";
            };
        }
    }
}
