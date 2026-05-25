using System;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    internal static class GraphPortAnchorResolver
    {
        private const float MinimumPortTop = 20.0f;
        private const float FirstPortOffset = 36.0f;
        private const float PortStep = 24.0f;
        private const float MinimumPortBottomInset = 12.0f;

        public static bool TryGetPortWorldAnchor(GraphDocument document, Guid portId, out Vector2 worldAnchor)
        {
            worldAnchor = default;
            GraphPortModel port = document?.TryGetPort(portId);
            GraphNodeModel node = port == null ? null : document.TryGetNode(port.NodeId);
            return TryGetPortWorldAnchor(node, port, out worldAnchor);
        }

        public static bool TryGetPortWorldAnchor(GraphNodeModel node, GraphPortModel port, out Vector2 worldAnchor)
        {
            worldAnchor = default;
            if (node == null || port == null)
            {
                return false;
            }

            Vector2 nodeSize = GraphSelectionManager.GetNodeWorldSize(node);
            int portIndex = GetDirectionalPortIndex(node, port);
            float portY = Math.Min(Math.Max(MinimumPortTop, FirstPortOffset + portIndex * PortStep), Math.Max(MinimumPortTop, nodeSize.Y - MinimumPortBottomInset));
            float portX = port.Direction == GraphPortDirection.Input ? 0.0f : nodeSize.X;
            worldAnchor = node.Position + new Vector2(portX, portY);
            return true;
        }

        public static bool TryGetPortLayoutAnchor(GraphDocument document, GraphViewportTransform viewport, Guid portId, out Vector2 layoutAnchor)
        {
            layoutAnchor = default;
            if (viewport == null || !TryGetPortWorldAnchor(document, portId, out Vector2 worldAnchor))
            {
                return false;
            }

            layoutAnchor = viewport.WorldToLayout(worldAnchor);
            return true;
        }

        private static int GetDirectionalPortIndex(GraphNodeModel node, GraphPortModel port)
        {
            int index = 0;
            for (int candidateIndex = 0; candidateIndex < node.Ports.Count; candidateIndex++)
            {
                GraphPortModel candidate = node.Ports[candidateIndex];
                if (candidate == null || candidate.Direction != port.Direction)
                {
                    continue;
                }

                if (candidate.Id == port.Id)
                {
                    return index;
                }

                index++;
            }

            return 0;
        }
    }
}