using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphDocumentModelTests
{
    [Fact]
    public void EmptyDocument_HasVersionOneAndNoContent()
    {
        object document = CreateGraphDocument();

        Assert.Equal(1, Get<int>(document, "Version"));
        Assert.Equal(0, Count(Get<object>(document, "Nodes")));
        Assert.Equal(0, Count(Get<object>(document, "Edges")));
        Assert.Equal(0, Count(Get<object>(document, "Comments")));
    }

    [Fact]
    public void AddNode_PreservesStableGuidPositionAndEditorMetadata()
    {
        object document = CreateGraphDocument();
        Guid nodeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        object node = Invoke(document, "AddNode", nodeId, "DialogueLine", "Line", new Vector2(10, 20));

        Assert.Equal(nodeId, Get<Guid>(node, "Id"));
        Assert.Equal("DialogueLine", Get<string>(node, "NodeType"));
        Assert.Equal("Line", Get<string>(node, "Title"));
        Assert.Equal(new Vector2(10, 20), Get<Vector2>(node, "Position"));

        IDictionary metadata = Assert.IsAssignableFrom<IDictionary>(Get<object>(node, "EditorMetadata"));
        metadata["speaker"] = "Guide";

        object fetched = Invoke(document, "TryGetNode", nodeId);
        IDictionary fetchedMetadata = Assert.IsAssignableFrom<IDictionary>(Get<object>(fetched, "EditorMetadata"));
        Assert.Equal("Guide", fetchedMetadata["speaker"]);
    }

    [Fact]
    public void AddPort_StoresDirectionCardinalityTypeAndRequiredState()
    {
        object document = CreateGraphDocument();
        Guid nodeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid inputPortId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid outputPortId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        object inputDirection = EnumValue("GraphPortDirection", "Input");
        object outputDirection = EnumValue("GraphPortDirection", "Output");
        object stringType = EnumValue("GraphValueType", "String");
        object execType = EnumValue("GraphValueType", "Exec");
        object single = EnumValue("GraphPortCardinality", "Single");
        object multiple = EnumValue("GraphPortCardinality", "Multiple");

        Invoke(document, "AddNode", nodeId, "DialogueLine", "Line", Vector2.Zero);
        object input = Invoke(document, "AddPort", nodeId, inputPortId, "In", inputDirection, execType, single, true);
        object output = Invoke(document, "AddPort", nodeId, outputPortId, "Text", outputDirection, stringType, multiple, false);

        Assert.Equal(inputPortId, Get<Guid>(input, "Id"));
        Assert.Equal(nodeId, Get<Guid>(input, "NodeId"));
        Assert.Equal(inputDirection, Get<object>(input, "Direction"));
        Assert.Equal(execType, Get<object>(input, "ValueType"));
        Assert.Equal(single, Get<object>(input, "Cardinality"));
        Assert.True(Get<bool>(input, "IsRequired"));

        Assert.Equal(outputPortId, Get<Guid>(output, "Id"));
        Assert.Equal(outputDirection, Get<object>(output, "Direction"));
        Assert.Equal(stringType, Get<object>(output, "ValueType"));
        Assert.Equal(multiple, Get<object>(output, "Cardinality"));
    }

    [Fact]
    public void Connect_AddsEdge_WhenSourceAndTargetAreValid()
    {
        object document = CreateGraphDocument();
        Guid sourceNodeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Guid targetNodeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        Guid sourcePortId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid targetPortId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Guid edgeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        object outputDirection = EnumValue("GraphPortDirection", "Output");
        object inputDirection = EnumValue("GraphPortDirection", "Input");
        object execType = EnumValue("GraphValueType", "Exec");
        object single = EnumValue("GraphPortCardinality", "Single");

        Invoke(document, "AddNode", sourceNodeId, "Start", "Start", Vector2.Zero);
        Invoke(document, "AddNode", targetNodeId, "DialogueLine", "Line", new Vector2(200, 0));
        Invoke(document, "AddPort", sourceNodeId, sourcePortId, "Next", outputDirection, execType, single, false);
        Invoke(document, "AddPort", targetNodeId, targetPortId, "In", inputDirection, execType, single, true);

        object edge = Invoke(document, "Connect", edgeId, sourceNodeId, sourcePortId, targetNodeId, targetPortId);

        Assert.Equal(edgeId, Get<Guid>(edge, "Id"));
        Assert.Equal(sourceNodeId, Get<Guid>(edge, "SourceNodeId"));
        Assert.Equal(sourcePortId, Get<Guid>(edge, "SourcePortId"));
        Assert.Equal(targetNodeId, Get<Guid>(edge, "TargetNodeId"));
        Assert.Equal(targetPortId, Get<Guid>(edge, "TargetPortId"));
        Assert.Equal(1, Count(Get<object>(document, "Edges")));
    }

    [Fact]
    public void Connect_RejectsMissingNodesMissingPortsWrongDirectionsAndDuplicates()
    {
        object document = CreateGraphDocument();
        Guid sourceNodeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        Guid targetNodeId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        Guid sourcePortId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        Guid targetPortId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        Guid edgeId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        object outputDirection = EnumValue("GraphPortDirection", "Output");
        object inputDirection = EnumValue("GraphPortDirection", "Input");
        object execType = EnumValue("GraphValueType", "Exec");
        object single = EnumValue("GraphPortCardinality", "Single");

        AssertInvocationThrows(() => Invoke(document, "Connect", edgeId, sourceNodeId, sourcePortId, targetNodeId, targetPortId));

        Invoke(document, "AddNode", sourceNodeId, "Start", "Start", Vector2.Zero);
        Invoke(document, "AddNode", targetNodeId, "DialogueLine", "Line", Vector2.Zero);
        AssertInvocationThrows(() => Invoke(document, "Connect", edgeId, sourceNodeId, sourcePortId, targetNodeId, targetPortId));

        Invoke(document, "AddPort", sourceNodeId, sourcePortId, "Out", outputDirection, execType, single, false);
        Invoke(document, "AddPort", targetNodeId, targetPortId, "In", inputDirection, execType, single, false);
        Invoke(document, "Connect", edgeId, sourceNodeId, sourcePortId, targetNodeId, targetPortId);

        AssertInvocationThrows(() => Invoke(document, "Connect", Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId));
        AssertInvocationThrows(() => Invoke(document, "Connect", Guid.NewGuid(), targetNodeId, targetPortId, sourceNodeId, sourcePortId));
    }

    [Fact]
    public void RemoveNode_RemovesDependentEdges()
    {
        object document = CreateGraphDocument();
        Guid sourceNodeId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        Guid targetNodeId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        Guid sourcePortId = Guid.Parse("34343434-3434-3434-3434-343434343434");
        Guid targetPortId = Guid.Parse("56565656-5656-5656-5656-565656565656");
        object outputDirection = EnumValue("GraphPortDirection", "Output");
        object inputDirection = EnumValue("GraphPortDirection", "Input");
        object execType = EnumValue("GraphValueType", "Exec");
        object single = EnumValue("GraphPortCardinality", "Single");

        Invoke(document, "AddNode", sourceNodeId, "Start", "Start", Vector2.Zero);
        Invoke(document, "AddNode", targetNodeId, "DialogueLine", "Line", Vector2.Zero);
        Invoke(document, "AddPort", sourceNodeId, sourcePortId, "Out", outputDirection, execType, single, false);
        Invoke(document, "AddPort", targetNodeId, targetPortId, "In", inputDirection, execType, single, false);
        Invoke(document, "Connect", Guid.Parse("78787878-7878-7878-7878-787878787878"), sourceNodeId, sourcePortId, targetNodeId, targetPortId);

        bool removed = Invoke<bool>(document, "RemoveNode", sourceNodeId);

        Assert.True(removed);
        Assert.Equal(1, Count(Get<object>(document, "Nodes")));
        Assert.Equal(0, Count(Get<object>(document, "Edges")));
        Assert.Null(Invoke(document, "TryGetNode", sourceNodeId));
    }

    private static object CreateGraphDocument() => Activator.CreateInstance(GraphType("GraphDocument"))!;

    private static Type GraphType(string name)
        => Type.GetType($"MGUI.Core.UI.Graph.{name}, MGUI.Core", throwOnError: true)!;

    private static object EnumValue(string enumName, string value)
        => Enum.Parse(GraphType(enumName), value);

    private static T Get<T>(object target, string propertyName)
        => (T)GetValue(target, propertyName)!;

    private static T Invoke<T>(object target, string methodName, params object[] args)
        => (T)Invoke(target, methodName, args)!;

    private static object? GetValue(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName)
            ?? throw new MissingMemberException(target.GetType().FullName, propertyName);
        return property.GetValue(target);
    }

    private static object? Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethods().Single(x => x.Name == methodName && x.GetParameters().Length == args.Length);
        return method.Invoke(target, args);
    }

    private static void AssertInvocationThrows(Action action)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(action);
        Assert.NotNull(exception.InnerException);
    }

    private static int Count(object? collection)
    {
        if (collection is ICollection typedCollection)
        {
            return typedCollection.Count;
        }

        if (collection is IEnumerable enumerable)
        {
            int count = 0;
            foreach (object? _ in enumerable)
            {
                count++;
            }

            return count;
        }

        throw new InvalidOperationException("Expected a collection-like value.");
    }
}