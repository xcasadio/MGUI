using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;
using System.Globalization;
using System.Text;

namespace MGUI.Tests.Animation;

/// <summary>
/// A scripted scenario around the reveal that <see cref="MGTextBlock.TextCharactersPerSecond"/> drives, run frame by frame on a headless desktop, that writes one journal: after each
/// frame and each action, the progress, the revealed character count, whether the reveal runs, the active animations of the desktop and the text drawn; between them the
/// <see cref="INotifyPropertyChanged"/> notifications of the text block (in order, with the value of the progress) and the events of the reveal run raised during a frame.
/// Its journal is frozen in <see cref="TextRevealGolden"/> (taken from the code before the reveal run was reused from one restart to the next).
/// </summary>
internal static class TextRevealScenario
{
    private const string FirstText = "The quick brown fox jumps over";
    private const string SecondText = "A much longer second text that wraps over";
    private const string ThirdText = "Short";

    public static List<string> Run(bool allowsInlineFormatting)
    {
        List<string> journal = new();
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextBlock textBlock = new(scene.Window, FirstText, null, null, allowsInlineFormatting) { PreferredWidth = 300 };
        scene.Panel.TryAddChild(textBlock);
        scene.Frames(2);

        bool ticking = false;
        HashSet<UIAnimation> hooked = new(ReferenceEqualityComparer.Instance);
        textBlock.PropertyChanged += (sender, e) =>
        {
            string value = e.PropertyName switch
            {
                nameof(MGTextBlock.TextProgress) => textBlock.TextProgress?.ToString("R", CultureInfo.InvariantCulture) ?? "null",
                nameof(MGTextBlock.TextCharactersPerSecond) => textBlock.TextCharactersPerSecond?.ToString("R", CultureInfo.InvariantCulture) ?? "null",
                _ => ""
            };
            journal.Add($"  pc:{e.PropertyName}={value}");
        };

        void Hook()
        {
            foreach (UIAnimation animation in textBlock.Animations.Active.Where(x => x.Name == MGTextBlock.TextRevealAnimationName))
            {
                if (hooked.Add(animation))
                {
                    animation.Started += (s, e) => { if (ticking) { journal.Add("  ev:Started"); } };
                    animation.Updated += (s, e) => { if (ticking) { journal.Add("  ev:Updated"); } };
                    animation.Completed += (s, e) => { if (ticking) { journal.Add("  ev:Completed"); } };
                    animation.Cancelled += (s, e) => { if (ticking) { journal.Add("  ev:Cancelled"); } };
                }
            }
        }

        void Snap(string label)
        {
            StringBuilder drawn = new();
            int count = 0;
            foreach (var call in scene.Draw().DrawTextCalls)
            {
                drawn.Append(call.Text).Append('|'); count += call.Text.Length;
            }

            journal.Add($"{label}: progress={textBlock.TextProgress?.ToString("R", CultureInfo.InvariantCulture) ?? "null"} revealed={count} " +
                        $"run={textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress)} active={scene.Desktop.Animations.ActiveCount} drawn={drawn}");
        }

        void Frames(string label, int count, int milliseconds = AnimationTestScene.FrameMilliseconds)
        {
            for (int i = 0; i < count; i++)
            {
                Hook();
                ticking = true;
                scene.Frames(1, milliseconds);
                ticking = false;
                Hook();
                Snap($"{label}[{i}]");
            }
        }

        void Act(string label, Action action)
        {
            journal.Add($"> {label}");
            action();
            Hook();
            Snap(label);
        }

        //  1. enabling a speed, a reveal to the end, and the text held
        Act("speed 20", () => textBlock.TextCharactersPerSecond = 20);
        Frames("reveal", 30);
        //  2. a text change mid-reveal, a second one right after it in the same frame, the identical text
        Act("text second", () => textBlock.Text = SecondText);
        Frames("second", 12);
        Act("text third then first", () => { textBlock.Text = ThirdText; textBlock.Text = FirstText; });
        Frames("first", 5);
        Act("identical text", () => textBlock.Text = FirstText);
        Frames("identical", 3);
        //  3. a speed change mid-reveal
        Act("speed 60", () => textBlock.TextCharactersPerSecond = 60);
        Frames("faster", 6);
        Act("speed 5", () => textBlock.TextCharactersPerSecond = 5);
        Frames("slower", 6);
        //  4. TextProgress by hand: a fraction, zero (replay), one, null, a fraction again
        Act("progress 0.5", () => textBlock.TextProgress = 0.5);
        Frames("half", 4);
        Act("progress 0", () => textBlock.TextProgress = 0.0);
        Frames("replay", 4);
        Act("progress 1", () => textBlock.TextProgress = 1.0);
        Frames("one", 3);
        Act("progress null", () => textBlock.TextProgress = null);
        Frames("null", 3);
        Act("progress 0.2", () => textBlock.TextProgress = 0.2);
        Frames("fifth", 4);
        //  5. a reveal to the end (a speed that completes it), then a text change, a speed change and a replay after the completion
        Act("speed 200", () => textBlock.TextCharactersPerSecond = 200);
        Frames("finish", 20);
        Act("speed 100 after completion", () => textBlock.TextCharactersPerSecond = 100);
        Frames("after completion", 3);
        Act("text second after completion", () => textBlock.Text = SecondText);
        Frames("second again", 12);
        Act("progress 0 after text", () => textBlock.TextProgress = 0.0);
        Frames("replayed", 3);
        //  6. a null speed, a text change without speed, a speed again; zero and negative speeds
        Act("speed null", () => textBlock.TextCharactersPerSecond = null);
        Frames("no speed", 3);
        Act("text first without speed", () => textBlock.Text = FirstText);
        Frames("text without speed", 2);
        Act("speed 30 from null", () => textBlock.TextCharactersPerSecond = 30);
        Frames("from null", 8);
        Act("speed 0", () => textBlock.TextCharactersPerSecond = 0);
        Frames("zero", 3);
        Act("speed 30 from zero", () => textBlock.TextCharactersPerSecond = 30);
        Frames("from zero", 5);
        Act("speed -4", () => textBlock.TextCharactersPerSecond = -4);
        Frames("negative", 3);
        Act("speed 30 from negative", () => textBlock.TextCharactersPerSecond = 30);
        Frames("from negative", 5);
        //  7. an empty text, then a text again
        Act("text empty", () => textBlock.Text = "");
        Frames("empty", 3);
        Act("text first from empty", () => textBlock.Text = FirstText);
        Frames("from empty", 6);
        //  8. detached mid-reveal, a text change and a speed change while detached, reattached
        Act("detach", () => scene.Panel.TryRemoveChild(textBlock));
        Frames("detached", 4);
        Act("text second detached", () => textBlock.Text = SecondText);
        Frames("detached second", 3);
        Act("attach", () => scene.Panel.TryAddChild(textBlock));
        Frames("attached", 8);
        Act("detach again", () => scene.Panel.TryRemoveChild(textBlock));
        Act("speed 40 detached", () => textBlock.TextCharactersPerSecond = 40);
        Act("attach again", () => scene.Panel.TryAddChild(textBlock));
        Frames("attached again", 8);
        //  9. the clock of the desktop: a pause with a text change during it, a time scale
        Act("pause clock", () => scene.Desktop.Animations.Clock.IsPaused = true);
        Frames("paused", 3);
        Act("text third paused", () => textBlock.Text = ThirdText);
        Frames("paused third", 3);
        Act("resume clock", () => scene.Desktop.Animations.Clock.IsPaused = false);
        Frames("resumed", 6);
        Act("time scale 0.5", () => scene.Desktop.Animations.Clock.TimeScale = 0.5f);
        Act("text second at half speed", () => textBlock.Text = SecondText);
        Frames("half time", 8);
        Act("time scale 1", () => scene.Desktop.Animations.Clock.TimeScale = 1f);
        Frames("full time", 4);
        //  10. frames of other lengths
        Act("text first, long frames", () => textBlock.Text = FirstText);
        Frames("long frame", 4, 100);
        Frames("short frame", 4, 5);
        //  11. a text change made from the callback of another animation, during the tick of the engine: from an animation started
        //  before the reveal (it is ticked first), and from one started after it
        Act("speed null", () => textBlock.TextCharactersPerSecond = null);
        int beforeUpdates = 0;
        UIPropertyAnimation<float> before = AnimationTestScene.OpacityAnimation(0f, 1f, 2000);
        before.Updated += (s, e) =>
        {
            if (++beforeUpdates == 5)
            {
                textBlock.Text = SecondText;
            }
        };
        Act("start the animation before the reveal", () => scene.Top.Animations.Start(before));
        Act("speed 20 after it", () => textBlock.TextCharactersPerSecond = 20);
        Frames("callback before", 10);
        int afterUpdates = 0;
        UIPropertyAnimation<float> after = AnimationTestScene.OpacityAnimation(0f, 1f, 2000);
        after.Updated += (s, e) =>
        {
            if (++afterUpdates == 3)
            {
                textBlock.Text = ThirdText;
            }
        };
        Act("start the animation after the reveal", () => scene.Bottom.Animations.Start(after));
        Frames("callback after", 8);
        Act("cancel the helpers", () => { before.Cancel(); after.Cancel(); });
        //  12. the whole text revealed again to the end
        Act("text second", () => textBlock.Text = SecondText);
        Frames("last", 40);

        return journal;
    }
}
