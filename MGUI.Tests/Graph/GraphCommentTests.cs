using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphCommentTests
{
    [Fact]
    public void GraphComment_CommandsCreateMoveResizeDeleteAndUndo()
    {
        GraphDocument document = new();
        GraphCommandStack commands = new();
        Guid commentId = Guid.NewGuid();
        GraphCommentModel comment = new(commentId, new Rectangle(10, 20, 220, 90), "Notes", "Start here");

        Assert.True(commands.Execute(document, new CreateCommentCommand(comment)));
        Assert.Same(comment, document.TryGetComment(commentId));

        Assert.True(commands.Execute(document, new MoveCommentCommand(commentId, comment.Bounds, new Rectangle(40, 50, 220, 90))));
        Assert.Equal(new Rectangle(40, 50, 220, 90), document.TryGetComment(commentId)!.Bounds);

        Assert.True(commands.Execute(document, new ResizeCommentCommand(commentId, document.TryGetComment(commentId)!.Bounds, new Rectangle(40, 50, 300, 160))));
        Assert.Equal(new Rectangle(40, 50, 300, 160), document.TryGetComment(commentId)!.Bounds);

        Assert.True(commands.Execute(document, new DeleteCommentCommand(commentId)));
        Assert.Null(document.TryGetComment(commentId));

        Assert.True(commands.Undo(document));
        Assert.Equal(new Rectangle(40, 50, 300, 160), document.TryGetComment(commentId)!.Bounds);
        Assert.True(commands.Undo(document));
        Assert.Equal(new Rectangle(40, 50, 220, 90), document.TryGetComment(commentId)!.Bounds);
        Assert.True(commands.Undo(document));
        Assert.Equal(new Rectangle(10, 20, 220, 90), document.TryGetComment(commentId)!.Bounds);
        Assert.True(commands.Undo(document));
        Assert.Null(document.TryGetComment(commentId));
    }

    [Fact]
    public void GraphComment_GraphViewSynchronizesVisibleCommentBox()
    {
        MGGraphView graphView = CreateGraphView();
        Guid commentId = Guid.NewGuid();
        graphView.Document.AddComment(commentId, new Rectangle(12, 24, 260, 120), "Group", "Important branch");
        graphView.SelectedCommentIds.Add(commentId);

        graphView.ViewportTransform.Zoom = 2.0f;
        graphView.ViewportTransform.Pan = new Vector2(5, 7);
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetCommentControl(commentId, out MGGraphCommentBox commentBox));
        Assert.Contains(commentBox, graphView.NodesCanvas.Children);
        Assert.Equal("Group", commentBox.Title);
        Assert.Equal("Important branch", commentBox.Text);
        Assert.True(commentBox.IsSelected);
        Assert.Equal(29, MGCanvas.GetLeft(commentBox));
        Assert.Equal(55, MGCanvas.GetTop(commentBox));
        Assert.Equal(520, commentBox.PreferredWidth);
        Assert.Equal(240, commentBox.PreferredHeight);
    }

    [Fact]
    public void GraphComment_GraphViewCreatesMovesResizesDeletesAndRestoresComment()
    {
        MGGraphView graphView = CreateGraphView();

        GraphCommentModel comment = graphView.CreateCommentAt(new Vector2(30, 40), "Todo", "Wire later");

        Assert.NotNull(comment);
        Assert.Equal(new Rectangle(30, 40, 260, 120), comment.Bounds);
        Assert.Contains(comment.Id, graphView.SelectedCommentIds);

        Assert.True(graphView.MoveSelectedCommentsBy(new Vector2(10, 15)));
        Assert.Equal(new Rectangle(40, 55, 260, 120), graphView.Document.TryGetComment(comment.Id)!.Bounds);
        Assert.True(graphView.ResizeComment(comment.Id, new Rectangle(40, 55, 320, 180)));
        Assert.Equal(new Rectangle(40, 55, 320, 180), graphView.Document.TryGetComment(comment.Id)!.Bounds);

        Assert.True(graphView.DeleteSelection());
        Assert.Null(graphView.Document.TryGetComment(comment.Id));
        Assert.Empty(graphView.SelectedCommentIds);

        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Equal(new Rectangle(40, 55, 320, 180), graphView.Document.TryGetComment(comment.Id)!.Bounds);
    }

    [Fact]
    public void GraphComment_SerializationPreservesBoundsTitleTextAndColor()
    {
        GraphDocument document = new();
        GraphCommentModel comment = document.AddComment(Guid.NewGuid(), new Rectangle(5, 6, 300, 140), "Comment Title", "Comment body");
        comment.Color = new Color(12, 34, 56, 200);

        GraphSerializer serializer = new();
        GraphDocument roundTrip = serializer.Deserialize(serializer.Serialize(document).Json).Document;

        GraphCommentModel restored = Assert.Single(roundTrip.Comments);
        Assert.Equal(comment.Id, restored.Id);
        Assert.Equal(new Rectangle(5, 6, 300, 140), restored.Bounds);
        Assert.Equal("Comment Title", restored.Title);
        Assert.Equal("Comment body", restored.Text);
        Assert.Equal(new Color(12, 34, 56, 200), restored.Color);
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
}