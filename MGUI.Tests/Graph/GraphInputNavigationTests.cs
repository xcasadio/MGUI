using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Graph;

public class GraphInputNavigationTests
{
    [Fact]
    public void GraphInput_ZoomAtViewportPointKeepsWorldPointUnderCursor()
    {
        MGGraphView graphView = CreateGraphView();
        graphView.ViewportTransform.Pan = new Vector2(12, -8);
        graphView.ViewportTransform.Zoom = 1.0f;
        Vector2 viewportPoint = new(160, 90);
        Vector2 worldBefore = graphView.ViewportTransform.ViewportToWorld(viewportPoint);

        Assert.True(graphView.ZoomAtViewportPoint(viewportPoint, 120));

        Vector2 worldAfter = graphView.ViewportTransform.ViewportToWorld(viewportPoint);
        Assert.True(Vector2.Distance(worldBefore, worldAfter) < 0.001f);
    }

    [Fact]
    public void GraphInput_PanDoesNotMutateNodeWorldPositions()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel node = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", new Vector2(40, 72));
        Vector2 positionBefore = node.Position;

        Assert.True(graphView.PanViewportBy(new Vector2(24, -12)));

        Assert.Equal(positionBefore, node.Position);
        Assert.Equal(new Vector2(24, -12), graphView.ViewportTransform.Pan);
    }

    [Fact]
    public void GraphInput_AllowFlagsDisablePanAndZoomCommands()
    {
        MGGraphView graphView = CreateGraphView();
        graphView.AllowPan = false;
        graphView.AllowZoom = false;

        Assert.False(graphView.PanViewportBy(new Vector2(10, 10)));
        Assert.False(graphView.ZoomAtViewportPoint(new Vector2(100, 100), 120));
        Assert.Equal(Vector2.Zero, graphView.ViewportTransform.Pan);
        Assert.Equal(1.0f, graphView.ViewportTransform.Zoom);
    }

    [Fact]
    public void GraphInput_FrameAllUsesDocumentNodeBounds()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel first = graphView.Document.AddNode(Guid.NewGuid(), "Value", "First", new Vector2(0, 0));
        GraphNodeModel second = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Second", new Vector2(220, 120));
        first.Size = new Vector2(100, 60);
        second.Size = new Vector2(100, 60);

        Assert.True(graphView.FrameAll(new Rectangle(0, 0, 400, 300), 20));

        Vector2 worldCenter = new(160, 90);
        Vector2 viewportCenter = graphView.ViewportTransform.WorldToViewport(worldCenter);
        Assert.True(Vector2.Distance(new Vector2(200, 150), viewportCenter) < 0.01f);
    }

    [Fact]
    public void GraphInput_FrameSelectionUsesSelectedNodesBeforeFullDocument()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel selected = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Selected", new Vector2(20, 40));
        GraphNodeModel far = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Far", new Vector2(1000, 1000));
        selected.Size = new Vector2(120, 80);
        far.Size = new Vector2(120, 80);
        graphView.SelectedNodeIds.Add(selected.Id);

        Assert.True(graphView.FrameSelection(new Rectangle(0, 0, 500, 300), 30));

        Vector2 selectedCenter = graphView.ViewportTransform.WorldToViewport(new Vector2(80, 80));
        Assert.True(Vector2.Distance(new Vector2(250, 150), selectedCenter) < 0.01f);
    }

    [Fact]
    public void GraphInput_HandleGraphShortcutInvokesFrameCommands()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel node = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", new Vector2(200, 200));
        node.Size = new Vector2(100, 80);
        graphView.ViewportTransform.Pan = new Vector2(25, 35);
        graphView.ViewportTransform.Zoom = 2.0f;

        Assert.True(graphView.HandleGraphShortcut(Keys.Home));
        Assert.Equal(1.0f, graphView.ViewportTransform.Zoom);
        Assert.True(graphView.HandleGraphShortcut(Keys.A));
        Assert.False(graphView.HandleGraphShortcut(Keys.B));
    }

    [Fact]
    public void GraphInput_NodeReleasedOutsideDoesNotBlockSiblingButtonClicks()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGCanvas root = new(window);
        MGButton button = new(window)
        {
            PreferredWidth = 120,
            PreferredHeight = 30,
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 304,
        };

        window.SetContent(root);
        desktop.Windows.Add(window);
        using (root.AllowChangingContentTemporarily())
        {
            root.TryAddChild(button);
            root.TryAddChild(graphView);
        }

        MGCanvas.SetLeft(button, 8);
        MGCanvas.SetTop(button, 8);
        MGCanvas.SetLeft(graphView, 0);
        MGCanvas.SetTop(graphView, 48);

        graphView.Document.AddNode(Guid.NewGuid(), "Value", "Node", new Vector2(40, 40));
        graphView.SynchronizeDocument();

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        int clickCount = 0;
        button.MouseHandler.LMBReleasedInside += (_, _) => clickCount++;

        Point buttonCenter = button.ActualLayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 32, buttonCenter);
        AdvanceFrame(runtime, desktop, 48, buttonCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, buttonCenter);

        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void GraphInput_ClickingEdgeSelectsItAndDeleteRemovesIt()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", new Vector2(40, 60)).Id;
        Guid targetNodeId = document.AddNode(Guid.NewGuid(), "Value", "Target", new Vector2(260, 120)).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;
        Guid targetPortId = document.AddPort(targetNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float).Id;
        GraphEdgeModel edge = document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Assert.True(graphView.TryGetPortControl(sourcePortId, out MGGraphPort sourcePort));
        Assert.True(graphView.TryGetPortControl(targetPortId, out MGGraphPort targetPort));
        List<Vector2> edgePoints = new();
        GraphBezierGeometry.BuildDefaultEdge(sourcePort.GetLayoutAnchor(), targetPort.GetLayoutAnchor(), edgePoints, GraphBezierGeometry.DefaultSegmentCount);
        Point clickPoint = edgePoints[edgePoints.Count / 2].ToPoint();

        AdvanceFrame(runtime, desktop, 32, clickPoint);
        AdvanceFrame(runtime, desktop, 48, clickPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, clickPoint);

        Assert.Contains(edge.Id, graphView.SelectedEdgeIds);
        Assert.True(graphView.HandleGraphShortcut(Keys.Delete, controlDown: false));
        Assert.Empty(document.Edges);
    }

    [Fact]
    public void GraphInput_HandleGraphShortcutCopyAndPasteDuplicatesSelectedSubgraphAtPointer()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };
        string clipboardText = string.Empty;
        graphView.ClipboardTextReader = () => clipboardText;
        graphView.ClipboardTextWriter = value => clipboardText = value;

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", new Vector2(40, 60)).Id;
        Guid targetNodeId = document.AddNode(Guid.NewGuid(), "Value", "Target", new Vector2(260, 120)).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;
        Guid targetPortId = document.AddPort(targetNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float).Id;
        GraphEdgeModel originalEdge = document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        graphView.SelectedNodeIds.Add(sourceNodeId);
        graphView.SelectedNodeIds.Add(targetNodeId);
        graphView.UpdateSelectionVisuals();

        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Point pasteScreenPoint = new(graphView.NodesCanvas.AlignedContentBounds.Left + 320, graphView.NodesCanvas.AlignedContentBounds.Top + 210);
        AdvanceFrame(runtime, desktop, 32, pasteScreenPoint);
        Vector2 expectedAnchor = graphView.GetWorldPointFromScreenPosition(pasteScreenPoint);

        Assert.True(graphView.HandleGraphShortcut(Keys.C, controlDown: true));
        Assert.StartsWith("MGUI.GraphClipboard.v1", clipboardText);
        Assert.True(graphView.HandleGraphShortcut(Keys.V, controlDown: true));

        GraphNodeModel pastedSource = Assert.Single(document.Nodes, node => node.Id != sourceNodeId && node.Id != targetNodeId && node.Title == "Source");
        GraphNodeModel pastedTarget = Assert.Single(document.Nodes, node => node.Id != sourceNodeId && node.Id != targetNodeId && node.Title == "Target");
        GraphEdgeModel pastedEdge = Assert.Single(document.Edges, edge => edge.Id != originalEdge.Id);

        Assert.Equal(expectedAnchor, pastedSource.Position);
        Assert.Equal(new Vector2(expectedAnchor.X + 220, expectedAnchor.Y + 60), pastedTarget.Position);
        Assert.Equal(pastedSource.Id, pastedEdge.SourceNodeId);
        Assert.Equal(pastedTarget.Id, pastedEdge.TargetNodeId);
        Assert.Contains(pastedSource.Id, graphView.SelectedNodeIds);
        Assert.Contains(pastedTarget.Id, graphView.SelectedNodeIds);
    }

    [Fact]
    public void GraphInput_SelectedOverlappingNodeStaysTopmostForSubsequentClicks()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        GraphDocument document = graphView.Document;
        Guid firstNodeId = document.AddNode(Guid.NewGuid(), "Value", "First", new Vector2(40, 60)).Id;
        Guid secondNodeId = document.AddNode(Guid.NewGuid(), "Value", "Second", new Vector2(140, 110)).Id;
        document.TryGetNode(firstNodeId)!.Size = new Vector2(220, 160);
        document.TryGetNode(secondNodeId)!.Size = new Vector2(220, 160);

        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Assert.True(graphView.TryGetNodeControl(firstNodeId, out MGGraphNode firstNode));
        Assert.True(graphView.TryGetNodeControl(secondNodeId, out MGGraphNode secondNode));

        Point firstOnlyPoint = new(firstNode.ActualLayoutBounds.Left + 20, firstNode.ActualLayoutBounds.Top + 20);
        Rectangle overlap = Rectangle.Intersect(firstNode.ActualLayoutBounds, secondNode.ActualLayoutBounds);
        Assert.True(overlap.Width > 0 && overlap.Height > 0);
        Point overlapPoint = overlap.Center;

        AdvanceFrame(runtime, desktop, 32, firstOnlyPoint);
        AdvanceFrame(runtime, desktop, 48, firstOnlyPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, firstOnlyPoint);
        Assert.Contains(firstNodeId, graphView.SelectedNodeIds);

        AdvanceFrame(runtime, desktop, 80, overlapPoint);
        AdvanceFrame(runtime, desktop, 96, overlapPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 112, overlapPoint);

        Assert.Contains(firstNodeId, graphView.SelectedNodeIds);
        Assert.DoesNotContain(secondNodeId, graphView.SelectedNodeIds);
    }

    [Fact]
    public void GraphInput_OccludedLowerPortDoesNotCaptureTopNodePress()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        GraphDocument document = graphView.Document;
        Guid lowerNodeId = document.AddNode(Guid.NewGuid(), "Value", "Lower", new Vector2(150, 110)).Id;
        Guid upperNodeId = document.AddNode(Guid.NewGuid(), "Value", "Upper", new Vector2(95, 70)).Id;
        Guid lowerPortId = document.AddPort(lowerNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float).Id;
        document.TryGetNode(lowerNodeId)!.Size = new Vector2(220, 160);
        document.TryGetNode(upperNodeId)!.Size = new Vector2(260, 190);

        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Assert.True(graphView.TryGetPortControl(lowerPortId, out MGGraphPort lowerPort));
        Assert.True(graphView.TryGetNodeControl(upperNodeId, out MGGraphNode upperNode));

        Point coveredPortPoint = lowerPort.GetLayoutAnchor().ToPoint();
        Assert.True(upperNode.ActualLayoutBounds.Contains(coveredPortPoint));

        AdvanceFrame(runtime, desktop, 32, coveredPortPoint);
        AdvanceFrame(runtime, desktop, 48, coveredPortPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, coveredPortPoint);

        Assert.Contains(upperNodeId, graphView.SelectedNodeIds);
        Assert.DoesNotContain(lowerNodeId, graphView.SelectedNodeIds);
        Assert.False(graphView.ConnectionController.IsDragging);
    }

    [Fact]
    public void GraphInput_DoubleClickingCommentOpensEditorAndCommitUpdatesDocument()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        Guid commentId = Guid.NewGuid();
        graphView.Document.AddComment(commentId, new Rectangle(80, 70, 240, 96), "Comment", "Line 1");
        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Assert.True(graphView.TryGetCommentControl(commentId, out MGGraphCommentBox commentBox));
    Point clickPoint = GetCommentBodyPoint(commentBox);

        AdvanceFrame(runtime, desktop, 32, clickPoint);
        AdvanceFrame(runtime, desktop, 48, clickPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, clickPoint);
        AdvanceFrame(runtime, desktop, 80, clickPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 96, clickPoint);
        AdvanceFrame(runtime, desktop, 112, clickPoint);

        Assert.True(graphView.IsCommentEditorOpen);
        Assert.Equal(commentId, graphView.EditingCommentId);
        Assert.Same(commentBox.TitleTextBox, graphView.CommentEditorTitleTextBox);
        Assert.Same(commentBox.BodyTextBox, graphView.CommentEditorBodyTextBox);
        Assert.False(commentBox.TitleTextBox.IsReadonly);
        Assert.False(commentBox.BodyTextBox.IsReadonly);
        Assert.True(commentBox.TitleTextBox.IsHitTestVisible);
        Assert.True(commentBox.BodyTextBox.IsHitTestVisible);

        graphView.CommentEditorTitleTextBox.SetText("Edited Comment");
        graphView.CommentEditorBodyTextBox.SetText("Line 1\nLine 2\nLine 3");

        Assert.True(graphView.CommitActiveCommentEditor());
        Assert.False(graphView.IsCommentEditorOpen);
        Assert.True(commentBox.TitleTextBox.IsReadonly);
        Assert.True(commentBox.BodyTextBox.IsReadonly);
        Assert.False(commentBox.TitleTextBox.IsHitTestVisible);
        Assert.False(commentBox.BodyTextBox.IsHitTestVisible);

        GraphCommentModel updated = graphView.Document.TryGetComment(commentId)!;
        Assert.Equal("Edited Comment", updated.Title);
        Assert.Equal("Line 1\nLine 2\nLine 3", updated.Text);
        Assert.True(updated.Bounds.Height >= 96);

        Assert.True(graphView.Commands.Undo(graphView.Document));
        GraphCommentModel reverted = graphView.Document.TryGetComment(commentId)!;
        Assert.Equal("Comment", reverted.Title);
        Assert.Equal("Line 1", reverted.Text);
    }

    [Fact]
    public void GraphInput_DraggingCommentBodyMovesCommentAndCanUndo()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 360,
            ShowGrid = false,
        };

        window.SetContent(graphView);
        desktop.Windows.Add(window);

        Guid commentId = Guid.NewGuid();
        Rectangle initialBounds = new(80, 70, 240, 120);
        graphView.Document.AddComment(commentId, initialBounds, "Comment", "Line 1\nLine 2\nLine 3");
        graphView.SynchronizeDocument();
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Assert.True(graphView.TryGetCommentControl(commentId, out MGGraphCommentBox commentBox));
        Assert.True(commentBox.TitleTextBox.IsReadonly);
        Assert.True(commentBox.BodyTextBox.IsReadonly);
        Point startPoint = GetCommentBodyPoint(commentBox);
        Point endPoint = startPoint + new Point(80, 44);

        AdvanceFrame(runtime, desktop, 32, startPoint);
        AdvanceFrame(runtime, desktop, 48, startPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, startPoint + new Point(8, 6), MouseButton.Left);
        AdvanceFrame(runtime, desktop, 80, endPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 96, endPoint);
        AdvanceFrame(runtime, desktop, 112, endPoint);

        GraphCommentModel moved = graphView.Document.TryGetComment(commentId)!;
        Assert.Equal(new Rectangle(initialBounds.X + 80, initialBounds.Y + 44, initialBounds.Width, initialBounds.Height), moved.Bounds);
        Assert.Contains(commentId, graphView.SelectedCommentIds);

        Assert.True(graphView.Commands.Undo(graphView.Document));
        GraphCommentModel restored = graphView.Document.TryGetComment(commentId)!;
        Assert.Equal(initialBounds, restored.Bounds);
    }

    private static MGGraphView CreateGraphView()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
        };
        return new MGGraphView(window);
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null, int scrollWheel = 0)
        => new(
            position.X,
            position.Y,
            scrollWheel,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private static Point GetCommentBodyPoint(MGGraphCommentBox commentBox)
    {
        Rectangle bodyBounds = commentBox.BodyTextBox?.ActualLayoutBounds ?? Rectangle.Empty;
        if (bodyBounds.Width > 0 && bodyBounds.Height > 0)
        {
            return bodyBounds.Center;
        }

        return new Point(commentBox.ActualLayoutBounds.Left + 24, commentBox.ActualLayoutBounds.Center.Y);
    }
}