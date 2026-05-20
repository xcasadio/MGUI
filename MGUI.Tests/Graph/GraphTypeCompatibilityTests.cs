using MGUI.Core.UI.Graph;

namespace MGUI.Tests.Graph;

public class GraphTypeCompatibilityTests
{
    [Fact]
    public void AreTypesCompatible_AllowsExactTypesAndIntToFloat()
    {
        GraphTypeCompatibilityService service = new();

        Assert.True(service.AreTypesCompatible(Output(GraphValueType.Float), Input(GraphValueType.Float)));
        Assert.True(service.AreTypesCompatible(Output(GraphValueType.Int), Input(GraphValueType.Float)));
        Assert.True(service.AreTypesCompatible(Output(GraphValueType.String), Input(GraphValueType.Wildcard)));
    }

    [Fact]
    public void AreTypesCompatible_RejectsIncompatibleExecAndDataPorts()
    {
        GraphTypeCompatibilityService service = new();

        Assert.False(service.AreTypesCompatible(Output(GraphValueType.Exec), Input(GraphValueType.Float)));
        Assert.False(service.AreTypesCompatible(Output(GraphValueType.Texture2D), Input(GraphValueType.Color)));
    }

    [Fact]
    public void AreTypesCompatible_RequiresMatchingCustomTypeNames()
    {
        GraphTypeCompatibilityService service = new();
        GraphPortModel source = Output(GraphValueType.Custom, "DialogueLine");
        GraphPortModel matchingTarget = Input(GraphValueType.Custom, "DialogueLine");
        GraphPortModel mismatchedTarget = Input(GraphValueType.Custom, "BehaviorNode");

        Assert.True(service.AreTypesCompatible(source, matchingTarget));
        Assert.False(service.AreTypesCompatible(source, mismatchedTarget));
    }

    [Fact]
    public void ValidateConnection_RejectsSingleTargetAlreadyConnected()
    {
        GraphDocument document = CreateExecDocument(out GraphPortModel sourcePort, out GraphPortModel targetPort);
        GraphTypeCompatibilityService service = new();
        document.Connect(Guid.NewGuid(), sourcePort.NodeId, sourcePort.Id, targetPort.NodeId, targetPort.Id);

        GraphConnectionValidationResult result = service.ValidateConnection(document, sourcePort.NodeId, sourcePort.Id, targetPort.NodeId, targetPort.Id);

        Assert.False(result.IsValid);
        Assert.Equal(GraphTypeCompatibilityService.DuplicateConnection, result.Code);
    }

    [Fact]
    public void ValidateConnection_RejectsCyclesWhenDocumentDisallowsThem()
    {
        GraphDocument document = new() { DisallowCycles = true };
        GraphNodeModel first = document.AddNode(Guid.NewGuid(), "Step", "First", Microsoft.Xna.Framework.Vector2.Zero);
        GraphNodeModel second = document.AddNode(Guid.NewGuid(), "Step", "Second", Microsoft.Xna.Framework.Vector2.Zero);
        GraphPortModel firstIn = document.AddPort(first.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, false);
        GraphPortModel firstOut = document.AddPort(first.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        GraphPortModel secondIn = document.AddPort(second.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, false);
        GraphPortModel secondOut = document.AddPort(second.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        document.Connect(Guid.NewGuid(), first.Id, firstOut.Id, second.Id, secondIn.Id);

        GraphConnectionValidationResult result = new GraphTypeCompatibilityService().ValidateConnection(document, second.Id, secondOut.Id, first.Id, firstIn.Id);

        Assert.False(result.IsValid);
        Assert.Equal(GraphTypeCompatibilityService.CycleDetected, result.Code);
    }

    private static GraphDocument CreateExecDocument(out GraphPortModel sourcePort, out GraphPortModel targetPort)
    {
        GraphDocument document = new();
        GraphNodeModel source = document.AddNode(Guid.NewGuid(), "Start", "Start", Microsoft.Xna.Framework.Vector2.Zero);
        GraphNodeModel target = document.AddNode(Guid.NewGuid(), "Line", "Line", Microsoft.Xna.Framework.Vector2.Zero);
        sourcePort = document.AddPort(source.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        targetPort = document.AddPort(target.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, false);
        return document;
    }

    private static GraphPortModel Output(GraphValueType type, string customTypeName = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Out", GraphPortDirection.Output, type) { CustomTypeName = customTypeName };

    private static GraphPortModel Input(GraphValueType type, string customTypeName = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), "In", GraphPortDirection.Input, type) { CustomTypeName = customTypeName };
}