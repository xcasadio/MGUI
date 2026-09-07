using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Input;

/// <summary>
/// Regression for the task "Rendre les abonnements dynamic resource deregistrables et lies au cycle de vie"
/// (<c>Docs/Tasks/styling-theme-tasks.md</c>): a dynamic resource reference registered via
/// <see cref="UIResourceReferenceApplicator.Apply"/> on an element inside a closed <see cref="MGWindow"/> must not root that
/// window (and its subtree) from <see cref="MGDesktop.Resources"/>, mirroring the existing guarantee for the theme-change
/// subscription pinned by <see cref="MGUI.Tests.Input.InputLifetimeRegressionTests"/>. This reuses the same GC-harness pattern
/// (headless <see cref="GraphTestRuntime"/>, repeated <see cref="GC.Collect()"/>/<see cref="GC.WaitForPendingFinalizers"/>).
/// </summary>
public class ResourceReferenceLifetimeRegressionTests
{
    [Fact]
    public void ClosedWindow_WithDynamicResourceReference_IsNotRootedByDesktopResources()
    {
        MGDesktop desktop = CreateDesktop();

        WeakReference windowReference = BuildAndCloseWindowWithDynamicReference(desktop);

        desktop.Update();
        desktop.Update();
        CollectGarbage();

        Assert.False(windowReference.IsAlive,
            "Expected the closed window to be collectable: the dynamic resource subscription must not root it via Desktop.Resources.");
    }

    /// <summary>
    /// Companion regression guarding the other half of the contract: a window that is closed but still strongly referenced
    /// (the "closing is not dying" scenario) keeps receiving <see cref="MGResources.OnStaticResourceLookupChanged"/> updates
    /// forwarded from <see cref="MGDesktop.Resources"/> - both while closed and after being re-shown - through garbage collections.
    /// </summary>
    [Fact]
    public void ResourceChange_StillReachesWindow_WhileClosedAndAfterReopen()
    {
        MGDesktop desktop = CreateDesktop();
        desktop.Resources.AddStaticResource("Accent", 1);

        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);
        desktop.Update();

        CountingTarget target = new();
        UIResourceReferenceConfig config = new(nameof(CountingTarget.Value), "Accent", true);
        Assert.True(UIResourceReferenceApplicator.Apply(window, target, config, window.GetResources()));
        Assert.Equal(1, target.Value);

        Assert.True(window.TryCloseWindow());
        Assert.DoesNotContain(window, desktop.Windows);

        CollectGarbage();

        desktop.Resources.SetStaticResource("Accent", 2);
        Assert.Equal(2, target.Value);

        // Re-show the SAME instance (SampleBase.Show pattern) and change the resource again after another GC.
        desktop.Windows.Add(window);
        desktop.Update();
        CollectGarbage();

        desktop.Resources.SetStaticResource("Accent", 3);
        Assert.Equal(3, target.Value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference BuildAndCloseWindowWithDynamicReference(MGDesktop desktop)
    {
        desktop.Resources.AddStaticResource("Accent", 1);

        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);
        desktop.Update();

        CountingTarget target = new();
        UIResourceReferenceConfig config = new(nameof(CountingTarget.Value), "Accent", true);
        Assert.True(UIResourceReferenceApplicator.Apply(window, target, config, window.GetResources()));

        bool closed = window.TryCloseWindow();
        Assert.True(closed, "Precondition failed: the test window could not be closed.");
        Assert.DoesNotContain(window, desktop.Windows);

        return new WeakReference(window);
    }

    private static void CollectGarbage()
    {
        for (int i = 0; i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static MGDesktop CreateDesktop()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        return new MGDesktop(runtime);
    }

    private sealed class CountingTarget
    {
        public int Value { get; set; }
    }
}
