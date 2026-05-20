using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    public sealed class GraphPortDefinition
    {
        public string Name { get; }
        public GraphPortDirection Direction { get; }
        public GraphValueType ValueType { get; }
        public GraphPortCardinality Cardinality { get; }
        public bool IsRequired { get; }

        public GraphPortDefinition(string name, GraphPortDirection direction, GraphValueType valueType,
            GraphPortCardinality cardinality = GraphPortCardinality.Single, bool isRequired = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? direction.ToString() : name;
            Direction = direction;
            ValueType = valueType;
            Cardinality = cardinality;
            IsRequired = isRequired;
        }
    }

    public sealed class GraphNodeDefinition
    {
        public string NodeType { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public IReadOnlyList<GraphPortDefinition> Ports { get; }

        public GraphNodeDefinition(string nodeType, string displayName, string category, IEnumerable<GraphPortDefinition> ports)
        {
            NodeType = string.IsNullOrWhiteSpace(nodeType) ? throw new ArgumentException("Node type is required.", nameof(nodeType)) : nodeType;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? NodeType : displayName;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            Ports = ports == null ? Array.Empty<GraphPortDefinition>() : ports.Where(port => port != null).ToList();
        }

        public GraphNodeModel CreateNode(Guid nodeId, Vector2 position)
        {
            GraphNodeModel node = new(nodeId, NodeType, DisplayName, position);
            for (int portIndex = 0; portIndex < Ports.Count; portIndex++)
            {
                GraphPortDefinition port = Ports[portIndex];
                node.Ports.Add(new GraphPortModel(nodeId, Guid.NewGuid(), port.Name, port.Direction, port.ValueType, port.Cardinality, port.IsRequired));
            }

            return node;
        }
    }

    public sealed class GraphNodePalette
    {
        private readonly GraphTypeCompatibilityService CompatibilityService = new();
        public IReadOnlyList<GraphNodeDefinition> Definitions { get; }

        public GraphNodePalette(IEnumerable<GraphNodeDefinition> definitions)
        {
            Definitions = definitions == null ? Array.Empty<GraphNodeDefinition>() : definitions.Where(definition => definition != null).ToList();
        }

        public IEnumerable<GraphNodeDefinition> GetDefinitions(GraphPortModel draggedPort = null)
        {
            if (draggedPort == null)
            {
                return Definitions;
            }

            return Definitions.Where(definition => TryFindCompatiblePort(definition, draggedPort, out _));
        }

        public bool TryFindCompatiblePort(GraphNodeDefinition definition, GraphPortModel draggedPort, out GraphPortDefinition compatiblePort)
        {
            compatiblePort = null;
            if (definition == null || draggedPort == null)
            {
                return false;
            }

            for (int portIndex = 0; portIndex < definition.Ports.Count; portIndex++)
            {
                GraphPortDefinition candidate = definition.Ports[portIndex];
                if (AreCompatible(draggedPort, candidate))
                {
                    compatiblePort = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryFindCompatiblePort(GraphNodeModel node, GraphPortModel draggedPort, out GraphPortModel compatiblePort)
        {
            compatiblePort = null;
            if (node == null || draggedPort == null)
            {
                return false;
            }

            for (int portIndex = 0; portIndex < node.Ports.Count; portIndex++)
            {
                GraphPortModel candidate = node.Ports[portIndex];
                if (AreCompatible(draggedPort, candidate))
                {
                    compatiblePort = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool AreCompatible(GraphPortModel draggedPort, GraphPortDefinition candidate)
        {
            GraphPortModel candidateModel = new(Guid.Empty, Guid.NewGuid(), candidate.Name, candidate.Direction, candidate.ValueType, candidate.Cardinality, candidate.IsRequired);
            return AreCompatible(draggedPort, candidateModel);
        }

        private bool AreCompatible(GraphPortModel draggedPort, GraphPortModel candidate)
        {
            if (draggedPort.Direction == GraphPortDirection.Output && candidate.Direction == GraphPortDirection.Input)
            {
                return CompatibilityService.AreTypesCompatible(draggedPort, candidate);
            }

            if (draggedPort.Direction == GraphPortDirection.Input && candidate.Direction == GraphPortDirection.Output)
            {
                return CompatibilityService.AreTypesCompatible(candidate, draggedPort);
            }

            return false;
        }

        public static GraphNodePalette CreateDefault()
            => new(new[]
            {
                new GraphNodeDefinition("dialogue/start", "Start", "Dialogue", new[]
                {
                    new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
                }),
                new GraphNodeDefinition("dialogue/line", "Line", "Dialogue", new[]
                {
                    new GraphPortDefinition("In", GraphPortDirection.Input, GraphValueType.Exec),
                    new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
                }),
                new GraphNodeDefinition("value/float", "Float", "Values", new[]
                {
                    new GraphPortDefinition("Value", GraphPortDirection.Output, GraphValueType.Float, GraphPortCardinality.Multiple),
                }),
                new GraphNodeDefinition("math/add", "Add", "Math", new[]
                {
                    new GraphPortDefinition("A", GraphPortDirection.Input, GraphValueType.Float, isRequired: true),
                    new GraphPortDefinition("B", GraphPortDirection.Input, GraphValueType.Float, isRequired: true),
                    new GraphPortDefinition("Result", GraphPortDirection.Output, GraphValueType.Float, GraphPortCardinality.Multiple),
                }),
            });
    }
}