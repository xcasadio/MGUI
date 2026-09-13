using System.Globalization;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph;

public sealed class GraphSelectionManager
{
    public const string AutoMeasuredWidthMetadataKey = "__GraphAutoWidth";
    public const string AutoMeasuredHeightMetadataKey = "__GraphAutoHeight";

    public HashSet<Guid> SelectedNodeIds { get; }
    public HashSet<Guid> SelectedEdgeIds { get; }

    public GraphSelectionManager()
        : this(new HashSet<Guid>(), new HashSet<Guid>()) { }

    public GraphSelectionManager(HashSet<Guid> selectedNodeIds, HashSet<Guid> selectedEdgeIds)
    {
        SelectedNodeIds = selectedNodeIds ?? throw new ArgumentNullException(nameof(selectedNodeIds));
        SelectedEdgeIds = selectedEdgeIds ?? throw new ArgumentNullException(nameof(selectedEdgeIds));
    }

    public bool Clear()
    {
        var changed = SelectedNodeIds.Count > 0 || SelectedEdgeIds.Count > 0;
        SelectedNodeIds.Clear();
        SelectedEdgeIds.Clear();
        return changed;
    }

    public bool SelectNode(Guid nodeId, bool additive = false, bool toggle = false)
    {
        if (nodeId == Guid.Empty)
        {
            return false;
        }

        var changed = false;
        if (!additive)
        {
            changed |= ClearExceptNode(nodeId);
        }

        if (toggle && SelectedNodeIds.Contains(nodeId))
        {
            SelectedNodeIds.Remove(nodeId);
            return true;
        }

        changed |= SelectedNodeIds.Add(nodeId);
        SelectedEdgeIds.Clear();
        return changed;
    }

    public bool SelectEdge(Guid edgeId, bool additive = false, bool toggle = false)
    {
        if (edgeId == Guid.Empty)
        {
            return false;
        }

        var changed = false;
        if (!additive)
        {
            changed |= ClearExceptEdge(edgeId);
        }

        if (toggle && SelectedEdgeIds.Contains(edgeId))
        {
            SelectedEdgeIds.Remove(edgeId);
            return true;
        }

        changed |= SelectedEdgeIds.Add(edgeId);
        return changed;
    }

    public bool SelectNodes(IEnumerable<Guid> nodeIds, bool additive = false)
    {
        var changed = false;
        if (!additive)
        {
            changed |= Clear();
        }

        if (nodeIds == null)
        {
            return changed;
        }

        foreach (var nodeId in nodeIds)
        {
            if (nodeId != Guid.Empty)
            {
                changed |= SelectedNodeIds.Add(nodeId);
            }
        }

        return changed;
    }

    public bool SelectNodesInRectangle(GraphDocument document, RectangleF worldRectangle, bool additive = false)
    {
        var changed = false;
        if (!additive)
        {
            changed |= Clear();
        }

        if (document == null || worldRectangle.Width <= 0.0f || worldRectangle.Height <= 0.0f)
        {
            return changed;
        }

        for (var nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
        {
            var node = document.Nodes[nodeIndex];
            if (node != null && Intersects(worldRectangle, GetNodeWorldBounds(node)))
            {
                changed |= SelectedNodeIds.Add(node.Id);
            }
        }

        return changed;
    }

    public bool IsNodeSelected(Guid nodeId) => SelectedNodeIds.Contains(nodeId);

    public static Vector2 GetNodeWorldSize(GraphNodeModel node)
    {
        if (node == null)
        {
            return new Vector2(1.0f, 1.0f);
        }

        if (node.Size.HasValue)
        {
            return ClampSize(node.Size.Value);
        }

        return TryGetAutoMeasuredWorldSize(node, out var measured)
            ? measured
            : new Vector2(160.0f, 100.0f);
    }

    public static void SetAutoMeasuredWorldSize(GraphNodeModel node, Vector2 size)
    {
        if (node?.EditorMetadata == null)
        {
            return;
        }

        var clamped = ClampSize(size);
        node.EditorMetadata[AutoMeasuredWidthMetadataKey] = clamped.X.ToString(CultureInfo.InvariantCulture);
        node.EditorMetadata[AutoMeasuredHeightMetadataKey] = clamped.Y.ToString(CultureInfo.InvariantCulture);
    }

    public static void ClearAutoMeasuredWorldSize(GraphNodeModel node)
    {
        if (node?.EditorMetadata == null)
        {
            return;
        }

        node.EditorMetadata.Remove(AutoMeasuredWidthMetadataKey);
        node.EditorMetadata.Remove(AutoMeasuredHeightMetadataKey);
    }

    public static RectangleF GetNodeWorldBounds(GraphNodeModel node)
    {
        if (node == null)
        {
            return new RectangleF();
        }

        var size = GetNodeWorldSize(node);
        return new RectangleF(node.Position.X, node.Position.Y, Math.Max(1.0f, size.X), Math.Max(1.0f, size.Y));
    }

    private static bool TryGetAutoMeasuredWorldSize(GraphNodeModel node, out Vector2 size)
    {
        size = default;
        if (node?.EditorMetadata == null)
        {
            return false;
        }

        if (!node.EditorMetadata.TryGetValue(AutoMeasuredWidthMetadataKey, out var widthText)
            || !node.EditorMetadata.TryGetValue(AutoMeasuredHeightMetadataKey, out var heightText)
            || !float.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            || !float.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            return false;
        }

        size = ClampSize(new Vector2(width, height));
        return true;
    }

    private static Vector2 ClampSize(Vector2 size)
        => new(Math.Max(1.0f, size.X), Math.Max(1.0f, size.Y));

    private bool ClearExceptNode(Guid nodeId)
    {
        var changed = false;
        if (SelectedEdgeIds.Count > 0)
        {
            SelectedEdgeIds.Clear();
            changed = true;
        }

        if (SelectedNodeIds.Count == 1 && SelectedNodeIds.Contains(nodeId))
        {
            return changed;
        }

        if (SelectedNodeIds.Count > 0)
        {
            SelectedNodeIds.Clear();
            changed = true;
        }

        return changed;
    }

    private bool ClearExceptEdge(Guid edgeId)
    {
        var changed = false;
        if (SelectedNodeIds.Count > 0)
        {
            SelectedNodeIds.Clear();
            changed = true;
        }

        if (SelectedEdgeIds.Count == 1 && SelectedEdgeIds.Contains(edgeId))
        {
            return changed;
        }

        if (SelectedEdgeIds.Count > 0)
        {
            SelectedEdgeIds.Clear();
            changed = true;
        }

        return changed;
    }

    private static bool Intersects(RectangleF first, RectangleF second)
        => first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;
}