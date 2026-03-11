using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Handles serialization and deserialization of DockLayoutModel to/from JSON.
/// </summary>
public static class DockLayoutSerializer
{
    private const string CurrentVersion = "1.0";

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

    #endregion

    #region Serialization (5.1)

    /// <summary>
    /// Serializes a DockLayoutModel to JSON string.
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
            RootNode = SerializeNode(model.RootNode)
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(layoutDto, options);
    }

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

    #region Deserialization (5.2)

    /// <summary>
    /// Deserializes a DockLayoutModel from JSON string.
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var layoutDto = JsonSerializer.Deserialize<LayoutDto>(json, options);

        if (layoutDto == null)
        {
            throw new InvalidOperationException("Failed to deserialize layout: result is null.");
        }

        if (layoutDto.Version != CurrentVersion)
        {
            System.Diagnostics.Debug.WriteLine($"[DockLayoutSerializer] Warning: Layout version mismatch. Expected {CurrentVersion}, got {layoutDto.Version}. Attempting to load anyway.");
        }

        var rootNode = DeserializeNode(layoutDto.RootNode, panelFactory);
        rootNode = CleanupInvalidNodes(rootNode);

        if (rootNode == null)
        {
            rootNode = new DockTabGroupNode();
        }

        return new DockLayoutModel(rootNode);
    }

    /// <summary>
    /// Recursively deserializes a NodeDto to DockNode.
    /// </summary>
    private static DockNode DeserializeNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory)
    {
        if (dto == null)
        {
            return null;
        }

        switch (dto.Type)
        {
            case "Split":
                return DeserializeSplitNode(dto, panelFactory);

            case "TabGroup":
                return DeserializeTabGroupNode(dto, panelFactory);

            default:
                return null;
        }
    }

    /// <summary>
    /// Deserializes a DockSplitNode.
    /// </summary>
    private static DockSplitNode DeserializeSplitNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory)
    {
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

        splitNode.FirstChild = DeserializeNode(dto.FirstChild, panelFactory);
        splitNode.SecondChild = DeserializeNode(dto.SecondChild, panelFactory);

        return splitNode;
    }

    /// <summary>
    /// Deserializes a DockTabGroupNode.
    /// </summary>
    private static DockTabGroupNode DeserializeTabGroupNode(NodeDto dto, Func<string, Func<MGElement>> panelFactory)
    {
        var tabGroupNode = new DockTabGroupNode(dto.Id);

        int skippedCount = 0;

        if (dto.Panels != null)
        {
            foreach (var panelDto in dto.Panels)
            {
                var panel = DeserializePanel(panelDto, panelFactory);
                if (panel != null)
                {
                    tabGroupNode.Panels.Add(panel);
                }
                else
                {
                    skippedCount++;
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
    /// Deserializes a DockPanelNode.
    /// </summary>
    private static DockPanelNode DeserializePanel(PanelDto dto, Func<string, Func<MGElement>> panelFactory)
    {
        if (dto == null)
        {
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
    /// Recursively cleans up invalid nodes in the deserialized tree.
    /// Removes empty tab groups and collapses/removes invalid split nodes.
    /// </summary>
    /// <param name="node">The node to clean up.</param>
    /// <returns>The cleaned node, or null if the node should be removed.</returns>
    private static DockNode CleanupInvalidNodes(DockNode node)
    {
        if (node == null)
        {
            return null;
        }

        // Handle tab group nodes
        if (node is DockTabGroupNode tabGroup)
        {
            // Remove empty tab groups
            if (tabGroup.Panels.Count == 0)
            {
                return null;
            }

            // Tab group is valid if it has at least one panel
            return tabGroup;
        }

        // Handle split nodes
        if (node is DockSplitNode splitNode)
        {
            // Recursively clean up children
            splitNode.FirstChild = CleanupInvalidNodes(splitNode.FirstChild);
            splitNode.SecondChild = CleanupInvalidNodes(splitNode.SecondChild);

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
