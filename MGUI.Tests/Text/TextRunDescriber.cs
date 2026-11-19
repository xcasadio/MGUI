using System.Globalization;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Text;

namespace MGUI.Tests.Text;

/// <summary>Turns runs and lines into text that says everything about them, so two pipelines are compared on every field (a brush by its type and its colors).</summary>
internal static class TextRunDescriber
{
    public static string Describe(IFillBrush brush) => brush switch
    {
        null => "null",
        MGSolidFillBrush solid => $"solid({solid.Color})",
        MGGradientFillBrush gradient => $"gradient({gradient.TopLeftColor},{gradient.TopRightColor},{gradient.BottomRightColor},{gradient.BottomLeftColor})",
        _ => brush.GetType().Name
    };

    public static string Describe(MGTextRunConfig settings)
        => $"bold={settings.IsBold} italic={settings.IsItalic} opacity={settings.Opacity.ToString("R", CultureInfo.InvariantCulture)} fg={settings.Foreground?.ToString() ?? "null"} " +
           $"underline=({settings.Underline.IsEnabled},{settings.Underline.Height},{settings.Underline.VerticalOffset},{Describe(settings.Underline.Brush)}) " +
           $"background=({Describe(settings.Background.Brush)},{settings.Background.Padding.Left},{settings.Background.Padding.Top},{settings.Background.Padding.Right},{settings.Background.Padding.Bottom}) " +
           $"shadow=({settings.Shadow.ShadowColor?.ToString() ?? "null"},{settings.Shadow.ShadowOffset?.ToString() ?? "null"})";

    public static string Describe(MGTextRun run) => run switch
    {
        MGTextRunText text => $"Text[{Escape(text.Text)}|{Describe(text.Settings)}|tt={text.ToolTipId ?? "null"}|action={text.ActionId ?? "null"}]",
        MGTextRunLineBreak lineBreak => $"LineBreak[{lineBreak.LineBreakCharacterCount}|tt={lineBreak.ToolTipId ?? "null"}|action={lineBreak.ActionId ?? "null"}]",
        MGTextRunImage image => $"Image[{image.SourceName}|{image.TargetWidth}x{image.TargetHeight}|tt={image.ToolTipId ?? "null"}|action={image.ActionId ?? "null"}]",
        _ => run.GetType().Name
    };

    public static List<string> Describe(IEnumerable<MGTextRun> runs) => runs?.Select(Describe).ToList();

    public static List<string> Describe(IEnumerable<MGTextLine> lines)
        => lines?.Select(x => $"Line#{x.LineNumber}[w={x.LineWidth.ToString("R", CultureInfo.InvariantCulture)} th={x.LineTextHeight.ToString("R", CultureInfo.InvariantCulture)} ih={x.LineImageHeight.ToString("R", CultureInfo.InvariantCulture)} " +
                                   $"h={x.LineTotalHeight.ToString("R", CultureInfo.InvariantCulture)} eol={x.EndsInLinebreakCharacter} idx={string.Join(",", x.OriginalCharacterIndices)}|{string.Join(";", x.Runs.Select(Describe))}]").ToList();

    public static string Escape(string text) => text?.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
