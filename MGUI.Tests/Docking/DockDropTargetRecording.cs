using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Docking;

/// <summary>
/// Records a scripted drag frame by frame. The eight fields of <c>MGDockHost.CurrentDropTarget</c> are copied as text at the frame they are read, never kept as a
/// reference: a target that the host reuses would make a recording of references agree with itself.
/// </summary>
internal sealed class DockDropTargetRecording
{
    private readonly DockDropTargetScene Scene;
    private readonly List<string> Lines = new();

    public DockDropTargetRecording(DockDropTargetScene scene)
    {
        Scene = scene;
    }

    public IReadOnlyList<string> Result => Lines;

    /// <summary>The eight fields of a drop target, as text, or <c>null</c>.</summary>
    public static string Values(DockDropTarget target)
    {
        if (target == null)
        {
            return "null";
        }

        return $"node={DockDropTargetScene.Describe(target.TargetNode)} zone={target.Zone} hit={Rect(target.HitRect)} preview={Rect(target.PreviewRect)} tab={target.TabIndex} hostEdge={target.IsHostEdge} splitterDrop={target.IsSplitterDrop} splitter={DockDropTargetScene.Describe(target.SplitterNode)}";
    }

    private static string Rect(Rectangle r) => $"{r.X},{r.Y},{r.Width},{r.Height}";

    private MGDockHost Host => Scene.Host;

    public void Note(string text) => Lines.Add("## " + text);

    private void RecordFrame(string label) => Lines.Add($"{label}: {Values(Host.CurrentDropTarget)}");

    /// <summary>Moves the mouse and runs one frame.</summary>
    public void Move(string label, Point position)
    {
        Scene.Bench.Mouse = position;
        Scene.Bench.Frame();
        RecordFrame(label);
    }

    /// <summary>Runs one frame with the mouse where it is.</summary>
    public void Hold(string label)
    {
        Scene.Bench.Frame();
        RecordFrame(label);
    }

    /// <summary>Calls <c>GetDropTarget</c> and records what it returns and the property after it.</summary>
    public void Public(string label, Point position)
    {
        DockDropTarget returned = Host.GetDropTarget(position);
        Lines.Add($"{label}: returned {Values(returned)} | current {Values(Host.CurrentDropTarget)}");
    }

    /// <summary>Presses on the tab of A1 and starts the drag, recording each frame.</summary>
    public void PressAndStartTheDrag()
    {
        Note("press and start the drag");
        Scene.Bench.Mouse = Scene.TabA1;
        Scene.Bench.Frames(2);
        Scene.Bench.Left = ButtonState.Pressed;
        Scene.Bench.Frame();
        RecordFrame("pressed");
        Scene.Bench.Mouse = new Point((Scene.TabA1.X + Scene.Bounds(Scene.GroupB).Center.X) / 2, (Scene.TabA1.Y + Scene.Bounds(Scene.GroupB).Center.Y) / 2);
        Scene.Bench.Frame();
        RecordFrame("moved midway");
    }

    /// <summary>Releases the left button (the drop), runs two frames, and records the layout.</summary>
    public void Release(string label)
    {
        Scene.Bench.Left = ButtonState.Released;
        Scene.Bench.Frame();
        RecordFrame(label + " released");
        Scene.Bench.Frame();
        RecordFrame(label + " after the drop");
        Lines.Add($"{label} layout: {Scene.DescribeLayout()}");
    }
}
