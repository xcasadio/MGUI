using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;

namespace MGUI.Samples.Features;

public sealed class EditorRichTextBoxSample : SampleBase
{
    private const string CompletionDemoPrefix = "public cla";
    private const string CompletionDemoText = "using System;\n\nnamespace Demo\n{\n    public cla\n}";

    private readonly MGOverlayPanel _rootPanel;
    private readonly MGRichTextBox _editor;
    private readonly MGListBox<string> _completionList;
    private readonly MGTextBlock _statusText;
    private string _themeName = "Dark";

    public EditorRichTextBoxSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "EditorRichTextBox.xaml")
    {
        ApplyScenarioId("SCN-EDITOR-RTB-001");

        _rootPanel = Window.GetElementByName<MGOverlayPanel>("RootPanel");
        _editor = Window.GetElementByName<MGRichTextBox>("EditorTextBox");
        _completionList = Window.GetElementByName<MGListBox<string>>("CompletionList");
        _statusText = Window.GetElementByName<MGTextBlock>("StatusText");

        ApplyEditorTheme(EditorTheme.Dark);

        _editor.SyntaxHighlighter = new CSharpRichTextSyntaxHighlighter();
        _editor.CompletionProvider = new CSharpKeywordCompletionProvider();
        SetCompletionDemoText();

        Window.GetElementByName<MGButton>("DarkThemeButton").AddCommandHandler((_, __) => ApplyEditorTheme(EditorTheme.Dark));
        Window.GetElementByName<MGButton>("BlueThemeButton").AddCommandHandler((_, __) => ApplyEditorTheme(EditorTheme.Blue));
        Window.GetElementByName<MGButton>("CompleteButton").AddCommandHandler((_, __) => RefreshCompletions());
        Window.GetElementByName<MGButton>("PreviousCompletionButton").AddCommandHandler((_, __) => MoveCompletion(-1));
        Window.GetElementByName<MGButton>("NextCompletionButton").AddCommandHandler((_, __) => MoveCompletion(1));
        Window.GetElementByName<MGButton>("AcceptCompletionButton").AddCommandHandler((_, __) => AcceptCompletion());
        Window.GetElementByName<MGButton>("ResetTextButton").AddCommandHandler((_, __) => ResetText());

        RefreshCompletions();
    }

    private enum EditorTheme
    {
        Dark,
        Blue
    }

    private void ApplyEditorTheme(EditorTheme theme)
    {
        if (theme == EditorTheme.Blue)
        {
            ApplyEditorTheme(
                themeName: "Blue",
                rootBackground: new Color(9, 32, 56),
                editorBackground: new Color(10, 43, 74),
                editorBorder: new Color(55, 139, 214),
                textForeground: new Color(218, 235, 250),
                selectionBackground: new Color(29, 96, 154),
                palette: new MGRichTextSyntaxPalette
                {
                    Keyword = new Color(116, 192, 255),
                    String = new Color(255, 198, 128),
                    Comment = new Color(115, 221, 156),
                    Number = new Color(197, 220, 148),
                    TypeName = new Color(84, 230, 214)
                });
            return;
        }

        ApplyEditorTheme(
            themeName: "Dark",
            rootBackground: new Color(24, 28, 34),
            editorBackground: new Color(18, 22, 28),
            editorBorder: new Color(72, 82, 96),
            textForeground: new Color(215, 220, 228),
            selectionBackground: new Color(38, 79, 120),
            palette: MGRichTextSyntaxPalette.Default);
    }

    private void ApplyEditorTheme(string themeName, Color rootBackground, Color editorBackground, Color editorBorder, Color textForeground,
        Color selectionBackground, MGRichTextSyntaxPalette palette)
    {
        _themeName = themeName;
        _rootPanel.BackgroundBrush.SetAll(rootBackground.AsFillBrush());
        _editor.BackgroundBrush.SetAll(editorBackground.AsFillBrush());
        _editor.BackgroundBrush.FocusedColor = null;
        _editor.BorderComponent.Element.BackgroundBrush.SetAll(editorBackground.AsFillBrush());
        _editor.BorderComponent.Element.BackgroundBrush.FocusedColor = null;
        _editor.BorderComponent.Element.BorderBrush = editorBorder.AsFillBrush().AsUniformBorderBrush();
        _editor.DefaultTextForeground.SetAll(textForeground);
        _editor.TextBlockComponent.Element.Foreground.SetAll(textForeground);
        _editor.FocusedSelectionBackgroundColor = selectionBackground;
        _editor.UnfocusedSelectionBackgroundColor = selectionBackground * 0.85f;
        _editor.SyntaxPalette = palette;
        _editor.RefreshSyntaxHighlighting();
        _statusText?.SetText($"Theme {_themeName} | Text {_editor.Text.Length} chars");
    }

    private void RefreshCompletions()
    {
        MGRichTextCompletionResult result = _editor.RequestCompletions();
        _editor.CompletionPopup.Open(result);
        SyncCompletionList();
    }

    private void MoveCompletion(int delta)
    {
        _editor.MoveCompletionSelection(delta);
        SyncCompletionList();
    }

    private void AcceptCompletion()
    {
        if (_editor.AcceptSelectedCompletion())
        {
            _editor.RefreshSyntaxHighlighting();
        }

        RefreshCompletions();
    }

    private void ResetText()
    {
        SetCompletionDemoText();
        RefreshCompletions();
    }

    private void SetCompletionDemoText()
    {
        _editor.SetText(CompletionDemoText);
        _editor.CaretIndex = CompletionDemoText.IndexOf(CompletionDemoPrefix, System.StringComparison.Ordinal) + CompletionDemoPrefix.Length;
        _editor.RefreshSyntaxHighlighting();
    }

    private void SyncCompletionList()
    {
        List<string> labels = new();
        MGRichTextCompletionResult result = _editor.CompletionPopup.Result;
        for (int index = 0; index < result.Items.Count; index++)
        {
            MGRichTextCompletionItem item = result.Items[index];
            labels.Add(item.Kind == null ? item.Label : $"{item.Label}  {item.Kind}");
        }

        _completionList.SetItemsSource(labels);
        if (_editor.CompletionPopup.SelectedItem != null && labels.Count > 0)
        {
            int selectedIndex = _editor.CompletionPopup.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < labels.Count)
            {
                _completionList.SelectItem(labels[selectedIndex], true);
            }
        }

        _statusText.SetText($"Theme {_themeName} | Text { _editor.Text.Length } chars | completions { labels.Count }");
    }
}