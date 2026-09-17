using System.Runtime.CompilerServices;

// Allow MGUI.Tests to access internal members such as the DockOperation placement
// helpers (AutoHidePanel, RestoreToPlacement, ClosePanel, ForgetPlacement,
// ResolvePlacementGroup, CollectUnreferencedPlaceholders) which are needed to verify
// placeholder-group restore behaviour (floating, auto-hide, close/reopen).
[assembly: InternalsVisibleTo("MGUI.Tests")]
[assembly: InternalsVisibleTo("CasaEngine.Tests")]
