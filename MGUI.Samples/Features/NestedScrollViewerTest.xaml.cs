// NestedScrollViewerTest.xaml.cs
// ─────────────────────────────────────────────────────────────────────────────
// PURPOSE
//   Validates that MGGrid and MGUniformGrid cell-selection works correctly
//   when the grids are nested inside multiple MGScrollViewer instances.
//
// VIEWPORT FORMULA ANALYSIS   (see also the XAML file header comment)
//   The formula used in MGGrid.UpdateSelection / MGUniformGrid.UpdateSelection:
//       Viewport = SV.ContentViewport.GetTranslated(this.Origin - SV.Origin)
//
//   • SV.ContentViewport  = viewport rectangle in layout-coordinates, set once
//                           during Arrange (not updated on scroll).
//   • this.Origin         = UA.Offset accumulated from ALL ancestor SVs.
//   • SV.Origin           = UA.Offset accumulated from SV's own ancestors.
//   → Origin - SV.Origin  = this SV's own scroll-offset only.
//
//   For two levels (OuterSV > InnerSV > Grid):
//       Grid.Origin    = OuterOffset + InnerOffset
//       InnerSV.Origin = OuterOffset
//       Δ              = InnerOffset                    ← InnerSV's own scroll
//
//   The formula is therefore CORRECT for arbitrary nesting depth.
//   OuterSV clips mouse input to its viewport, so clicks that reach the Grid
//   are already constrained to the outer bounds — no extra intersection needed.
//
// TESTED SCENARIOS
//   Case 1 — single ScrollViewer + Grid       (baseline row-selection)
//   Case 2 — two nested SVs + Grid           (vertical outer, horizontal inner)
//   Case 3 — two nested SVs + UniformGrid    (same topology, different grid type)
//
// ASSERTIONS
//   • Clicking a cell that is visually scrolled off-screen MUST NOT produce
//     a selection (the click can't reach the grid in that case, which is
//     validated indirectly by observing that no unexpected selection label
//     appears).
//   • After each SelectionChanged event, Debug.Assert verifies the selection
//     is non-null and has at least one cell, i.e. the selection data is
//     internally consistent.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Diagnostics;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class NestedScrollViewerTestSample : SampleBase
    {
        // ── Case 1: single SV + Grid ─────────────────────────────────────────
        private MGScrollViewer Case1SV       { get; }
        private MGGrid         Case1Grid     { get; }
        private MGTextBlock    Case1Label    { get; }

        // ── Case 2: outer SV > inner SV > Grid ───────────────────────────────
        private MGScrollViewer Case2OuterSV  { get; }
        private MGScrollViewer Case2InnerSV  { get; }
        private MGGrid         Case2Grid     { get; }
        private MGTextBlock    Case2Label    { get; }

        // ── Case 3: outer SV > inner SV > UniformGrid ────────────────────────
        private MGScrollViewer Case3OuterSV  { get; }
        private MGScrollViewer Case3InnerSV  { get; }
        private MGUniformGrid  Case3UGrid    { get; }
        private MGTextBlock    Case3Label    { get; }

        // ── Viewport display labels ───────────────────────────────────────────
        private MGTextBlock VP_Case1SV      { get; }
        private MGTextBlock VP_Case2OuterSV { get; }
        private MGTextBlock VP_Case2InnerSV { get; }
        private MGTextBlock VP_Case3OuterSV { get; }
        private MGTextBlock VP_Case3InnerSV { get; }

        public NestedScrollViewerTestSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "NestedScrollViewerTest.xaml")
        {
            // ── Retrieve elements ─────────────────────────────────────────────
            Case1SV      = Window.GetElementByName<MGScrollViewer>("Case1SV");
            Case1Grid    = Window.GetElementByName<MGGrid>("Case1Grid");
            Case1Label   = Window.GetElementByName<MGTextBlock>("Case1SelectionLabel");

            Case2OuterSV = Window.GetElementByName<MGScrollViewer>("Case2OuterSV");
            Case2InnerSV = Window.GetElementByName<MGScrollViewer>("Case2InnerSV");
            Case2Grid    = Window.GetElementByName<MGGrid>("Case2Grid");
            Case2Label   = Window.GetElementByName<MGTextBlock>("Case2SelectionLabel");

            Case3OuterSV = Window.GetElementByName<MGScrollViewer>("Case3OuterSV");
            Case3InnerSV = Window.GetElementByName<MGScrollViewer>("Case3InnerSV");
            Case3UGrid   = Window.GetElementByName<MGUniformGrid>("Case3UGrid");
            Case3Label   = Window.GetElementByName<MGTextBlock>("Case3SelectionLabel");

            VP_Case1SV      = Window.GetElementByName<MGTextBlock>("VP_Case1SV");
            VP_Case2OuterSV = Window.GetElementByName<MGTextBlock>("VP_Case2OuterSV");
            VP_Case2InnerSV = Window.GetElementByName<MGTextBlock>("VP_Case2InnerSV");
            VP_Case3OuterSV = Window.GetElementByName<MGTextBlock>("VP_Case3OuterSV");
            VP_Case3InnerSV = Window.GetElementByName<MGTextBlock>("VP_Case3InnerSV");

            // ── Case 1: single-SV + Grid selection assertions ─────────────────
            Case1Grid.SelectionChanged += (sender, selection) =>
            {
                if (selection.HasValue)
                {
                    int rowIndex = Case1Grid.GetRowIndex(selection.Value.Cell.Row);
                    string msg = $"Selected Row: {rowIndex}";
                    Case1Label.SetText(msg);

                    // Assert that the selection contains at least one cell
                    int cellCount = 0;
                    foreach (var _ in selection.Value)
                    {
                        cellCount++;
                    }

                    Debug.Assert(cellCount > 0,
                        "[NestedSVTest Case1] SelectionChanged fired but selection contains 0 cells.");
                    Debug.WriteLine($"[NestedSVTest Case1] {msg} ({cellCount} cells)");
                }
                else
                {
                    Case1Label.SetText("Selected Row: (none)");
                    Debug.WriteLine("[NestedSVTest Case1] Selection cleared.");
                }
            };

            // ── Case 2: nested SVs + Grid selection assertions ────────────────
            Case2Grid.SelectionChanged += (sender, selection) =>
            {
                if (selection.HasValue)
                {
                    int rowIndex = Case2Grid.GetRowIndex(selection.Value.Cell.Row);
                    string msg = $"Selected Row: {rowIndex}";
                    Case2Label.SetText(msg);

                    int cellCount = 0;
                    foreach (var _ in selection.Value)
                    {
                        cellCount++;
                    }

                    Debug.Assert(cellCount > 0,
                        "[NestedSVTest Case2] SelectionChanged fired but selection contains 0 cells.");

                    // Assert that the selected row index makes sense (0-based, within grid bounds)
                    Debug.Assert(rowIndex >= 0 && rowIndex < Case2Grid.Rows.Count,
                        $"[NestedSVTest Case2] Row index {rowIndex} is out of range [0, {Case2Grid.Rows.Count}).");
                    Debug.WriteLine($"[NestedSVTest Case2] {msg} ({cellCount} cells). " +
                        $"OuterSV.VerticalOffset={Case2OuterSV.VerticalOffset:F0} " +
                        $"InnerSV.HorizontalOffset={Case2InnerSV.HorizontalOffset:F0}");
                }
                else
                {
                    Case2Label.SetText("Selected Row: (none)");
                    Debug.WriteLine("[NestedSVTest Case2] Selection cleared.");
                }
            };

            // ── Case 3: nested SVs + UniformGrid selection assertions ──────────
            Case3UGrid.SelectionChanged += (sender, selection) =>
            {
                if (selection.HasValue)
                {
                    GridCellIndex cell = selection.Value.Cell;
                    string msg = $"Selected Cell: Row={cell.Row}, Col={cell.Column}";
                    Case3Label.SetText(msg);

                    int cellCount = 0;
                    foreach (var _ in selection.Value)
                    {
                        cellCount++;
                    }

                    Debug.Assert(cellCount > 0,
                        "[NestedSVTest Case3] SelectionChanged fired but selection contains 0 cells.");

                    // Assert cell indices are within grid bounds
                    Debug.Assert(cell.Row >= 0 && cell.Row < Case3UGrid.Rows,
                        $"[NestedSVTest Case3] Row {cell.Row} out of range [0, {Case3UGrid.Rows}).");
                    Debug.Assert(cell.Column >= 0 && cell.Column < Case3UGrid.Columns,
                        $"[NestedSVTest Case3] Column {cell.Column} out of range [0, {Case3UGrid.Columns}).");

                    Debug.WriteLine($"[NestedSVTest Case3] {msg}. " +
                        $"OuterSV.VerticalOffset={Case3OuterSV.VerticalOffset:F0} " +
                        $"InnerSV.HorizontalOffset={Case3InnerSV.HorizontalOffset:F0}");
                }
                else
                {
                    Case3Label.SetText("Selected Cell: (none)");
                    Debug.WriteLine("[NestedSVTest Case3] Selection cleared.");
                }
            };

            // ── Live viewport bounds display ──────────────────────────────────
            // Updated every frame so the user can watch how viewport bounds
            // and Origin change as they scroll each ScrollViewer.
            Window.OnBeginUpdateContents += (sender, e) =>
            {
                VP_Case1SV.SetText(FormatViewport("Case1 SV",
                    Case1SV.ContentViewport, Case1SV.Origin, Case1SV.VerticalOffset, Case1SV.HorizontalOffset));

                VP_Case2OuterSV.SetText(FormatViewport("Case2 Outer",
                    Case2OuterSV.ContentViewport, Case2OuterSV.Origin, Case2OuterSV.VerticalOffset, Case2OuterSV.HorizontalOffset));

                VP_Case2InnerSV.SetText(FormatViewport("Case2 Inner",
                    Case2InnerSV.ContentViewport, Case2InnerSV.Origin, Case2InnerSV.VerticalOffset, Case2InnerSV.HorizontalOffset));

                VP_Case3OuterSV.SetText(FormatViewport("Case3 Outer",
                    Case3OuterSV.ContentViewport, Case3OuterSV.Origin, Case3OuterSV.VerticalOffset, Case3OuterSV.HorizontalOffset));

                VP_Case3InnerSV.SetText(FormatViewport("Case3 Inner",
                    Case3InnerSV.ContentViewport, Case3InnerSV.Origin, Case3InnerSV.VerticalOffset, Case3InnerSV.HorizontalOffset));
            };

            Window.WindowDataContext = this;
        }

        private static string FormatViewport(string label, Rectangle viewport, Point origin, float vOffset, float hOffset)
            => $"{label}: VP=({viewport.X},{viewport.Y} {viewport.Width}×{viewport.Height})  " +
               $"Origin=({origin.X},{origin.Y})  Scroll=({hOffset:F0},{vOffset:F0})";
    }
}
