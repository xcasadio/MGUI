using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphValidationTests
{
    [Fact]
    public void Validator_ReportsRequiredInputPortsThatAreNotConnected()
    {
        GraphDocument document = new();
        GraphNodeModel node = document.AddNode(Guid.NewGuid(), "DialogueLine", "Line", Vector2.Zero);
        GraphPortModel requiredInput = document.AddPort(node.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);

        GraphValidationResult result = new GraphDocumentValidator().Validate(document);

        Assert.False(result.IsValid);
        GraphValidationIssue issue = Assert.Single(result.Issues);
        Assert.Equal("RequiredPortUnconnected", issue.Code);
        Assert.Equal(node.Id, issue.NodeId);
        Assert.Equal(requiredInput.Id, issue.PortId);
    }

    [Fact]
    public void Validator_ReportsEdgesWithMissingReferences()
    {
        GraphDocument document = new();
        GraphEdgeModel invalidEdge = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        document.Edges.Add(invalidEdge);

        GraphValidationResult result = new GraphDocumentValidator().Validate(document);

        Assert.False(result.IsValid);
        GraphValidationIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GraphTypeCompatibilityService.MissingSourceNode, issue.Code);
        Assert.Equal(invalidEdge.Id, issue.EdgeId);
    }

    [Fact]
    public void Validator_AcceptsConnectedRequiredPortWithCompatibleTypes()
    {
        GraphDocument document = new();
        GraphNodeModel source = document.AddNode(Guid.NewGuid(), "Start", "Start", Vector2.Zero);
        GraphNodeModel target = document.AddNode(Guid.NewGuid(), "DialogueLine", "Line", Vector2.Zero);
        GraphPortModel sourcePort = document.AddPort(source.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        GraphPortModel targetPort = document.AddPort(target.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        document.Connect(Guid.NewGuid(), source.Id, sourcePort.Id, target.Id, targetPort.Id);

        GraphValidationResult result = new GraphDocumentValidator().Validate(document);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }
}