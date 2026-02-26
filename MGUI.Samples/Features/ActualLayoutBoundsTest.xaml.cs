// ActualLayoutBoundsTest.xaml.cs
// ─────────────────────────────────────────────────────────────────────────────
// PURPOSE
//   Validates the Task 5 fix in MGElement.Update:
//   Content children now receive ContentAreaBounds (ActualLayoutBounds shrunk by
//   the parent's Padding) instead of the full ActualLayoutBounds.
//
// FIX SUMMARY
//   OLD: UpdateContents(UA)  where UA.ActualLayoutBounds = parent.ActualLayoutBounds
//   NEW: UpdateContents(UAForContents)
//          where UAForContents.ActualLayoutBounds = Inset(parent.ActualLayoutBounds, parent.Padding)
//
//   Components (border, title bar, headers panel, etc.) still receive the full
//   unpadded bounds via Component.Update(UA).
//
//   The Debug.Assert added in Update() fires at runtime if ContentAreaBounds ever
//   exceeds ActualLayoutBounds, giving immediate feedback for regressions.
//
// VISUAL VERIFICATION
//   Scenario 1: A border with Padding=20 contains two buttons. Their
//       ActualLayoutBounds shown in the live info section should be offset by 20
//       from the border's bounds.
//   Scenario 2: Nested borders with Padding=16 and Padding=12. The innermost text
//       block's ActualLayoutBounds should reflect both levels of padding.
//   Scenario 3: MGListBox with a title component. Both the header (component) and
//       the list items (content children) should render correctly.
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class ActualLayoutBoundsTestSample : SampleBase
    {
        private MGBorder  PaddedBorder    { get; }
        private MGButton  BtnInPadded1    { get; }
        private MGButton  BtnInPadded2    { get; }
        private MGBorder  OuterBorder     { get; }
        private MGBorder  InnerBorder     { get; }
        private MGTextBlock InnerText     { get; }

        private MGTextBlock Info_PaddedBorder { get; }
        private MGTextBlock Info_BtnInPadded1 { get; }
        private MGTextBlock Info_BtnInPadded2 { get; }
        private MGTextBlock Info_OuterBorder  { get; }
        private MGTextBlock Info_InnerBorder  { get; }
        private MGTextBlock Info_InnerText    { get; }

        public ActualLayoutBoundsTestSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "ActualLayoutBoundsTest.xaml")
        {
            PaddedBorder = Window.GetElementByName<MGBorder>("PaddedBorder");
            BtnInPadded1 = Window.GetElementByName<MGButton>("BtnInPadded1");
            BtnInPadded2 = Window.GetElementByName<MGButton>("BtnInPadded2");
            OuterBorder  = Window.GetElementByName<MGBorder>("OuterBorder");
            InnerBorder  = Window.GetElementByName<MGBorder>("InnerBorder");
            InnerText    = Window.GetElementByName<MGTextBlock>("InnerText");

            Info_PaddedBorder = Window.GetElementByName<MGTextBlock>("Info_PaddedBorder");
            Info_BtnInPadded1 = Window.GetElementByName<MGTextBlock>("Info_BtnInPadded1");
            Info_BtnInPadded2 = Window.GetElementByName<MGTextBlock>("Info_BtnInPadded2");
            Info_OuterBorder  = Window.GetElementByName<MGTextBlock>("Info_OuterBorder");
            Info_InnerBorder  = Window.GetElementByName<MGTextBlock>("Info_InnerBorder");
            Info_InnerText    = Window.GetElementByName<MGTextBlock>("Info_InnerText");

            Debug.Assert(PaddedBorder != null, "[ActualLayoutBoundsTest] PaddedBorder not found.");
            Debug.Assert(BtnInPadded1 != null, "[ActualLayoutBoundsTest] BtnInPadded1 not found.");

            // Update live info labels every frame.
            Window.OnBeginUpdateContents += (sender, e) =>
            {
                Info_PaddedBorder.SetText(FormatBounds("PaddedBorder (pad=20)", PaddedBorder));
                Info_BtnInPadded1.SetText(FormatBounds("BtnInPadded1",          BtnInPadded1));
                Info_BtnInPadded2.SetText(FormatBounds("BtnInPadded2",          BtnInPadded2));
                Info_OuterBorder .SetText(FormatBounds("OuterBorder (pad=16)",  OuterBorder));
                Info_InnerBorder .SetText(FormatBounds("InnerBorder (pad=12)",  InnerBorder));
                Info_InnerText   .SetText(FormatBounds("InnerText (deep)",      InnerText));

                // Verify that each child's ActualLayoutBounds is contained within its parent's
                // ActualLayoutBounds (which is the parent's content-area bounds due to the fix).
                if (PaddedBorder.ActualLayoutBounds.Width > 0)
                {
                    Rectangle contentArea = new Rectangle(
                        PaddedBorder.ActualLayoutBounds.X + PaddedBorder.Padding.Left,
                        PaddedBorder.ActualLayoutBounds.Y + PaddedBorder.Padding.Top,
                        PaddedBorder.ActualLayoutBounds.Width  - PaddedBorder.Padding.Left - PaddedBorder.Padding.Right,
                        PaddedBorder.ActualLayoutBounds.Height - PaddedBorder.Padding.Top  - PaddedBorder.Padding.Bottom);

                    // Each button's actual bounds should be within the content area of PaddedBorder.
                    // (It may be clipped by scrolling, but must not exceed the content area.)
                    if (BtnInPadded1.ActualLayoutBounds.Width > 0)
                    {
                        bool btn1InContent = contentArea.Contains(BtnInPadded1.ActualLayoutBounds)
                                          || BtnInPadded1.ActualLayoutBounds.IsEmpty;
                        Debug.Assert(btn1InContent,
                            $"[ActualLayoutBoundsTest] BtnInPadded1.ActualLayoutBounds ({BtnInPadded1.ActualLayoutBounds}) " +
                            $"exceeds PaddedBorder content area ({contentArea})");
                    }
                }
            };

            Window.WindowDataContext = this;
        }

        private static string FormatBounds(string name, MGElement el)
        {
            if (el == null) return $"{name}: (null)";
            Rectangle b = el.ActualLayoutBounds;
            return $"{name}: ({b.X},{b.Y}) {b.Width}×{b.Height}  lay-pad={el.Padding}";
        }
    }
}
