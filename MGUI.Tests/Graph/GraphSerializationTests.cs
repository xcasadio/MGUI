using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphSerializationTests
{
    [Fact]
    public void Serializer_RoundTripsNodesPortsEdgesCommentsAndMetadata()
    {
        GraphDocument document = new() { DisallowCycles = true };
        document.EditorMetadata["viewport"] = "saved";
        GraphNodeModel source = document.AddNode(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Start", "Start", new Vector2(10, 20));
        GraphNodeModel target = document.AddNode(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "DialogueLine", "Line", new Vector2(200, 40));
        target.Size = new Vector2(180, 120);
        target.Properties["Text"] = "Hello";
        target.EditorMetadata["speaker"] = "Guide";
        GraphPortModel output = document.AddPort(source.Id, Guid.Parse("11111111-1111-1111-1111-111111111111"), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        GraphPortModel input = document.AddPort(target.Id, Guid.Parse("22222222-2222-2222-2222-222222222222"), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        GraphEdgeModel edge = document.Connect(Guid.Parse("33333333-3333-3333-3333-333333333333"), source.Id, output.Id, target.Id, input.Id);
        edge.RenderMetadata["route"] = "bezier";
        GraphCommentModel comment = document.AddComment(Guid.Parse("44444444-4444-4444-4444-444444444444"), new Rectangle(0, 0, 320, 180), "Intro", "Opening branch");
        comment.Color = new Color(12, 34, 56, 200);

        GraphSerializer serializer = new();
        GraphSerializationResult serializeResult = serializer.Serialize(document);
        GraphSerializationResult deserializeResult = serializer.Deserialize(serializeResult.Json);

        Assert.True(serializeResult.Success);
        Assert.True(deserializeResult.Success);
        GraphDocument roundTrip = deserializeResult.Document;
        Assert.Equal(2, roundTrip.Nodes.Count);
        Assert.Single(roundTrip.Edges);
        Assert.Single(roundTrip.Comments);
        Assert.True(roundTrip.DisallowCycles);
        Assert.Equal("saved", roundTrip.EditorMetadata["viewport"]);
        Assert.Equal(target.Id, roundTrip.Nodes[1].Id);
        Assert.Equal(new Vector2(200, 40), roundTrip.Nodes[1].Position);
        Assert.Equal(new Vector2(180, 120), roundTrip.Nodes[1].Size);
        Assert.Equal("Hello", roundTrip.Nodes[1].Properties["Text"]);
        Assert.Equal("Guide", roundTrip.Nodes[1].EditorMetadata["speaker"]);
        Assert.Equal(edge.Id, roundTrip.Edges[0].Id);
        Assert.Equal("bezier", roundTrip.Edges[0].RenderMetadata["route"]);
        Assert.Equal(comment.Id, roundTrip.Comments[0].Id);
        Assert.Equal(new Rectangle(0, 0, 320, 180), roundTrip.Comments[0].Bounds);
        Assert.Equal(new Color(12, 34, 56, 200), roundTrip.Comments[0].Color);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsDiagnosticInsteadOfThrowing()
    {
        GraphSerializationResult result = new GraphSerializer().Deserialize("{ invalid json");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Serializer_PreservesUnknownNodeTypesAsData()
    {
        GraphDocument document = new();
        Guid unknownId = Guid.NewGuid();
        document.AddNode(unknownId, "Mystery.Plugin.Node", "Unknown", Vector2.Zero);

        GraphSerializer serializer = new();
        GraphDocument roundTrip = serializer.Deserialize(serializer.Serialize(document).Json).Document;

        Assert.Equal("Mystery.Plugin.Node", roundTrip.TryGetNode(unknownId)!.NodeType);
    }
}