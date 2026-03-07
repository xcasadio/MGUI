using System.Runtime.CompilerServices;

// Allow MGUI.Tests to access internal members such as
// DockPanelNode.AutoHideReturnGroup / AutoHideReturnZone / AutoHideReturnSplitRatio
// which are needed to verify UnpinPanel snapshot behaviour.
[assembly: InternalsVisibleTo("MGUI.Tests")]
