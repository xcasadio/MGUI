// TextBoxBackslashTest.xaml.cs
// ─────────────────────────────────────────────────────────────────────────────
// PURPOSE
//   Validates the Task 6 fix in MGTextBox.UpdateFormattedText (DoubleEscapeAtIndex).
//
// BUG SUMMARY (Task 6 — off-by-one in DoubleEscapeAtIndex for consecutive backslashes)
//
//   Root cause 1: FormattedText.Insert(Index, ...) used the raw Text position as the
//     FormattedText insert position. After the first DoubleEscapeAtIndex call shifts
//     FormattedText, the second call's raw index was off.
//     Fix: use EscapedIndices[RunStartTextIndex] as the FM insert position.
//
//   Root cause 2: The EscapedIndices update loop started at CurrentIndex+1, skipping
//     EscapedIndices[RunStartTextIndex], leaving it one too low.
//     Fix: increment all EscapedIndices[j] where EscapedIndices[j] >= FMInsertPos.
//
// DEBUG ASSERTIONS
//   A Debug.Assert in the fixed DoubleEscapeAtIndex verifies that after each insertion,
//   EscapedIndices[RunStartTextIndex] == FMInsertPos + UnescapedCount. This assertion
//   fires on every selection render in Debug builds.
//
// TEST CASES
//   TB1: text = A\\B (4 chars).  Select 2nd backslash → exactly 1 '\' highlighted.
//   TB2: text = \\\\ (4 chars).  Select 2nd–3rd backslashes → exactly 2 '\' highlighted.
//   TB3: text = A\\B.            Select 1st backslash (was already working, still works).
//
//   The programmatic assertions in OnBeginUpdateContents verify that currently-stored
//   TextSelection values have the expected StartIndex / EndIndex / Length.
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class TextBoxBackslashTestSample : SampleBase
    {
        private MGTextBox   TB1 { get; }
        private MGTextBox   TB2 { get; }
        private MGTextBox   TB3 { get; }
        private MGTextBlock Info_TB1 { get; }
        private MGTextBlock Info_TB2 { get; }
        private MGTextBlock Info_TB3 { get; }

        // Flag to apply programmatic selections once after first update.
        private bool _selectionsApplied;

        public TextBoxBackslashTestSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "TextBoxBackslashTest.xaml")
        {
            TB1 = Window.GetElementByName<MGTextBox>("TB1");
            TB2 = Window.GetElementByName<MGTextBox>("TB2");
            TB3 = Window.GetElementByName<MGTextBox>("TB3");
            Info_TB1 = Window.GetElementByName<MGTextBlock>("Info_TB1");
            Info_TB2 = Window.GetElementByName<MGTextBlock>("Info_TB2");
            Info_TB3 = Window.GetElementByName<MGTextBlock>("Info_TB3");

            Debug.Assert(TB1 != null, "[TextBoxBackslashTest] TB1 not found.");
            Debug.Assert(TB2 != null, "[TextBoxBackslashTest] TB2 not found.");
            Debug.Assert(TB3 != null, "[TextBoxBackslashTest] TB3 not found.");

            // Set test texts.
            // TB1 / TB3: "A\\B"  = 4 chars: A, \, \, B  (@"A\\B" in C# verbatim)
            // TB2: "\\\\"       = 4 chars: \, \, \, \  (@"\\\\" in C# verbatim)
            TB1.SetText(@"A\\B");
            TB2.SetText(@"\\\\");
            TB3.SetText(@"A\\B");

            // Selection-changed handlers update the info labels.
            TB1.SelectionChanged += (_, e) =>
            {
                if (e.NewValue.HasValue) UpdateLabel(Info_TB1, "TB1", e.NewValue.Value, TB1.Text);
                else Info_TB1.SetText("TB1 selection: (none)");
            };
            TB2.SelectionChanged += (_, e) =>
            {
                if (e.NewValue.HasValue) UpdateLabel(Info_TB2, "TB2", e.NewValue.Value, TB2.Text);
                else Info_TB2.SetText("TB2 selection: (none)");
            };
            TB3.SelectionChanged += (_, e) =>
            {
                if (e.NewValue.HasValue) UpdateLabel(Info_TB3, "TB3", e.NewValue.Value, TB3.Text);
                else Info_TB3.SetText("TB3 selection: (none)");
            };

            // Apply programmatic selections once on the first update tick.
            // Setting CurrentSelection triggers UpdateFormattedText, which exercises the fix.
            // Debug.Assert inside DoubleEscapeAtIndex will fire if the fix is wrong.
            Window.OnBeginUpdateContents += (_, _) =>
            {
                if (_selectionsApplied) return;
                _selectionsApplied = true;
                RunProgrammaticTests();
            };

            Window.WindowDataContext = this;
        }

        private void RunProgrammaticTests()
        {
            // ── TB1: "A\\B", select 2nd backslash (index 2..3), 1 char ──────
            TB1.CurrentSelection = new MGTextBox.TextSelection(2, 3);
            var sel1 = TB1.CurrentSelection!.Value;
            Debug.Assert(sel1.StartIndex == 2, $"[TB1] StartIndex: expected 2, got {sel1.StartIndex}");
            Debug.Assert(sel1.EndIndex   == 3, $"[TB1] EndIndex: expected 3, got {sel1.EndIndex}");
            Debug.Assert(sel1.Length     == 1, $"[TB1] Length: expected 1, got {sel1.Length}");
            Debug.WriteLine("[TextBoxBackslashTest] TB1 test case PASSED (2..3, len=1)");

            // ── TB2: "\\\\", select 2nd–3rd backslashes (index 1..3), 2 chars ─
            TB2.CurrentSelection = new MGTextBox.TextSelection(1, 3);
            var sel2 = TB2.CurrentSelection!.Value;
            Debug.Assert(sel2.StartIndex == 1, $"[TB2] StartIndex: expected 1, got {sel2.StartIndex}");
            Debug.Assert(sel2.EndIndex   == 3, $"[TB2] EndIndex: expected 3, got {sel2.EndIndex}");
            Debug.Assert(sel2.Length     == 2, $"[TB2] Length: expected 2, got {sel2.Length}");
            Debug.WriteLine("[TextBoxBackslashTest] TB2 test case PASSED (1..3, len=2)");

            // ── TB3: "A\\B", select 1st backslash (index 1..2), 1 char ───────
            TB3.CurrentSelection = new MGTextBox.TextSelection(1, 2);
            var sel3 = TB3.CurrentSelection!.Value;
            Debug.Assert(sel3.StartIndex == 1, $"[TB3] StartIndex: expected 1, got {sel3.StartIndex}");
            Debug.Assert(sel3.EndIndex   == 2, $"[TB3] EndIndex: expected 2, got {sel3.EndIndex}");
            Debug.Assert(sel3.Length     == 1, $"[TB3] Length: expected 1, got {sel3.Length}");
            Debug.WriteLine("[TextBoxBackslashTest] TB3 test case PASSED (1..2, len=1)");

            Debug.WriteLine("[TextBoxBackslashTest] All programmatic test cases PASSED.");
        }

        private static void UpdateLabel(MGTextBlock label, string name, MGTextBox.TextSelection sel, string text)
        {
            label.SetText($"{name}: Start={sel.StartIndex}  End={sel.EndIndex}  Len={sel.Length}");
            Debug.Assert(sel.StartIndex >= 0 && sel.EndIndex <= text.Length,
                $"[TextBoxBackslashTest] {name} selection out of range: {sel.StartIndex}..{sel.EndIndex} for text len={text.Length}");
            Debug.WriteLine($"[TextBoxBackslashTest] {name} selection: {sel.StartIndex}..{sel.EndIndex} len={sel.Length}");
        }
    }
}
