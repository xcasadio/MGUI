namespace MGUI.Core.UI.Docking
{
    /// <summary>
    /// Defines the zones where a panel can be docked relative to a target node.
    /// </summary>
    public enum DockZone
    {
        /// <summary>No valid docking zone.</summary>
        None,
        
        /// <summary>Dock to the left side (creates horizontal split).</summary>
        Left,
        
        /// <summary>Dock to the right side (creates horizontal split).</summary>
        Right,
        
        /// <summary>Dock to the top side (creates vertical split).</summary>
        Top,
        
        /// <summary>Dock to the bottom side (creates vertical split).</summary>
        Bottom,
        
        /// <summary>Dock as a tab in the center (adds to existing tab group).</summary>
        Center
    }
}
