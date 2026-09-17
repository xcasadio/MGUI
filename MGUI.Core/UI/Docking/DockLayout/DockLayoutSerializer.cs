using System.Text.Json;
using System.Text.Json.Serialization;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Handles serialization and deserialization of DockLayoutModel to/from JSON.
/// Format 2.0 (no backward compatibility with 1.0, D7/D8): the tree (placeholder groups
/// included), the floating windows with their bounds, the auto-hidden panels and the
/// remembered placements are all persisted, so a reload can rebuild the layout completely.
/// </summary>
public static class DockLayoutSerializer
{
    private const string CurrentVersion = "2.0";

    #region DTOs (Data Transfer Objects)

    /// <summary>
    /// Root DTO for serialized layout.
    /// </summary>
    public class LayoutDto
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = CurrentVersion;

        [JsonPropertyName("rootNode")]
        public NodeDto RootNode { get; set; }

        [JsonPropertyName("floatingGroups")]
        public List<FloatingGroupDto> FloatingGroups { get; set; } = new List<FloatingGroupDto>();

        [JsonPropertyName("autoHide")]
        public List<AutoHideSectionDto> AutoHide { get; set; } = new List<AutoHideSectionDto>();

        [JsonPropertyName("placements")]
        public List<PlacementDto> Placements { get; set; } = new List<PlacementDto>();
    }

    /// <summary>
    /// Base DTO for all node types.
    /// </summary>
    public class NodeDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } // "Split", "TabGroup", or "Panel"

        [JsonPropertyName("id")]
        public string Id { get; set; }

        // Split-specific properties
        [JsonPropertyName("orientation")]
        public string Orientation { get; set; }

        [JsonPropertyName("splitRatio")]
        public float? SplitRatio { get; set; }

        [JsonPropertyName("minFirstSize")]
        public int? MinFirstSize { get; set; }

        [JsonPropertyName("minSecondSize")]
        public int? MinSecondSize { get; set; }

        [JsonPropertyName("firstChild")]
        public NodeDto FirstChild { get; set; }

        [JsonPropertyName("secondChild")]
        public NodeDto SecondChild { get; set; }

        // TabGroup-specific properties
        [JsonPropertyName("activePanelId")]
        public string ActivePanelId { get; set; }

        [JsonPropertyName("isDocumentArea")]
        public bool? IsDocumentArea { get; set; }

        [JsonPropertyName("panels")]
        public List<PanelDto> Panels { get; set; }
    }

    /// <summary>
    /// DTO for DockPanelNode.
    /// </summary>
    public class PanelDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } // Serialized as string (path or name)

        [JsonPropertyName("canClose")]
        public bool CanClose { get; set; } = true;

        [JsonPropertyName("canFloat")]
        public bool CanFloat { get; set; } = true;

        [JsonPropertyName("canAutoHide")]
        public bool CanAutoHide { get; set; } = true;

        [JsonPropertyName("isPinned")]
        public bool IsPinned { get; set; } = true;

        [JsonPropertyName("dockableType")]
        public string DockableType { get; set; } = "Tool";

        [JsonPropertyName("family")]
        public string Family { get; set; }

        [JsonPropertyName("drawerSize")]
        public int DrawerSize { get; set; } = 200;

        [JsonPropertyName("allowedZones")]
        public List<string> AllowedZones { get; set; }
    }

    /// <summary>
    /// DTO for a floating window: the tab group it shows, plus its screen bounds (P9).
    /// </summary>
    public class FloatingGroupDto
    {
        [JsonPropertyName("group")]
        public NodeDto Group { get; set; }

        [JsonPropertyName("left")]
        public int Left { get; set; }

        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    /// <summary>
    /// DTO for one side's auto-hide strip: its panels, in strip order.
    /// </summary>
    public class AutoHideSectionDto
    {
        [JsonPropertyName("side")]
        public string Side { get; set; }

        [JsonPropertyName("panels")]
        public List<PanelDto> Panels { get; set; } = new List<PanelDto>();
    }

    /// <summary>
    /// DTO for a remembered placement (<see cref="DockPanelPlacement"/>): the group and tab
    /// index a panel that left the tree (floated, auto-hidden or closed) should return to.
    /// </summary>
    public class PlacementDto
    {
        [JsonPropertyName("panelId")]
        public string PanelId { get; set; }

        [JsonPropertyName("groupId")]
        public string GroupId { get; set; }

        [JsonPropertyName("tabIndex")]
        public int TabIndex { get; set; }
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Serializes a DockLayoutModel to JSON string (format 2.0): the tree (placeholder groups
    /// included), the floating windows with their bounds, the auto-hidden panels per side and
    /// the remembered placements.
    /// </summary>
    /// <param name="model">The layout model to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation (default: true).</param>
    /// <returns>JSON string representation of the layout.</returns>
    public static string ToJson(DockLayoutModel model, bool indented = true)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var layoutDto = new LayoutDto
        {
            Version = CurrentVersion,
            RootNode = SerializeNode(model.RootNode),
            FloatingGroups = model.FloatingGroups.Select(fg => new FloatingGroupDto
            {
                Group = SerializeNode(fg.Group),
                Left = fg.Left,
                Top = fg.Top,
                Width = fg.Width,
                Height = fg.Height,
            }).ToList(),
            Placements = model.Placements.Select(kv => new PlacementDto
            {
                PanelId = kv.Key,
                GroupId = kv.Value.GroupId,
                TabIndex = kv.Value.TabIndex,
            }).ToList(),
        };

        foreach (var side in AutoHideSideOrder)
        {
            var panels = model.GetAutoHidePanels(side);
            if (panels.Count > 0)
            {
                layoutDto.AutoHide.Add(new AutoHideSectionDto
                {
                    Side = side.ToString(),
                    Panels = panels.Select(SerializePanel).ToList(),
                });
            }
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(layoutDto, options);
    }

    private static readonly AutoHideSide[] AutoHideSideOrder =
    {
        AutoHideSide.Left, AutoHideSide.Right, AutoHideSide.Top, AutoHideSide.Bottom
    };

    /// <summary>
    /// Recursively serializes a DockNode to DTO.
    /// </summary>
    private static NodeDto SerializeNode(DockNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is DockSplitNode splitNode)
        {
            return new NodeDto
            {
                Type = "Split",
                Id = splitNode.Id,
                Orientation = splitNode.Orientation.ToString(),
                SplitRatio = splitNode.SplitRatio,
                MinFirstSize = splitNode.MinFirstSize,
                MinSecondSize = splitNode.MinSecondSize,
                FirstChild = SerializeNode(splitNode.FirstChild),
                SecondChild = SerializeNode(splitNode.SecondChild)
            };
        }

        if (node is DockTabGroupNode tabGroupNode)
        {
            return new NodeDto
            {
                Type = "TabGroup",
                Id = tabGroupNode.Id,
                ActivePanelId = tabGroupNode.ActivePanelId,
                IsDocumentArea = tabGroupNode.IsDocumentArea ? true : (bool?)null,
                Panels = tabGroupNode.Panels.Select(SerializePanel).ToList()
            };
        }

        throw new InvalidOperationException($"Unknown node type: {node.GetType().Name}");
    }

    /// <summary>
    /// Serializes a DockPanelNode to DTO.
    /// </summary>
    private static PanelDto SerializePanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return null;
        }

        return new PanelDto
        {
            Id = panel.Id,
            Title = panel.Title ?? string.Empty,
            Icon = panel.Icon?.ToString(), // Convert icon to string representation
            CanClose = panel.CanClose,
            CanFloat = panel.CanFloat,
            CanAutoHide = panel.CanAutoHide,
            IsPinned = panel.IsPinned,
            DockableType = panel.DockableType.ToString(),
            Family = panel.Family,
            DrawerSize = panel.DrawerSize,
            AllowedZones = panel.AllowedZones?.Select(z => z.ToString()).ToList()
        };
    }

    #endregion

    #region Deserialization

    /// <summary>
    /// Deserializes a DockLayoutModel from JSON string. Throws on any failure (invalid JSON,
    /// a version other than 2.0, an unknown node type, or an inconsistent document - a
    /// duplicate panel id, or a placement for a panel that is also currently docked); see
    /// <see cref="TryFromJson"/> for a non-throwing equivalent (D7).
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="panelFactory">Factory function to create panel content. Takes panel ID and returns ContentFactory.</param>
    /// <returns>Deserialized DockLayoutModel.</returns>
    public static DockLayoutModel FromJson(string json, Func<string, Func<MGElement>> panelFactory = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        if (!TryDeserializeCore(json, panelFactory, out var model, out var diagnostics))
        {
            throw new InvalidOperationException(
                diagnostics.Count > 0
                    ? string.Join(" ", diagnostics)
                    : "Failed to deserialize layout.");
        }

        return model;
    }

    /// <summary>
    /// Attempts to deserialize a DockLayoutModel from JSON string. Never throws: on failure
    /// (invalid JSON, a version other than 2.0, an unknown node type, or an inconsistent
    /// document) it returns false with at least one diagnostic and <paramref name="model"/> is
    /// null (D7).
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="panelFactory">Factory function to create panel content. Takes panel ID and returns ContentFactory.</param>
    /// <param name="model">The deserialized model on success, otherwise null.</param>
    /// <param name="diagnostics">One diagnostic message per problem found. Empty on success.</param>
    /// <returns>True when the document was successfully deserialized.</returns>
    public static bool TryFromJson(string json, Func<string, Func<MGElement>> panelFactory, out DockLayoutModel model, out IReadOnlyList<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            model = null;
            diagnostics = new List<string> { "Layout JSON is empty." };
            return false;
        }

        var success = TryDeserializeCore(json, panelFactory, out model, out var diagnosticsList);
        diagnostics = diagnosticsList;
        return success;
    }

    /// <summary>
    /// Shared deserialization core used by both <see cref="FromJson"/> and
    /// <see cref="TryFromJson"/>. Parses the JSON, validates the version, deserializes the tree,
    /// the floating groups and the auto-hide sections (P8), checks the document's consistency
    /// (no duplicate panel id across the tree/floating groups/auto-hide sections, no placement
    /// for a panel that is also currently docked), then builds the model.
    /// </summary>
    private static bool TryDeserializeCore(string json, Func<string, Func<MGElement>> panelFactory, out DockLayoutModel model, out List<string> diagnostics)
    {
        diagnostics = new List<string>();
        model = null;

        LayoutDto layoutDto;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            layoutDto = JsonSerializer.Deserialize<LayoutDto>(json, options);
        }
        catch (JsonException ex)
        {
            diagnostics.Add($"Invalid layout JSON: {ex.Message}");
            return false;
        }

        if (layoutDto == null)
        {
            diagnostics.Add("Failed to deserialize layout: result is null.");
            return false;
        }

        if (layoutDto.Version != CurrentVersion)
        {
            diagnostics.Add($"Unsupported layout version '{layoutDto.Version ?? "(none)"}'. Expected '{CurrentVersion}'.");
            return false;
        }

        var nodeDiagnostics = new List<string>();
        var rawTree = DeserializeNode(layoutDto.RootNode, panelFactory, nodeDiagnostics);

        var floatingGroups = new List<DockFloatingGroup>();
        if (layoutDto.FloatingGroups != null)
        {
            foreach (var fgDto in layoutDto.FloatingGroups)
            {
                if (fgDto?.Group == null)
                {
                    continue;
                }

                var groupNode = DeserializeNode(fgDto.Group, panelFactory, nodeDiagnostics) as DockTabGroupNode;
                if (groupNode == null)
                {
                    if (fgDto.Group.Type != "TabGroup")
                    {
                        nodeDiagnostics.Add($"Floating group entry has an invalid node type '{fgDto.Group.Type}'.");
                    }

                    continue;
                }

                if (groupNode.Panels.Count == 0)
                {
                    // A floating group left with no panel (every panel dropped by the factory,
                    // or the group was already empty) is dropped (P8).
                    continue;
                }

                floatingGroups.Add(new DockFloatingGroup(groupNode, fgDto.Left, fgDto.Top, fgDto.Width, fgDto.Height));
            }
        }

        var autoHideBySide = new List<(AutoHideSide Side, List<DockPanelNode> Panels)>();
        if (layoutDto.AutoHide != null)
        {
            foreach (var sectionDto in layoutDto.AutoHide)
            {
                if (sectionDto == null || !Enum.TryParse<AutoHideSide>(sectionDto.Side, true, out var side))
                {
                    nodeDiagnostics.Add($"Auto-hide section has an unknown side '{sectionDto?.Side}'.");
                    continue;
                }

                var panels = new List<DockPanelNode>();
                if (sectionDto.Panels != null)
                {
                    foreach (var panelDto in sectionDto.Panels)
                    {
                        var panel = DeserializePanel(panelDto, panelFactory, nodeDiagnostics);
                        if (panel != null)
                        {
                            panels.Add(panel);
                        }
                    }
                }

                autoHideBySide.Add((side, panels));
            }
        }

        if (nodeDiagnostics.Count > 0)
        {
            diagnostics.AddRange(nodeDiagnostics);
            return false;
        }

        // Consistency: no panel id may appear more than once across the tree, the floating
        // groups and the auto-hide sections (P8).
        var treeIds = new List<string>();
        CollectPanelIds(rawTree, treeIds);
        var floatingIds = floatingGroups.SelectMany(fg => fg.Group.Panels.Select(p => p.Id)).ToList();
        var autoHideIds = autoHideBySide.SelectMany(s => s.Panels.Select(p => p.Id)).ToList();

        var allPresentIds = treeIds.Concat(floatingIds).Concat(autoHideIds).ToList();
        var duplicates = allPresentIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            diagnostics.Add($"Panel id(s) appear more than once across the tree, the floating groups and the auto-hide sections: {string.Join(", ", duplicates)}.");
            return false;
        }

        // Every panel id the document attempted to create (whether or not the factory actually
        // produced content for it) versus the ids that made it into the model: the difference is
        // the set of panels the factory skipped, whose placement (if any) is dropped with them.
        var attemptedIds = new HashSet<string>();
        CollectPanelDtoIds(layoutDto.RootNode, attemptedIds);
        if (layoutDto.FloatingGroups != null)
        {
            foreach (var fgDto in layoutDto.FloatingGroups)
            {
                CollectPanelDtoIds(fgDto?.Group, attemptedIds);
            }
        }

        if (layoutDto.AutoHide != null)
        {
            foreach (var sectionDto in layoutDto.AutoHide)
            {
                if (sectionDto?.Panels == null)
                {
                    continue;
                }

                foreach (var p in sectionDto.Panels)
                {
                    if (p != null && !string.IsNullOrEmpty(p.Id))
                    {
                        attemptedIds.Add(p.Id);
                    }
                }
            }
        }

        var presentIdSet = new HashSet<string>(allPresentIds);
        var droppedIds = new HashSet<string>(attemptedIds.Where(id => !presentIdSet.Contains(id)));
        var dockedIdSet = new HashSet<string>(treeIds);

        var placements = new Dictionary<string, DockPanelPlacement>();
        if (layoutDto.Placements != null)
        {
            foreach (var placementDto in layoutDto.Placements)
            {
                if (placementDto == null || string.IsNullOrEmpty(placementDto.PanelId) || string.IsNullOrEmpty(placementDto.GroupId))
                {
                    continue;
                }

                if (droppedIds.Contains(placementDto.PanelId))
                {
                    // The panel this placement belongs to was skipped by the factory: its
                    // placement is dropped with it (P8).
                    continue;
                }

                if (dockedIdSet.Contains(placementDto.PanelId))
                {
                    diagnostics.Add($"Panel '{placementDto.PanelId}' has a remembered placement but is also currently docked in the tree.");
                    return false;
                }

                placements[placementDto.PanelId] = new DockPanelPlacement(placementDto.GroupId, placementDto.TabIndex);
            }
        }

        var referencedGroupIds = new HashSet<string>(placements.Values.Select(p => p.GroupId));
        var cleanedRoot = CleanupInvalidNodes(rawTree, referencedGroupIds) ?? new DockTabGroupNode();

        model = new DockLayoutModel(cleanedRoot);

        foreach (var fg in floatingGroups)
        {
            model.AddFloatingGroup(fg);
        }

        foreach (var (side, panels) in autoHideBySide)
        {
            foreach (var panel in panels)
            {
                model.AddToAutoHide(panel, side);
            }
        }

        foreach (var kv in placements)
        {
            model.SetPlacement(kv.Key, kv.Value);
        }

        return true;
    }

    /// <summary>
    /// Recursively collects the ids of every <see cref="DockPanelNode"/> under <paramref name="node"/>.
    /// </summary>
    private static void CollectPanelIds(DockNode node, ICollection<string> ids)
    {
        switch (node)
        {
            case null:
                return;
            case DockTabGroupNode group:
                foreach (var p in group.Panels)
                {
                    ids.Add(p.Id);
                }

                return;
            case DockSplitNode split:
                CollectPanelIds(split.FirstChild, ids);
                CollectPanelIds(split.SecondChild, ids);
                return;
        }
    }

    /// <summary>
    /// Recursively collects the ids of every <see cref="PanelDto"/> under <paramref name="node"/>,
    /// regardless of whether the panel factory will later accept or skip them.
    /// </summary>
    private static void CollectPanelDtoIds(NodeDto node, ICollection<string> ids)
    {
        if (node == null)
        {
            return;
        }

        if (node.Type == "TabGroup" && node.Panels != null)
        {
            foreach (var p in node.Panels)
            {
                if (p != null && !string.IsNullOrEmpty(p.Id))
                {
                    ids.Add(p.Id);
                }
            }
        }
        else if (node.Type == "Split")
        {
            CollectPanelDtoIds(node.FirstChild, ids);
            CollectPanelDtoIds(node.SecondChild, ids);
        }
    }

    /// <summary>
    /// Recursively deserializes a NodeDto to DockNode. An unrecognized <see cref="NodeDto.Type"/>
    /// adds a diagnostic (when <paramref name="diagnostics"/> is not null) and returns null.
    /// </summary>
    private static DockNode DeserializeNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory, List<string> diagnostics)
    {
        if (dto == null)
        {
            return null;
        }

        switch (dto.Type)
        {
            case "Split":
                return DeserializeSplitNode(dto, panelFactory, diagnostics);

            case "TabGroup":
                return DeserializeTabGroupNode(dto, panelFactory, diagnostics);

            default:
                diagnostics?.Add($"Unknown node type '{dto.Type}'.");
                return null;
        }
    }

    /// <summary>
    /// Deserializes a DockSplitNode. Returns null with a diagnostic when the id is missing or
    /// blank, instead of letting <see cref="DockNode"/>'s constructor throw (D7).
    /// </summary>
    private static DockSplitNode DeserializeSplitNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory, List<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            diagnostics?.Add("A split node is missing its 'id'.");
            return null;
        }

        var splitNode = new DockSplitNode(dto.Id);

        if (Enum.TryParse<Orientation>(dto.Orientation, true, out var orientation))
        {
            splitNode.Orientation = orientation;
        }
        else
        {
            splitNode.Orientation = Orientation.Horizontal;
        }

        splitNode.SplitRatio = dto.SplitRatio ?? 0.5f;
        splitNode.MinFirstSize = dto.MinFirstSize ?? 100;
        splitNode.MinSecondSize = dto.MinSecondSize ?? 100;

        splitNode.FirstChild = DeserializeNode(dto.FirstChild, panelFactory, diagnostics);
        splitNode.SecondChild = DeserializeNode(dto.SecondChild, panelFactory, diagnostics);

        return splitNode;
    }

    /// <summary>
    /// Deserializes a DockTabGroupNode. Returns null with a diagnostic when the id is missing or
    /// blank, instead of letting <see cref="DockNode"/>'s constructor throw (D7).
    /// </summary>
    private static DockTabGroupNode DeserializeTabGroupNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory, List<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            diagnostics?.Add("A tab group node is missing its 'id'.");
            return null;
        }

        var tabGroupNode = new DockTabGroupNode(dto.Id);

        if (dto.Panels != null)
        {
            foreach (var panelDto in dto.Panels)
            {
                var panel = DeserializePanel(panelDto, panelFactory, diagnostics);
                if (panel != null)
                {
                    tabGroupNode.Panels.Add(panel);
                }
            }
        }

        // Set active panel
        if (!string.IsNullOrEmpty(dto.ActivePanelId))
        {
            // Check if the active panel exists in the deserialized panels
            if (tabGroupNode.Panels.Any(p => p.Id == dto.ActivePanelId))
            {
                tabGroupNode.SetActivePanel(dto.ActivePanelId);
            }
            else
            {
                if (tabGroupNode.Panels.Count > 0)
                {
                    tabGroupNode.SetActivePanel(tabGroupNode.Panels[0].Id);
                }
            }
        }
        else if (tabGroupNode.Panels.Count > 0)
        {
            // Default to first panel if no active panel specified
            tabGroupNode.SetActivePanel(tabGroupNode.Panels[0].Id);
        }

        // Restore document area flag
        if (dto.IsDocumentArea == true)
        {
            tabGroupNode.IsDocumentArea = true;
        }

        return tabGroupNode;
    }

    /// <summary>
    /// Deserializes a DockPanelNode. Returns null with a diagnostic when the id is missing or
    /// blank, instead of letting <see cref="DockNode"/>'s constructor throw (D7).
    /// </summary>
    private static DockPanelNode DeserializePanel(PanelDto dto, Func<string, Func<MGElement>> panelFactory, List<string> diagnostics)
    {
        if (dto == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            diagnostics?.Add("A panel is missing its 'id'.");
            return null;
        }

        // Check if panel factory can provide content for this panel ID
        Func<MGElement> contentFactory = null;
        if (panelFactory != null)
        {
            contentFactory = panelFactory(dto.Id);

            // If factory returns null, it means the panel is not available/registered
            if (contentFactory == null)
            {
                return null;
            }
        }

        var panel = new DockPanelNode(dto.Id)
        {
            Title = dto.Title ?? "Untitled",
            Icon = dto.Icon, // Store as string; view layer can convert to texture
            CanClose = dto.CanClose,
            CanFloat = dto.CanFloat,
            CanAutoHide = dto.CanAutoHide,
            IsPinned = dto.IsPinned,
            ContentFactory = contentFactory,
            Family = dto.Family,
            DrawerSize = dto.DrawerSize > 0 ? dto.DrawerSize : 200
        };

        // Restore dockable type
        if (!string.IsNullOrEmpty(dto.DockableType) &&
            Enum.TryParse<DockableType>(dto.DockableType, true, out var dockableType))
        {
            panel.DockableType = dockableType;
        }

        // Restore allowed zones (null = all zones permitted)
        if (dto.AllowedZones != null && dto.AllowedZones.Count > 0)
        {
            var zones = new List<DockZone>();
            foreach (var z in dto.AllowedZones)
            {
                if (Enum.TryParse<DockZone>(z, true, out var zone))
                {
                    zones.Add(zone);
                }
            }
            if (zones.Count > 0)
            {
                panel.AllowedZones = zones.AsReadOnly();
            }
        }

        return panel;
    }

    /// <summary>
    /// Recursively cleans up invalid nodes in the deserialized tree. Removes an empty tab group
    /// unless a remembered placement still references it (P3/P8), in which case it is kept as a
    /// hidden placeholder; collapses/removes invalid split nodes exactly as before.
    /// </summary>
    /// <param name="node">The node to clean up.</param>
    /// <param name="referencedGroupIds">Ids of tab groups a remembered placement still references.</param>
    /// <returns>The cleaned node, or null if the node should be removed.</returns>
    private static DockNode CleanupInvalidNodes(DockNode node, HashSet<string> referencedGroupIds)
    {
        if (node == null)
        {
            return null;
        }

        // Handle tab group nodes
        if (node is DockTabGroupNode tabGroup)
        {
            // Remove an empty tab group unless a placement still references it.
            if (tabGroup.Panels.Count == 0 && !referencedGroupIds.Contains(tabGroup.Id))
            {
                return null;
            }

            return tabGroup;
        }

        // Handle split nodes
        if (node is DockSplitNode splitNode)
        {
            // Recursively clean up children
            splitNode.FirstChild = CleanupInvalidNodes(splitNode.FirstChild, referencedGroupIds);
            splitNode.SecondChild = CleanupInvalidNodes(splitNode.SecondChild, referencedGroupIds);

            // Both children null → remove this split node
            if (splitNode.FirstChild == null && splitNode.SecondChild == null)
            {
                return null;
            }

            // One child null → promote the remaining child
            if (splitNode.FirstChild == null)
            {
                return splitNode.SecondChild;
            }

            if (splitNode.SecondChild == null)
            {
                return splitNode.FirstChild;
            }

            // Both children valid → keep split node
            return splitNode;
        }

        // Unknown node type or leaf node
        return node;
    }

    #endregion
}
