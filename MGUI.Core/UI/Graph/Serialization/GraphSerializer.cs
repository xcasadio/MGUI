using System.Text.Json;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph;

public sealed class GraphSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly GraphMigrationService MigrationService;

    public GraphSerializer()
        : this(new GraphMigrationService())
    {
    }

    public GraphSerializer(GraphMigrationService migrationService)
    {
        MigrationService = migrationService ?? new GraphMigrationService();
    }

    public GraphSerializationResult Serialize(GraphDocument document)
    {
        if (document == null)
        {
            return GraphSerializationResult.Failed("Graph document is required.");
        }

        string json = JsonSerializer.Serialize(ToDto(document), JsonOptions);
        return GraphSerializationResult.FromJson(json);
    }

    public GraphSerializationResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GraphSerializationResult.Failed("Graph JSON is empty.");
        }

        try
        {
            GraphDocumentDto dto = JsonSerializer.Deserialize<GraphDocumentDto>(json, JsonOptions);
            if (dto == null)
            {
                return GraphSerializationResult.Failed("Graph JSON did not contain a document.");
            }

            GraphSerializationResult result = GraphSerializationResult.FromDocument(MigrationService.Migrate(FromDto(dto), null));
            MigrationService.Migrate(result.Document, result.Diagnostics);
            result.Success = result.Document != null;
            return result;
        }
        catch (JsonException ex)
        {
            return GraphSerializationResult.Failed(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return GraphSerializationResult.Failed(ex.Message);
        }
    }

    private static GraphDocumentDto ToDto(GraphDocument document)
    {
        GraphDocumentDto dto = new()
        {
            Version = document.Version,
            DisallowCycles = document.DisallowCycles,
            EditorMetadata = Copy(document.EditorMetadata),
        };

        for (int nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
        {
            GraphNodeModel node = document.Nodes[nodeIndex];
            GraphNodeDto nodeDto = new()
            {
                Id = node.Id,
                NodeType = node.NodeType,
                Title = node.Title,
                X = node.Position.X,
                Y = node.Position.Y,
                Width = node.Size?.X,
                Height = node.Size?.Y,
                IsCollapsed = node.IsCollapsed,
                Properties = Copy(node.Properties),
                EditorMetadata = Copy(node.EditorMetadata),
            };

            for (int portIndex = 0; portIndex < node.Ports.Count; portIndex++)
            {
                GraphPortModel port = node.Ports[portIndex];
                nodeDto.Ports.Add(new GraphPortDto
                {
                    Id = port.Id,
                    NodeId = port.NodeId,
                    Name = port.Name,
                    Direction = port.Direction,
                    ValueType = port.ValueType,
                    Cardinality = port.Cardinality,
                    IsRequired = port.IsRequired,
                    DefaultValue = port.DefaultValue,
                    CustomTypeName = port.CustomTypeName,
                    EditorMetadata = Copy(port.EditorMetadata),
                });
            }

            dto.Nodes.Add(nodeDto);
        }

        for (int edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
        {
            GraphEdgeModel edge = document.Edges[edgeIndex];
            dto.Edges.Add(new GraphEdgeDto
            {
                Id = edge.Id,
                SourceNodeId = edge.SourceNodeId,
                SourcePortId = edge.SourcePortId,
                TargetNodeId = edge.TargetNodeId,
                TargetPortId = edge.TargetPortId,
                RenderMetadata = Copy(edge.RenderMetadata),
            });
        }

        for (int commentIndex = 0; commentIndex < document.Comments.Count; commentIndex++)
        {
            GraphCommentModel comment = document.Comments[commentIndex];
            GraphCommentDto commentDto = new()
            {
                Id = comment.Id,
                X = comment.Bounds.X,
                Y = comment.Bounds.Y,
                Width = comment.Bounds.Width,
                Height = comment.Bounds.Height,
                Title = comment.Title,
                Text = comment.Text,
                EditorMetadata = Copy(comment.EditorMetadata),
            };

            if (comment.Color.HasValue)
            {
                Color color = comment.Color.Value;
                commentDto.HasColor = true;
                commentDto.ColorR = color.R;
                commentDto.ColorG = color.G;
                commentDto.ColorB = color.B;
                commentDto.ColorA = color.A;
            }

            dto.Comments.Add(commentDto);
        }

        return dto;
    }

    private static GraphDocument FromDto(GraphDocumentDto dto)
    {
        GraphDocument document = new()
        {
            Version = dto.Version,
            DisallowCycles = dto.DisallowCycles,
            EditorMetadata = Copy(dto.EditorMetadata),
        };

        for (int nodeIndex = 0; nodeIndex < dto.Nodes.Count; nodeIndex++)
        {
            GraphNodeDto nodeDto = dto.Nodes[nodeIndex];
            GraphNodeModel node = new(nodeDto.Id, nodeDto.NodeType, nodeDto.Title, new Vector2(nodeDto.X, nodeDto.Y))
            {
                Size = nodeDto.Width.HasValue && nodeDto.Height.HasValue ? new Vector2(nodeDto.Width.Value, nodeDto.Height.Value) : null,
                IsCollapsed = nodeDto.IsCollapsed,
                Properties = Copy(nodeDto.Properties),
                EditorMetadata = Copy(nodeDto.EditorMetadata),
            };

            for (int portIndex = 0; portIndex < nodeDto.Ports.Count; portIndex++)
            {
                GraphPortDto portDto = nodeDto.Ports[portIndex];
                node.Ports.Add(new GraphPortModel(portDto.NodeId == Guid.Empty ? node.Id : portDto.NodeId, portDto.Id, portDto.Name, portDto.Direction, portDto.ValueType, portDto.Cardinality, portDto.IsRequired)
                {
                    DefaultValue = portDto.DefaultValue,
                    CustomTypeName = portDto.CustomTypeName,
                    EditorMetadata = Copy(portDto.EditorMetadata),
                });
            }

            document.AddNode(node);
        }

        for (int edgeIndex = 0; edgeIndex < dto.Edges.Count; edgeIndex++)
        {
            GraphEdgeDto edgeDto = dto.Edges[edgeIndex];
            document.AddEdge(new GraphEdgeModel(edgeDto.Id, edgeDto.SourceNodeId, edgeDto.SourcePortId, edgeDto.TargetNodeId, edgeDto.TargetPortId)
            {
                RenderMetadata = Copy(edgeDto.RenderMetadata),
            }, validate: false);
        }

        for (int commentIndex = 0; commentIndex < dto.Comments.Count; commentIndex++)
        {
            GraphCommentDto commentDto = dto.Comments[commentIndex];
            GraphCommentModel comment = new(commentDto.Id, new Rectangle(commentDto.X, commentDto.Y, commentDto.Width, commentDto.Height), commentDto.Title, commentDto.Text)
            {
                EditorMetadata = Copy(commentDto.EditorMetadata),
            };

            if (commentDto.HasColor)
            {
                comment.Color = new Color(commentDto.ColorR, commentDto.ColorG, commentDto.ColorB, commentDto.ColorA);
            }

            document.AddComment(comment);
        }

        return document;
    }

    private static Dictionary<string, string> Copy(Dictionary<string, string> source)
        => source == null ? new(StringComparer.Ordinal) : new(source, StringComparer.Ordinal);

    private sealed class GraphDocumentDto
    {
        public int Version { get; set; }
        public bool DisallowCycles { get; set; }
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);
        public List<GraphNodeDto> Nodes { get; set; } = new();
        public List<GraphEdgeDto> Edges { get; set; } = new();
        public List<GraphCommentDto> Comments { get; set; } = new();
    }

    private sealed class GraphNodeDto
    {
        public Guid Id { get; set; }
        public string NodeType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float? Width { get; set; }
        public float? Height { get; set; }
        public bool IsCollapsed { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);
        public List<GraphPortDto> Ports { get; set; } = new();
    }

    private sealed class GraphPortDto
    {
        public Guid Id { get; set; }
        public Guid NodeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public GraphPortDirection Direction { get; set; }
        public GraphValueType ValueType { get; set; }
        public GraphPortCardinality Cardinality { get; set; }
        public bool IsRequired { get; set; }
        public string DefaultValue { get; set; }
        public string CustomTypeName { get; set; }
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class GraphEdgeDto
    {
        public Guid Id { get; set; }
        public Guid SourceNodeId { get; set; }
        public Guid SourcePortId { get; set; }
        public Guid TargetNodeId { get; set; }
        public Guid TargetPortId { get; set; }
        public Dictionary<string, string> RenderMetadata { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class GraphCommentDto
    {
        public Guid Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool HasColor { get; set; }
        public byte ColorR { get; set; }
        public byte ColorG { get; set; }
        public byte ColorB { get; set; }
        public byte ColorA { get; set; }
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);
    }
}