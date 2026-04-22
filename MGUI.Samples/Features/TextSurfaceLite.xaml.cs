using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace MGUI.Samples.Features;

public class TextSurfaceLiteSamples : SampleBase
{
    private readonly MGTextBlock _annotatedText;
    private readonly MGTextLogView _logView;
    private readonly MGChatBox _chatBox;
    private int _annotationVersion = 1;
    private int _warningCount = 1;
    private int _chatMessageCount = 1;

    public TextSurfaceLiteSamples(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "TextSurfaceLite.xaml")
    {
        ApplyScenarioId("SCN-TEXT-002");

        MGContentPresenter annotatedHost = Window.GetElementByName<MGContentPresenter>("AnnotatedTextHost");
        MGContentPresenter logHost = Window.GetElementByName<MGContentPresenter>("LogHost");
        MGContentPresenter chatHost = Window.GetElementByName<MGContentPresenter>("ChatHost");

        _annotatedText = new(Window, string.Empty)
        {
            WrapText = true,
            MinHeight = 100,
            Padding = new Thickness(4)
        };
        _annotatedText.SetTextRuns(BuildAnnotatedRuns());

        _logView = new(Window, 8)
        {
            MinHeight = 170,
            AllowsInlineFormatting = true,
            ShowTimestamps = true,
            TimestampFormat = @"'\\['HH:mm:ss']'"
        };
        _logView.AppendEntry("[color=#6ad6ff]Boot[/color] Targeted text surfaces initialized.", "TRACE");
        _logView.AppendEntry("[color=#ffd166]Scope[/color] Rich chat, log and debug cases stay out of document-editor territory.", "WARN");
        _logView.AppendEntry("[color=#80ed99]OK[/color] Explicit runs can now bypass markup parsing when content is already structured.", "INFO");

        _chatBox = new(Window, 180, 8)
        {
            AllowsMessageInlineFormatting = true,
            TimestampFormat = @"'\\['HH:mm:ss']'",
            MinHeight = 170
        };
        _chatBox.Messages.Add(new("ToolingBot", DateTime.Now.AddSeconds(-45), "[color=#80ed99]Parser online[/color] and ready for annotated messages."));
        _chatBox.Messages.Add(new("Inspector", DateTime.Now.AddSeconds(-22), "Try the controls above to append [b]warnings[/b], [color=#6ad6ff]status[/color], or refresh the explicit run preview."));
        _chatBox.Messages.Add(new(Environment.UserName, DateTime.Now.AddSeconds(-6), "This feed keeps [u]chat[/u], [u]logs[/u], and runtime annotations intentionally lightweight."));

        annotatedHost.SetContent(_annotatedText);
        logHost.SetContent(_logView);
        chatHost.SetContent(_chatBox);

        Window.GetElementByName<MGButton>("AppendWarningButton").AddCommandHandler((_, __) => AppendWarning());
        Window.GetElementByName<MGButton>("AppendSuccessButton").AddCommandHandler((_, __) => AppendSuccess());
        Window.GetElementByName<MGButton>("RefreshRunsButton").AddCommandHandler((_, __) => RefreshAnnotatedRuns());
        Window.GetElementByName<MGButton>("AppendChatMessageButton").AddCommandHandler((_, __) => AppendChatMessage());
    }

    private IReadOnlyList<MGTextRun> BuildAnnotatedRuns()
    {
        Color accent = _annotationVersion % 2 == 0
            ? new Color(106, 214, 255)
            : new Color(255, 209, 102);

        return new MGTextRun[]
        {
            new MGTextRunText("Annotated runs", new MGTextRunConfig(true, false, 1.0f, accent), null, null),
            new MGTextRunText(" now form a compact programmatic path for runtime tooling text.", new MGTextRunConfig(false), null, null),
            new MGTextRunLineBreak(1),
            new MGTextRunText("Revision ", new MGTextRunConfig(false), null, null),
            new MGTextRunText(_annotationVersion.ToString(), new MGTextRunConfig(true, false, 1.0f, Color.White), null, null),
            new MGTextRunText(": use explicit runs when text is already structured; reserve inline markup for short authored strings.", new MGTextRunConfig(false, false, 1.0f, new Color(215, 220, 228)), null, null)
        };
    }

    private void RefreshAnnotatedRuns()
    {
        _annotationVersion++;
        _annotatedText.SetTextRuns(BuildAnnotatedRuns());
        _logView.AppendEntry($"[color=#6ad6ff]Refresh[/color] Annotated preview rebuilt #{_annotationVersion}.", "TRACE");
    }

    private void AppendWarning()
    {
        _warningCount++;
        _logView.AppendEntry($"[color=#ffd166]Warning {_warningCount}[/color] Scroll state is preserved while appending formatted diagnostics.", "WARN");
    }

    private void AppendSuccess()
    {
        _logView.AppendEntry("[color=#80ed99]Success[/color] Chat, log and annotated text keep reusing the same lightweight text pipeline.", "INFO");
    }

    private void AppendChatMessage()
    {
        _chatMessageCount++;
        _chatBox.SendMessage($"[b]Chat {_chatMessageCount}[/b]: [color=#6ad6ff]inline formatting[/color] is enabled per message body, without introducing a document editor.");
    }
}