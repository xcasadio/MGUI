using System;

namespace MGUI.Shared.Rendering
{
    /// <summary>
    /// Disposable render transaction contract used by UI code without requiring the concrete
    /// MonoGame transaction type in public signatures.
    /// </summary>
    public interface IUIDrawTransaction : IUIRenderContext, IDisposable { }
}