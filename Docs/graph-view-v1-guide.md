# MGGraphView V1 Guide

`MGGraphView` is the V1 visual node graph control in `MGUI.Core`. It is a reusable editor surface for graph documents: nodes, ports, edges, comments, viewport navigation, selection, undo/redo, validation and JSON serialization.

The graph code is split into two layers:

- UI controls in `MGUI.Core.UI`: `MGGraphView`, `MGGraphNode`, `MGGraphPort`, `MGGraphCommentBox`.
- Model and services in `MGUI.Core.UI.Graph`: `GraphDocument`, models, commands, validation, serialization, geometry, culling and palette services.

The control is generic. Domain-specific node behavior belongs in the host application or sample code, not in `MGGraphView`.

## Minimal C# Graph

```csharp
using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

MGGraphView graphView = new(window)
{
    ShowGrid = true,
    AllowPan = true,
    AllowZoom = true,
    SnapToGrid = true,
};

GraphDocument document = graphView.Document;

GraphNodeModel start = document.AddNode(Guid.NewGuid(), "dialogue/start", "Start", new Vector2(40, 80));
GraphPortModel startNext = document.AddPort(
    start.Id,
    Guid.NewGuid(),
    "Next",
    GraphPortDirection.Output,
    GraphValueType.Exec,
    GraphPortCardinality.Multiple);

GraphNodeModel line = document.AddNode(Guid.NewGuid(), "dialogue/line", "Line", new Vector2(280, 80));
GraphPortModel lineIn = document.AddPort(
    line.Id,
    Guid.NewGuid(),
    "In",
    GraphPortDirection.Input,
    GraphValueType.Exec,
    GraphPortCardinality.Single,
    isRequired: true);

document.Connect(Guid.NewGuid(), start.Id, startNext.Id, line.Id, lineIn.Id);
graphView.FrameAll();
```

Use `GraphDocument` as the source of truth. The visual controls are synchronized from the document and should not be treated as the persisted state.

## Minimal XAML Host

```xml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Width="900" Height="600" TitleText="Graph">
    <GraphView Name="GraphView"
               ShowGrid="True"
               AllowPan="True"
               AllowZoom="True"
               SnapToGrid="True" />
</Window>
```

Then wire the model in code-behind:

```csharp
MGGraphView graphView = Window.GetElementByName<MGGraphView>("GraphView");
graphView.Document = CreateDocument();
graphView.NodePalette = CreatePalette();
```

See the working sample at `MGUI.Samples/Features/GraphViewDialogue.xaml` and `MGUI.Samples/Features/GraphViewDialogue.xaml.cs`. It is exposed from the Compendium as `Dialogue Graph`.

## Model Types

Core document types:

- `GraphDocument`: owns `Nodes`, `Edges`, `Comments`, metadata, validation settings and change notifications.
- `GraphNodeModel`: stable `Id`, `NodeType`, `Title`, `Position`, optional `Size`, `Ports`, `Properties`, `EditorMetadata` and collapse state.
- `GraphPortModel`: stable `Id`, `NodeId`, display name, `GraphPortDirection`, `GraphValueType`, `GraphPortCardinality`, required flag and optional custom type name.
- `GraphEdgeModel`: stable `Id`, source node/port, target node/port and render metadata.
- `GraphCommentModel`: stable `Id`, world-space `Bounds`, `Title`, `Text`, optional `Color` and editor metadata.

The built-in value types are defined by `GraphValueType`: `Float`, `Int`, `Bool`, `String`, vector types, `Color`, `Texture2D`, `Material`, `Entity`, `Exec`, `Object`, `Custom` and `Wildcard`.

## Node Palette

Context-menu creation is driven by `GraphNodePalette`:

```csharp
graphView.NodePalette = new GraphNodePalette(new[]
{
    new GraphNodeDefinition("dialogue/start", "Start", "Dialogue", new[]
    {
        new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
    }),
    new GraphNodeDefinition("dialogue/line", "Dialogue Line", "Dialogue", new[]
    {
        new GraphPortDefinition("In", GraphPortDirection.Input, GraphValueType.Exec, isRequired: true),
        new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
    }),
});
```

Right-clicking empty graph space opens an `MGContextMenu` that creates nodes through `CreateNodeCommand`. Dragging a port to empty space opens the same palette filtered to compatible definitions and connects the first compatible new port when possible.

`Ctrl+D` duplication is intentionally reserved for V2.

## Commands And Undo

Use `GraphCommandStack` for undoable mutations:

- `CreateNodeCommand`, `DeleteNodeCommand`, `MoveNodeCommand`, `MoveNodesCommand`, `ResizeNodeCommand`.
- `ConnectPortsCommand`, `DisconnectPortsCommand`.
- `CreateCommentCommand`, `DeleteCommentCommand`, `MoveCommentCommand`, `ResizeCommentCommand`.
- `GraphBatchCommand` groups several commands into one undo/redo entry.

`MGGraphView.Commands` is the local stack used by keyboard shortcuts and UI interactions.

## Validation

`GraphTypeCompatibilityService` validates source/target direction, type compatibility, duplicates, target single-cardinality and optional cycle prevention.

`GraphDocumentValidator` validates all edges and required input ports:

```csharp
GraphValidationResult result = new GraphDocumentValidator().Validate(graphView.Document);
if (!result.IsValid)
{
    foreach (GraphValidationIssue issue in result.Issues)
    {
        // Display issue.Code and issue.Message in the host UI.
    }
}
```

The validator does not own presentation. A host can set node metadata such as `HasError=true` and call `SynchronizeDocument()` to update visuals.

## Serialization

Use `GraphSerializer` for versioned JSON:

```csharp
GraphSerializer serializer = new();
GraphSerializationResult saved = serializer.Serialize(graphView.Document);

if (saved.Success)
{
    string json = saved.Json;
    GraphSerializationResult loaded = serializer.Deserialize(json);
    if (loaded.Success)
    {
        graphView.Document = loaded.Document;
    }
}
```

Serialization preserves document metadata, nodes, ports, edges, comments, positions, sizes, comment bounds/title/text/color and editor metadata. Unknown node types are preserved as data.

## Input

Mouse:

- Middle-drag pans the viewport.
- Mouse wheel zooms around the pointer.
- Left-click selects a node or comment box.
- Left-drag moves selected nodes or comments.
- Left-drag empty space starts rectangle selection.
- Drag from a port creates a connection or opens a compatible-node menu over empty space.
- Right-click empty graph space opens the node/comment creation menu.
- The bottom-right corner of a comment box performs simple resize.

Keyboard while `MGGraphView` owns focus:

- `Delete`: delete selected nodes, edges and comments through undoable commands.
- `Ctrl+Z`: undo.
- `Ctrl+Y`: redo.
- `Escape`: cancel active pan, node drag, comment drag, resize, rectangle selection or connection drag.
- `A`: frame all.
- `F`: frame selection.
- `Home`: frame origin.

Embedded text controls keep their own shortcuts when they own keyboard focus.

## Styling And Templates

The built-in templates are registered in `MGControlTemplateCatalog`:

- `MGControlTemplateCatalog.GraphViewTemplateName` requires `PART_OuterBorder`, `PART_ViewportHost`, `PART_NodesCanvas`, `PART_OverlayPanel`.
- `MGControlTemplateCatalog.GraphNodeTemplateName` requires `PART_OuterBorder`, `PART_HeaderTextBlock`, `PART_PortsPanel`, `PART_BodyPresenter`.
- `MGControlTemplateCatalog.GraphPortTemplateName` requires `PART_OuterBorder`, `PART_Label`.
- `MGControlTemplateCatalog.GraphCommentBoxTemplateName` requires `PART_OuterBorder`, `PART_TitleTextBlock`, `PART_BodyTextBlock`.

Theme defaults live under `MGTheme.Graph`:

- Graph surface: `Padding`, `BorderBrush`, `BorderThickness`, `CanvasBackground`, `GridLineBrush`, `EdgeBrush`.
- Nodes: `NodeBorderBrush`, `NodeBorderThickness`, `NodeHeaderBackground`, `NodeHeaderForeground`, `NodeBodyBackground`.
- Ports: `PortBackground`, `PortForeground`.
- Comments: `CommentBackground`, `CommentBorderBrush`.

Edges are rendered centrally by `MGGraphSurfaceCanvas`; they are not individual UI elements in V1.

## Performance Notes

V1 includes viewport culling and edge geometry caching:

- `EnableViewportCulling` and `CullingPadding` control culling behavior.
- Offscreen node and comment controls are not created until they enter the padded viewport; existing controls are collapsed and reused.
- Edges are drawn only when their approximate world bounds intersect the viewport, unless selected.
- `GraphEdgeGeometryCache` stores sampled Bezier polylines and invalidates when start/end, thickness, zoom or segment count changes.
- `CullingDiagnostics` reports visible/culled nodes, comments and edges, plus edge cache hits/misses.

## V1 Limits

The V1 graph intentionally does not include:

- Graph compiler or runtime evaluator.
- Blackboard variables.
- Clipboard copy/cut/paste.
- Duplicate, align and distribute commands.
- Search, minimap or bookmarks.
- Reroute nodes.
- Group boxes that move contained nodes.
- Advanced edge routing or edge labels.
- Automatic replacement of existing single-cardinality connections.
- PropertyGrid integration for node properties.
- Drag/drop assets from an external editor.

`Docs/graph_node_system_features.md` remains the functional vision and roadmap. Treat this guide as the V1 API and usage reference.