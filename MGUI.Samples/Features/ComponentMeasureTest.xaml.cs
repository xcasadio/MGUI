// ComponentMeasureTest.xaml.cs
// ─────────────────────────────────────────────────────────────────────────────
// PURPOSE
//   Validates the Bug 1 fix in MGElement.MeasureSelf:
//   Shared component sizes now use element-wise MAX instead of SUM.
//
// BUG SUMMARY
//   OLD (broken): FullSize = Unshared + Max(comp1 + comp2 + ..., content)
//   NEW (fixed):  FullSize = Unshared + Max(Max(comp1, comp2, ...), content)
//
//   When two components both set IsWidthSharedWithContent=true (e.g. a title bar
//   and an inner border), they share the same space as the content region and
//   therefore should not be summed.
//
// ASSERTIONS
//   The Debug.Assert statements added directly in MGElement.MeasureSelf fire in
//   Debug builds whenever any element is measured, verifying that each component's
//   measured size does not exceed the available space given to it.
//
// VISUAL VERIFICATION
//   • MGListBox with title: header and item-list should share the same width.
//   • MGTabControl: tab-header row and content panel should be aligned correctly.
//   • MGTextBox with character counter: input field should not be clipped.
//   The live measurement labels at the bottom of the window confirm the sizes.
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class ComponentMeasureTestSample : SampleBase
    {
        private MGListBox<string> ListBox1     { get; }
        private MGListBox<string> ListBox2     { get; }
        private MGTabControl      TabControl   { get; }
        private MGTextBox         TextBox1     { get; }

        // Live info labels
        private MGTextBlock Info_LB1 { get; }
        private MGTextBlock Info_LB2 { get; }
        private MGTextBlock Info_TC  { get; }
        private MGTextBlock Info_TB1 { get; }

        public ComponentMeasureTestSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "ComponentMeasureTest.xaml")
        {
            ListBox1   = Window.GetElementByName<MGListBox<string>>("TestListBox1");
            ListBox2   = Window.GetElementByName<MGListBox<string>>("TestListBox2");
            TabControl = Window.GetElementByName<MGTabControl>("TestTabControl");
            TextBox1   = Window.GetElementByName<MGTextBox>("TestTextBox1");

            Info_LB1 = Window.GetElementByName<MGTextBlock>("Info_LB1");
            Info_LB2 = Window.GetElementByName<MGTextBlock>("Info_LB2");
            Info_TC  = Window.GetElementByName<MGTextBlock>("Info_TC");
            Info_TB1 = Window.GetElementByName<MGTextBlock>("Info_TB1");

            // Update the live measurement labels every frame so we can inspect
            // the real layout bounds of each test element after arrangement.
            Window.OnBeginUpdateContents += (sender, e) =>
            {
                Info_LB1.SetText(FormatBounds("ListBox1",   ListBox1));
                Info_LB2.SetText(FormatBounds("ListBox2",   ListBox2));
                Info_TC .SetText(FormatBounds("TabControl", TabControl));
                Info_TB1.SetText(FormatBounds("TextBox1",   TextBox1));
            };

            // Confirm that all elements were retrieved successfully.
            Debug.Assert(ListBox1   != null, "[ComponentMeasureTest] TestListBox1 not found in XAML.");
            Debug.Assert(ListBox2   != null, "[ComponentMeasureTest] TestListBox2 not found in XAML.");
            Debug.Assert(TabControl != null, "[ComponentMeasureTest] TestTabControl not found in XAML.");
            Debug.Assert(TextBox1   != null, "[ComponentMeasureTest] TestTextBox1 not found in XAML.");

            Window.WindowDataContext = this;
        }

        private static string FormatBounds(string name, MGElement el)
        {
            if (el == null)
            {
                return $"{name}: (null)";
            }

            Rectangle b = el.LayoutBounds;
            return $"{name}:  pos=({b.X},{b.Y})  size={b.Width}×{b.Height}  pad={el.Padding}";
        }
    }
}
