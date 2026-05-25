using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    public sealed class CreateNodeCommand : IGraphCommand
    {
        private readonly GraphNodeModel Node;
        public string Name => "Create Node";

        public CreateNodeCommand(GraphNodeModel node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public bool Execute(GraphDocument document)
        {
            if (document?.TryGetNode(Node.Id) != null)
            {
                return false;
            }

            document.AddNode(Node);
            return true;
        }

        public bool Undo(GraphDocument document) => document != null && document.RemoveNode(Node.Id);
    }

    public sealed class DeleteNodeCommand : IGraphCommand
    {
        private readonly Guid NodeId;
        private GraphNodeModel DeletedNode;
        private readonly List<GraphEdgeModel> DeletedEdges = new();
        public string Name => "Delete Node";

        public DeleteNodeCommand(Guid nodeId)
        {
            NodeId = nodeId;
        }

        public bool Execute(GraphDocument document)
        {
            DeletedNode = document?.TryGetNode(NodeId);
            if (document == null || DeletedNode == null)
            {
                return false;
            }

            DeletedEdges.Clear();
            List<GraphEdgeModel> edges = document.GetEdgesForNode(NodeId);
            for (int i = 0; i < edges.Count; i++)
            {
                DeletedEdges.Add(edges[i]);
            }

            return document.RemoveNode(NodeId);
        }

        public bool Undo(GraphDocument document)
        {
            if (document == null || DeletedNode == null || document.TryGetNode(NodeId) != null)
            {
                return false;
            }

            document.AddNode(DeletedNode);
            for (int i = 0; i < DeletedEdges.Count; i++)
            {
                document.AddEdge(DeletedEdges[i], validate: false);
            }

            return true;
        }
    }

    public sealed class MoveNodeCommand : IGraphCommand
    {
        private readonly Guid NodeId;
        private readonly Vector2 OldPosition;
        private readonly Vector2 NewPosition;
        public string Name => "Move Node";

        public MoveNodeCommand(Guid nodeId, Vector2 oldPosition, Vector2 newPosition)
        {
            NodeId = nodeId;
            OldPosition = oldPosition;
            NewPosition = newPosition;
        }

        public bool Execute(GraphDocument document) => SetPosition(document, NewPosition);

        public bool Undo(GraphDocument document) => SetPosition(document, OldPosition);

        private bool SetPosition(GraphDocument document, Vector2 position)
        {
            GraphNodeModel node = document?.TryGetNode(NodeId);
            if (node == null)
            {
                return false;
            }

            node.Position = position;
            document.NotifyGraphChanged();
            return true;
        }
    }

    public readonly record struct GraphNodeMove(Guid NodeId, Vector2 OldPosition, Vector2 NewPosition);

    public sealed class MoveNodesCommand : IGraphCommand
    {
        private readonly List<GraphNodeMove> Moves;
        public string Name => "Move Nodes";

        public MoveNodesCommand(IEnumerable<GraphNodeMove> moves)
        {
            Moves = moves == null ? new List<GraphNodeMove>() : new List<GraphNodeMove>(moves);
        }

        public bool Execute(GraphDocument document) => SetPositions(document, useNewPositions: true);

        public bool Undo(GraphDocument document) => SetPositions(document, useNewPositions: false);

        private bool SetPositions(GraphDocument document, bool useNewPositions)
        {
            if (document == null || Moves.Count == 0)
            {
                return false;
            }

            bool appliedAny = false;
            bool changed = false;
            for (int moveIndex = 0; moveIndex < Moves.Count; moveIndex++)
            {
                GraphNodeMove move = Moves[moveIndex];
                GraphNodeModel node = document.TryGetNode(move.NodeId);
                if (node == null)
                {
                    continue;
                }

                appliedAny = true;
                Vector2 next = useNewPositions ? move.NewPosition : move.OldPosition;
                if (node.Position != next)
                {
                    node.Position = next;
                    changed = true;
                }
            }

            if (changed)
            {
                document.NotifyGraphChanged();
            }

            return appliedAny;
        }
    }

    public sealed class GraphBatchCommand : IGraphCommand
    {
        private readonly List<IGraphCommand> Commands;
        public string Name { get; }

        public GraphBatchCommand(string name, IEnumerable<IGraphCommand> commands)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Graph Batch" : name;
            Commands = commands == null ? new List<IGraphCommand>() : new List<IGraphCommand>(commands);
        }

        public bool Execute(GraphDocument document)
        {
            if (document == null || Commands.Count == 0)
            {
                return false;
            }

            bool executedAny = false;
            for (int commandIndex = 0; commandIndex < Commands.Count; commandIndex++)
            {
                executedAny |= Commands[commandIndex].Execute(document);
            }

            return executedAny;
        }

        public bool Undo(GraphDocument document)
        {
            if (document == null || Commands.Count == 0)
            {
                return false;
            }

            bool undoneAny = false;
            for (int commandIndex = Commands.Count - 1; commandIndex >= 0; commandIndex--)
            {
                undoneAny |= Commands[commandIndex].Undo(document);
            }

            return undoneAny;
        }
    }

    public sealed class ResizeNodeCommand : IGraphCommand
    {
        private readonly Guid NodeId;
        private readonly Vector2? OldSize;
        private readonly Vector2? NewSize;
        public string Name => "Resize Node";

        public ResizeNodeCommand(Guid nodeId, Vector2? oldSize, Vector2? newSize)
        {
            NodeId = nodeId;
            OldSize = oldSize;
            NewSize = newSize;
        }

        public bool Execute(GraphDocument document) => SetSize(document, NewSize);

        public bool Undo(GraphDocument document) => SetSize(document, OldSize);

        private bool SetSize(GraphDocument document, Vector2? size)
        {
            GraphNodeModel node = document?.TryGetNode(NodeId);
            if (node == null)
            {
                return false;
            }

            node.Size = size;
            document.NotifyGraphChanged();
            return true;
        }
    }

    public sealed class ConnectPortsCommand : IGraphCommand
    {
        private readonly GraphEdgeModel Edge;
        public string Name => "Connect Ports";

        public ConnectPortsCommand(GraphEdgeModel edge)
        {
            Edge = edge ?? throw new ArgumentNullException(nameof(edge));
        }

        public bool Execute(GraphDocument document)
        {
            if (document == null || document.TryGetEdge(Edge.Id) != null)
            {
                return false;
            }

            document.Connect(Edge.Id, Edge.SourceNodeId, Edge.SourcePortId, Edge.TargetNodeId, Edge.TargetPortId);
            return true;
        }

        public bool Undo(GraphDocument document) => document != null && document.Disconnect(Edge.Id);
    }

    public sealed class DisconnectPortsCommand : IGraphCommand
    {
        private readonly Guid EdgeId;
        private GraphEdgeModel Edge;
        public string Name => "Disconnect Ports";

        public DisconnectPortsCommand(Guid edgeId)
        {
            EdgeId = edgeId;
        }

        public bool Execute(GraphDocument document)
        {
            Edge = document?.TryGetEdge(EdgeId);
            return document != null && Edge != null && document.Disconnect(EdgeId);
        }

        public bool Undo(GraphDocument document)
        {
            if (document == null || Edge == null || document.TryGetEdge(Edge.Id) != null)
            {
                return false;
            }

            document.AddEdge(Edge, validate: true);
            return true;
        }
    }

    public sealed class CreateCommentCommand : IGraphCommand
    {
        private readonly GraphCommentModel Comment;
        public string Name => "Create Comment";

        public CreateCommentCommand(GraphCommentModel comment)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
        }

        public bool Execute(GraphDocument document)
        {
            if (document == null || document.TryGetComment(Comment.Id) != null)
            {
                return false;
            }

            document.AddComment(Comment);
            return true;
        }

        public bool Undo(GraphDocument document) => document != null && document.RemoveComment(Comment.Id);
    }

    public sealed class DeleteCommentCommand : IGraphCommand
    {
        private readonly Guid CommentId;
        private GraphCommentModel DeletedComment;
        public string Name => "Delete Comment";

        public DeleteCommentCommand(Guid commentId)
        {
            CommentId = commentId;
        }

        public bool Execute(GraphDocument document)
        {
            DeletedComment = document?.TryGetComment(CommentId);
            return DeletedComment != null && document.RemoveComment(CommentId);
        }

        public bool Undo(GraphDocument document)
        {
            if (document == null || DeletedComment == null || document.TryGetComment(CommentId) != null)
            {
                return false;
            }

            document.AddComment(DeletedComment);
            return true;
        }
    }

    public sealed class MoveCommentCommand : IGraphCommand
    {
        private readonly Guid CommentId;
        private readonly Rectangle OldBounds;
        private readonly Rectangle NewBounds;
        public string Name => "Move Comment";

        public MoveCommentCommand(Guid commentId, Rectangle oldBounds, Rectangle newBounds)
        {
            CommentId = commentId;
            OldBounds = oldBounds;
            NewBounds = newBounds;
        }

        public bool Execute(GraphDocument document) => SetBounds(document, NewBounds);

        public bool Undo(GraphDocument document) => SetBounds(document, OldBounds);

        private bool SetBounds(GraphDocument document, Rectangle bounds)
        {
            GraphCommentModel comment = document?.TryGetComment(CommentId);
            if (comment == null)
            {
                return false;
            }

            comment.Bounds = bounds;
            document.NotifyGraphChanged();
            return true;
        }
    }

    public sealed class ResizeCommentCommand : IGraphCommand
    {
        private readonly Guid CommentId;
        private readonly Rectangle OldBounds;
        private readonly Rectangle NewBounds;
        public string Name => "Resize Comment";

        public ResizeCommentCommand(Guid commentId, Rectangle oldBounds, Rectangle newBounds)
        {
            CommentId = commentId;
            OldBounds = oldBounds;
            NewBounds = newBounds;
        }

        public bool Execute(GraphDocument document) => SetBounds(document, NewBounds);

        public bool Undo(GraphDocument document) => SetBounds(document, OldBounds);

        private bool SetBounds(GraphDocument document, Rectangle bounds)
        {
            GraphCommentModel comment = document?.TryGetComment(CommentId);
            if (comment == null)
            {
                return false;
            }

            comment.Bounds = bounds;
            document.NotifyGraphChanged();
            return true;
        }
    }

    public sealed class EditCommentCommand : IGraphCommand
    {
        private readonly Guid CommentId;
        private readonly string OldTitle;
        private readonly string NewTitle;
        private readonly string OldText;
        private readonly string NewText;
        private readonly Rectangle OldBounds;
        private readonly Rectangle NewBounds;
        public string Name => "Edit Comment";

        public EditCommentCommand(Guid commentId, string oldTitle, string newTitle, string oldText, string newText, Rectangle oldBounds, Rectangle newBounds)
        {
            CommentId = commentId;
            OldTitle = oldTitle ?? string.Empty;
            NewTitle = newTitle ?? string.Empty;
            OldText = oldText ?? string.Empty;
            NewText = newText ?? string.Empty;
            OldBounds = oldBounds;
            NewBounds = newBounds;
        }

        public bool Execute(GraphDocument document) => Apply(document, NewTitle, NewText, NewBounds);

        public bool Undo(GraphDocument document) => Apply(document, OldTitle, OldText, OldBounds);

        private bool Apply(GraphDocument document, string title, string text, Rectangle bounds)
        {
            GraphCommentModel comment = document?.TryGetComment(CommentId);
            if (comment == null)
            {
                return false;
            }

            comment.Title = title ?? string.Empty;
            comment.Text = text ?? string.Empty;
            comment.Bounds = bounds;
            document.NotifyGraphChanged();
            return true;
        }
    }
}