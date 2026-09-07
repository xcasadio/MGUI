using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Completeness guard for ADR-0005's scalar pilot migration: scans <c>MGUI.Core</c> for every source line that
/// assigns one of the five scalar pilot properties (<c>Padding</c>, <c>Margin</c>, <c>MinHeight</c>,
/// <c>BorderBrush</c>, <c>BorderThickness</c>) — the same pattern the plan's discovery used
/// (<c>rg -n "\b(Padding|Margin|MinHeight|BorderBrush|BorderThickness)\s*=[^=]" MGUI.Core --type cs</c>) — and
/// fails when a hit is neither on an allow-list (a whole file, or a line matching a named pattern, each carrying
/// a reason) nor in the explicit pending-migration list below. The pending list is this slice's (S2) inventory
/// of every remaining framework write of a scalar pilot outside the pilot setters and their facades; S3 migrates
/// the template catalogue (reserved below) and a later pass migrates the pending sites, shrinking that list to
/// empty. Nothing may go unclassified in between: a new, unlisted write fails this test immediately.
/// </summary>
public class ResolvedPilotWriteSitesTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string CoreRoot = Path.Combine(RepoRoot, "MGUI.Core");

    private static readonly Regex PilotAssignmentPattern =
        new(@"\b(Padding|Margin|MinHeight|BorderBrush|BorderThickness)\s*=[^=]", RegexOptions.Compiled);

    /// <summary>A facade forwards its own setter's incoming <c>value</c> parameter, unmodified, straight to a
    /// child's public <c>BorderBrush</c>/<c>BorderThickness</c> setter (which is itself LocalValue, ADR-0005) —
    /// this is the application's entry point, not a framework-classified write, on every one of the ~50 sites
    /// that follow this shape across both the composite controls (<c>MGButton.BorderBrush</c>, ...) and the XAML
    /// DTOs (already whole-file allow-listed below, but the pattern also matches there, harmlessly).</summary>
    private static readonly Regex FacadeForwardingPattern =
        new(@"\.(BorderBrush|BorderThickness)\s*=\s*value\s*;", RegexOptions.Compiled);

    /// <summary>(relative path, reason) — every pilot-shaped hit anywhere in the file is allowed.</summary>
    private static readonly (string File, string Reason)[] AllowedWholeFiles =
    {
        (@"MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs",
            "S3: template catalogue lambdas (ApplyThemeDefault/ApplyTemplateValue), reserved for the next slice"),
        (@"MGUI.Core\UI\XAML\Controls.cs", "XAML DTO properties, not MGElement instances"),
        (@"MGUI.Core\UI\XAML\Containers.cs", "XAML DTO properties, not MGElement instances"),
        (@"MGUI.Core\UI\XAML\Lists.cs", "XAML DTO properties, not MGElement instances"),
        (@"MGUI.Core\UI\XAML\Element.cs", "XAML DTO properties, not MGElement instances"),
        (@"MGUI.Core\UI\XAML\Brushes.cs", "XAML DTO properties, not MGElement instances"),
        (@"MGUI.Core\UI\MGTheme.cs", "theme data, not MGElement instances"),
        (@"MGUI.Core\UI\XAML\ThemeDefinitionBuilder.cs", "theme definition DTO builder, not MGElement instances"),
        (@"MGUI.Core\UI\Brushes\Fill Brushes\MGPaddedFillBrush.cs", "brush-internal property sharing the pilot's name"),
        (@"MGUI.Core\UI\Brushes\Fill Brushes\MGBorderedFillBrush.cs", "brush-internal property sharing the pilot's name"),
        (@"MGUI.Core\UI\Brushes\Fill Brushes\MGNineSliceFillBrush.cs", "brush-internal property sharing the pilot's name"),
        (@"MGUI.Core\UI\Containers\Grids\GridDimension.cs", "grid sizing DTO, not an MGElement pilot"),
        (@"MGUI.Core\UI\Text\FormattedTextTokenizer.cs", "text layout internals sharing the pilot's name"),
        (@"MGUI.Core\UI\Text\TextRenderInfo.cs", "text layout internals sharing the pilot's name"),
        (@"MGUI.Core\UI\Text\MGTextRun.cs", "text layout internals sharing the pilot's name"),
        (@"MGUI.Core\UI\MGElement.cs", "the pilot setters/tagged setters themselves (SetMargin, SetPadding, SetMinHeight, ApplyXxxEffective)"),
        (@"MGUI.Core\UI\MGBorder.cs", "the pilot setters/tagged setters themselves (SetBorderBrush, SetBorderThickness, ApplyXxxEffective)"),
    };

    /// <summary>(relative path, 1-based line, reason) — a specific line is allowed regardless of file.</summary>
    private static readonly (string File, int Line, string Reason)[] AllowedLines =
    {
        (@"MGUI.Core\UI\MGContextMenu.cs", 528, "local variable `int MinHeight`, not the MGElement.MinHeight pilot"),
        (@"MGUI.Core\UI\MGDesktop.cs", 812, "local variable `int MinHeight`, not the MGElement.MinHeight pilot"),
        (@"MGUI.Core\UI\MGWindow.cs", 238, "method parameter default value (`int MinHeight = 100`), not a pilot write"),
        (@"MGUI.Core\UI\MGWindow.cs", 256, "method parameter default value (`int MinHeight = 50`), not a pilot write"),
        (@"MGUI.Core\UI\MGChatBox.cs", 145, "commented-out code"),
        (@"MGUI.Core\UI\MGScrollViewer.cs", 567, "commented-out code"),
        (@"MGUI.Core\UI\MGXAMLDesigner.cs", 70, "inside a verbatim string literal (sample XAML shown in the designer UI), not code"),
    };

    /// <summary>
    /// S2 inventory of every remaining framework write of a scalar pilot outside the pilot setters/facades and
    /// outside the template catalogue (reserved to S3): (relative path, 1-based line, intended source per the
    /// plan's classification rule, rationale). Migrating a site here to its tagged setter and removing its row
    /// is the S3+ second agent's job; this test only guarantees no NEW untagged write appears unnoticed.
    /// </summary>
    private static readonly (string File, int Line, string IntendedSource, string Rationale)[] PendingMigration =
    {
        // BLOCKED: this is not the MGElement/MGBorder pilot at all. MGBoundsAdorner.BorderThickness (int, drawn by
        // hand in OnDrawContents) is the adorner's own unrelated property; ADR-0005 scopes the resolved-value store
        // to MGElement/MGBorder, and adorners never go through GetBorder(). The regex text-matches the identifier
        // but there is no Thickness-typed pilot here to tag, so this line is intentionally left unmigrated.
        (@"MGUI.Core\UI\Docking\Controls\MGDockPreviewOverlay.cs", 78, "BLOCKED", "BLOCKED: MGBoundsAdorner.BorderThickness is an int drawn by hand, not the MGElement/MGBorder Thickness pilot; out of the S2 store surface"),
    };

    [Fact]
    public void Every_Scalar_Pilot_Write_In_MGUI_Core_Is_Classified()
    {
        Assert.True(Directory.Exists(CoreRoot), $"MGUI.Core not found at {CoreRoot}");

        Dictionary<string, string> wholeFileAllow = AllowedWholeFiles.ToDictionary(x => Normalize(x.File), x => x.Reason, StringComparer.OrdinalIgnoreCase);
        HashSet<(string File, int Line)> lineAllow = AllowedLines.Select(x => (Normalize(x.File), x.Line)).ToHashSet();
        HashSet<(string File, int Line)> pending = PendingMigration.Select(x => (Normalize(x.File), x.Line)).ToHashSet();

        // The pending list must itself be duplicate-free: a repeated (file, line) entry would silently hide a
        // second, differently-classified write on the same line.
        Assert.Equal(PendingMigration.Length, pending.Count);

        List<string> unclassified = new();

        foreach (string filePath in Directory.GetFiles(CoreRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || filePath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = Normalize(Path.GetRelativePath(CoreRoot, filePath));
            string fullRelative = Normalize(Path.Combine("MGUI.Core", relative));

            if (wholeFileAllow.ContainsKey(fullRelative))
                continue;

            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!PilotAssignmentPattern.IsMatch(line))
                    continue;

                int lineNumber = i + 1;

                if (FacadeForwardingPattern.IsMatch(line))
                    continue;

                if (lineAllow.Contains((fullRelative, lineNumber)))
                    continue;

                if (pending.Contains((fullRelative, lineNumber)))
                    continue;

                unclassified.Add($"{fullRelative}:{lineNumber}: {line.Trim()}");
            }
        }

        Assert.True(unclassified.Count == 0,
            "Unclassified scalar pilot write(s) found — add each to AllowedWholeFiles/AllowedLines (with a reason) " +
            "or to PendingMigration (with an intended source) in ResolvedPilotWriteSitesTests:\n" + string.Join("\n", unclassified));
    }

    /// <summary>Every entry in <see cref="PendingMigration"/> must still correspond to an actual pilot-shaped
    /// line at that (file, line) — this catches a stale entry left behind after an edit shifted line numbers.</summary>
    [Fact]
    public void PendingMigration_Entries_Still_Point_At_A_Pilot_Assignment()
    {
        List<string> stale = new();
        foreach ((string file, int line, _, _) in PendingMigration)
        {
            string fullPath = Path.Combine(RepoRoot, file);
            if (!File.Exists(fullPath))
            {
                stale.Add($"{file}:{line} (file not found)");
                continue;
            }

            string[] lines = File.ReadAllLines(fullPath);
            if (line < 1 || line > lines.Length || !PilotAssignmentPattern.IsMatch(lines[line - 1]))
                stale.Add($"{file}:{line} (no pilot assignment at that line any more)");
        }

        Assert.True(stale.Count == 0, "Stale PendingMigration entries (line numbers drifted or already migrated):\n" + string.Join("\n", stale));
    }

    private static string Normalize(string path) => path.Replace('/', '\\');
}
