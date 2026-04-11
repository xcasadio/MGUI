using System;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace MGUI.Shared.Rendering
{
    /// <summary>Desktop-facing subset of <see cref="MainRenderer"/> used by high-level UI orchestration.</summary>
    public interface IUIDesktopRuntime
    {
        public InputTracker Input { get; }
        public FontManager FontManager { get; }
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public ITextEngine TextEngine { get; set; }
        public event EventHandler<EventArgs<ITextEngine>> TextEngineChanged;
        public UpdateBaseArgs UpdateArgs { get; }

        public void RegisterView(IUIView View);
    }
}