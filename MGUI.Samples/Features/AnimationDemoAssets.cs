using System;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using Microsoft.Xna.Framework;

namespace MGUI.Samples.Features
{
    /// <summary>Assets shared by <see cref="AnimationDemoSample"/>'s capability sections: the embedded JSON clip and the small demonstration
    /// storyboard used by the composition and serialisation sections. Kept out of the sample class so the embedded JSON literal is easy to
    /// spot and to reuse from a test. Declared <see langword="public"/>, not <see langword="internal"/>, because MGUI.Samples does not grant
    /// MGUI.Tests an <c>InternalsVisibleTo</c> (checked: absent from MGUI.Core/Properties/AssemblyInfo.cs and there is no equivalent file in
    /// this project) -- an internal member would simply be invisible to the test that deserialises it.</summary>
    public static class AnimationDemoAssets
    {
        /// <summary>A three-track clip: <c>Opacity</c> 0 to 1, <c>RenderTransform.Scale</c> 0.8 to 1, <c>Background</c> transparent black to
        /// opaque blue, over one second -- the exact example of Docs/animation-architecture.md's "Clip multi-pistes" section. Deserialised by
        /// <see cref="UIKeyFrameClipSerializer.Deserialize"/> both for the live "Load clip" button (<see cref="AnimationDemoSample"/>'s
        /// <c>ClipTarget</c>) and, as a fresh instance, for the "Preview and seek" section's dedicated, never-live <c>PreviewTarget</c>.</summary>
        public const string ClipJson =
            "{" +
            "\"version\":1," +
            "\"duration\":\"00:00:01\"," +
            "\"tracks\":[" +
            "{\"property\":\"Opacity\",\"valueType\":\"Single\",\"frames\":[{\"offset\":0,\"value\":\"0\"},{\"offset\":1,\"value\":\"1\"}]}," +
            "{\"property\":\"RenderTransform.Scale\",\"valueType\":\"Vector2\",\"frames\":[{\"offset\":0,\"value\":\"0.8,0.8\"},{\"offset\":1,\"value\":\"1,1\"}]}," +
            "{\"property\":\"Background\",\"valueType\":\"Color\",\"frames\":[{\"offset\":0,\"value\":\"#00000000\"},{\"offset\":1,\"value\":\"#FF0000FF\"}]}" +
            "]" +
            "}";

        /// <summary>A keyframe pop of the scale: 0.8 -&gt; 1.1 (BackOut) -&gt; 1.0 (QuadOut) over 400 ms. Used by the "Keyframes and clip"
        /// section's "Play keyframes" button.</summary>
        public static UIKeyFrameAnimation<Vector2> CreatePop() => new(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Track = { { 0f, new Vector2(0.8f) }, { 0.6f, new Vector2(1.1f), "BackOut" }, { 1f, Vector2.One, "QuadOut" } },
            Name = "pop",
        };

        /// <summary>A small two-property storyboard (fade in, scale in from 0.85) with no owner set: used by the "Serialisation"
        /// section's "Save" button (serialised to JSON, then the
        /// "Load + play" button rebuilds and starts an independent copy from that JSON).</summary>
        public static UIStoryboard CreateDemoStoryboard()
        {
            UIStoryboard storyboard = new()
            {
                new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "demo-fade" },
                new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.85f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.BackOut, Name = "demo-scale" },
            };
            storyboard.Name = "demo";
            return storyboard;
        }
    }
}
