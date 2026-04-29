using MGUI.Core.UI;
using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;

namespace MGUI.Samples.Features;

public sealed class EditorRichTextBoxSample : SampleBase
{
    private const string InitialText = "using System;\n\nnamespace Demo\n{\n    public cla\n}";

    private readonly MGRichTextBox _editor;
    private readonly MGListBox<string> _completionList;
    private readonly MGTextBlock _statusText;

    public EditorRichTextBoxSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "EditorRichTextBox.xaml")
    {
        ApplyScenarioId("SCN-EDITOR-RTB-001");

        _editor = Window.GetElementByName<MGRichTextBox>("EditorTextBox");
        _completionList = Window.GetElementByName<MGListBox<string>>("CompletionList");
        _statusText = Window.GetElementByName<MGTextBlock>("StatusText");

        _editor.SyntaxHighlighter = new CSharpRichTextSyntaxHighlighter();
        _editor.CompletionProvider = new CSharpKeywordCompletionProvider();
        _editor.SetText(InitialText);
        _editor.RefreshSyntaxHighlighting();

        Window.GetElementByName<MGButton>("CompleteButton").AddCommandHandler((_, __) => RefreshCompletions());
        Window.GetElementByName<MGButton>("PreviousCompletionButton").AddCommandHandler((_, __) => MoveCompletion(-1));
        Window.GetElementByName<MGButton>("NextCompletionButton").AddCommandHandler((_, __) => MoveCompletion(1));
        Window.GetElementByName<MGButton>("AcceptCompletionButton").AddCommandHandler((_, __) => AcceptCompletion());
        Window.GetElementByName<MGButton>("ResetTextButton").AddCommandHandler((_, __) => ResetText());

        RefreshCompletions();
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
        _editor.SetText(InitialText);
        _editor.RefreshSyntaxHighlighting();
        RefreshCompletions();
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

        _statusText.SetText($"Text { _editor.Text.Length } chars | completions { labels.Count }");
    }
}