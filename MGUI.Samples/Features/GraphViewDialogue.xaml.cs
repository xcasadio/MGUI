using System;
using System.IO;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features;

public class GraphViewDialogueSample : SampleBase
{
    private readonly GraphSerializer Serializer = new();
    private readonly GraphDocumentValidator Validator = new();
    private readonly string SavePath = Path.Combine(Path.GetTempPath(), "MGUI.GraphViewDialogue.json");

    private MGGraphView GraphView;
    private MGTextBlock StatusText;
    private GraphDocument ObservedDocument;
    private string SavedJson = string.Empty;

    public GraphViewDialogueSample(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "GraphViewDialogue.xaml")
    {
        GraphView = Window.GetElementByName<MGGraphView>("DialogueGraphView");
        StatusText = Window.GetElementByName<MGTextBlock>("GraphStatusText");

        GraphView.NodePalette = CreateDialoguePalette();
        UseDocument(CreateSampleDocument(), frameAll: true);
        SavedJson = Serializer.Serialize(GraphView.Document).Json;

        Window.GetElementByName<MGButton>("FrameAllButton").MouseHandler.LMBReleasedInside += (_, _) => GraphView.FrameAll(new Rectangle(0, 0, 900, 540));
        Window.GetElementByName<MGButton>("UndoButton").MouseHandler.LMBReleasedInside += (_, _) => { GraphView.Commands.Undo(GraphView.Document); ValidateGraph("Undo applied."); };
        Window.GetElementByName<MGButton>("RedoButton").MouseHandler.LMBReleasedInside += (_, _) => { GraphView.Commands.Redo(GraphView.Document); ValidateGraph("Redo applied."); };
        Window.GetElementByName<MGButton>("ValidateButton").MouseHandler.LMBReleasedInside += (_, _) => ValidateGraph("Validation refreshed.");
        Window.GetElementByName<MGButton>("SaveGraphButton").MouseHandler.LMBReleasedInside += (_, _) => SaveGraph();
        Window.GetElementByName<MGButton>("LoadGraphButton").MouseHandler.LMBReleasedInside += (_, _) => LoadGraph();
        Window.GetElementByName<MGButton>("ResetGraphButton").MouseHandler.LMBReleasedInside += (_, _) => UseDocument(CreateSampleDocument(), frameAll: true, statusPrefix: "Sample reset.");
    }

    private void UseDocument(GraphDocument document, bool frameAll, string statusPrefix = null)
    {
        if (ObservedDocument != null)
        {
            ObservedDocument.GraphChanged -= OnGraphChanged;
        }

        GraphView.Document = document ?? new GraphDocument();
        ObservedDocument = GraphView.Document;
        ObservedDocument.GraphChanged += OnGraphChanged;

        if (frameAll)
        {
            GraphView.FrameAll(new Rectangle(0, 0, 900, 540));
        }

        ValidateGraph(statusPrefix);
    }

    private void OnGraphChanged(object sender, EventArgs e)
        => ValidateGraph();

    private void SaveGraph()
    {
        GraphSerializationResult result = Serializer.Serialize(GraphView.Document);
        if (!result.Success)
        {
            SetStatus("Save failed: " + string.Join("; ", result.Diagnostics));
            return;
        }

        SavedJson = result.Json;
        File.WriteAllText(SavePath, SavedJson);
        ValidateGraph($"Saved JSON to {SavePath}.");
    }

    private void LoadGraph()
    {
        string json = File.Exists(SavePath) ? File.ReadAllText(SavePath) : SavedJson;
        GraphSerializationResult result = Serializer.Deserialize(json);
        if (!result.Success)
        {
            SetStatus("Load failed: " + string.Join("; ", result.Diagnostics));
            return;
        }

        UseDocument(result.Document, frameAll: true, statusPrefix: "Loaded saved JSON.");
    }

    private void ValidateGraph(string statusPrefix = null)
    {
        GraphValidationResult result = Validator.Validate(GraphView.Document);
        ApplyValidationState(result);

        string summary = result.IsValid
            ? "Validation: OK."
            : $"Validation: {result.Issues.Count} issue(s): " + string.Join(" | ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));

        SetStatus(string.IsNullOrWhiteSpace(statusPrefix) ? summary : $"{statusPrefix} {summary}");
    }

    private void ApplyValidationState(GraphValidationResult result)
    {
        foreach (GraphNodeModel node in GraphView.Document.Nodes)
        {
            node.EditorMetadata.Remove("HasError");
            node.EditorMetadata.Remove("HasWarning");
        }

        foreach (GraphValidationIssue issue in result.Issues)
        {
            if (issue.NodeId.HasValue)
            {
                GraphNodeModel node = GraphView.Document.TryGetNode(issue.NodeId.Value);
                if (node != null)
                {
                    node.EditorMetadata[issue.Severity == GraphValidationSeverity.Error ? "HasError" : "HasWarning"] = "true";
                }
            }
        }

        GraphView.SynchronizeDocument();
    }

    private void SetStatus(string text)
    {
        if (StatusText != null)
        {
            StatusText.Text = text ?? string.Empty;
        }
    }

    private static GraphNodePalette CreateDialoguePalette()
        => new(new[]
        {
            new GraphNodeDefinition("dialogue/start", "Start", "Dialogue", new[]
            {
                new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
            }),
            new GraphNodeDefinition("dialogue/line", "Dialogue Line", "Dialogue", new[]
            {
                new GraphPortDefinition("In", GraphPortDirection.Input, GraphValueType.Exec, isRequired: true),
                new GraphPortDefinition("Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
                new GraphPortDefinition("Speaker", GraphPortDirection.Output, GraphValueType.String, GraphPortCardinality.Multiple),
            }),
            new GraphNodeDefinition("dialogue/choice", "Choice", "Dialogue", new[]
            {
                new GraphPortDefinition("In", GraphPortDirection.Input, GraphValueType.Exec, isRequired: true),
                new GraphPortDefinition("Selected", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple),
            }),
            new GraphNodeDefinition("dialogue/end", "End", "Dialogue", new[]
            {
                new GraphPortDefinition("In", GraphPortDirection.Input, GraphValueType.Exec, isRequired: true),
            }),
        });

    private static GraphDocument CreateSampleDocument()
    {
        GraphDocument document = new() { DisallowCycles = true };

        GraphNodeModel start = document.AddNode(Guid.NewGuid(), "dialogue/start", "Start", new Vector2(-360, -120));
        GraphPortModel startNext = document.AddPort(start.Id, Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);

        GraphNodeModel line = document.AddNode(Guid.NewGuid(), "dialogue/line", "Line: Welcome", new Vector2(-120, -140));
        line.Properties["Speaker"] = "Guide";
        line.Properties["Text"] = "Welcome. Choose how to answer.";
        GraphPortModel lineIn = document.AddPort(line.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        GraphPortModel lineNext = document.AddPort(line.Id, Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);
        GraphPortModel lineSpeaker = document.AddPort(line.Id, Guid.NewGuid(), "Speaker", GraphPortDirection.Output, GraphValueType.String, GraphPortCardinality.Multiple);

        GraphNodeModel choice = document.AddNode(Guid.NewGuid(), "dialogue/choice", "Choice: Ask", new Vector2(160, -170));
        choice.Properties["Text"] = "Ask about the ruins";
        GraphPortModel choiceIn = document.AddPort(choice.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        GraphPortModel choiceSelected = document.AddPort(choice.Id, Guid.NewGuid(), "Selected", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);

        GraphNodeModel end = document.AddNode(Guid.NewGuid(), "dialogue/end", "End", new Vector2(430, -125));
        GraphPortModel endIn = document.AddPort(end.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);

        GraphNodeModel orphanChoice = document.AddNode(Guid.NewGuid(), "dialogue/choice", "Choice: Missing Input", new Vector2(155, 80));
        document.AddPort(orphanChoice.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        document.AddPort(orphanChoice.Id, Guid.NewGuid(), "Selected", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);

        document.Connect(Guid.NewGuid(), start.Id, startNext.Id, line.Id, lineIn.Id);
        document.Connect(Guid.NewGuid(), line.Id, lineNext.Id, choice.Id, choiceIn.Id);
        document.Connect(Guid.NewGuid(), choice.Id, choiceSelected.Id, end.Id, endIn.Id);
        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), line.Id, lineSpeaker.Id, end.Id, endIn.Id), validate: false);

        document.AddComment(Guid.NewGuid(), new Rectangle(-390, -210, 360, 70), "V1 sample", "Right click empty space to add nodes. Drag ports to connect compatible nodes.");
        document.AddComment(Guid.NewGuid(), new Rectangle(120, 30, 360, 95), "Validation demo", "This sample intentionally includes one missing required input and one incompatible string-to-exec edge.");

        return document;
    }
}