namespace MGUI.Samples.Features
{
    /// <summary>V3 sample assets (SCN-ANIM-003, Docs/Tasks/animation-v3-tasks.md U11) kept out of <see cref="AnimationDemoSample"/> so the embedded
    /// JSON clip literal is easy to spot and to reuse from a test. Declared <see langword="public"/>, not <see langword="internal"/>, because
    /// MGUI.Samples does not grant MGUI.Tests an <c>InternalsVisibleTo</c> (checked: absent from MGUI.Core/Properties/AssemblyInfo.cs and there is
    /// no equivalent file in this project) -- an internal member would simply be invisible to the test that deserialises it.</summary>
    public static class AnimationDemoV3Assets
    {
        /// <summary>A three-track clip (U6, ADR-0008 decision 6): <c>Opacity</c> 0 to 1, <c>RenderTransform.Scale</c> 0.8 to 1, <c>Background</c>
        /// transparent black to opaque blue, over one second -- the exact example of Docs/animation-architecture.md's "Clip multi-pistes" section.
        /// Deserialised by <see cref="MGUI.Core.UI.Animation.KeyFrames.UIKeyFrameClipSerializer.Deserialize"/> both for the live "Play clip" button
        /// (<see cref="AnimationDemoSample"/>'s <c>V3ClipTarget</c>) and, as a fresh instance, for the "Preview and Seek" section's dedicated,
        /// never-live <c>V3PreviewTarget</c>.</summary>
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
    }
}
