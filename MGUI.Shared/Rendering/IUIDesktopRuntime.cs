using System;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace MGUI.Shared.Rendering
{
    /// <summary>Desktop-facing runtime contract used by high-level UI orchestration.</summary>
    public interface IUIDesktopRuntime
    {
        public InputTracker Input { get; }
        public FontManager FontManager { get; }
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public ITextMeasurementEngine TextEngine { get; set; }
        public event EventHandler<EventArgs<ITextMeasurementEngine>> TextEngineChanged;
        public UpdateBaseArgs UpdateArgs { get; }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin, DrawContext DefaultContext = DrawContext.Sprites);
        public void RegisterView(IUIView View);
    }
}