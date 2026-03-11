using System;
using MGUI.Shared.Rendering;

namespace MGUI.Core.UI
{
    /// <summary>Runtime view wrapper that attaches an <see cref="MGDesktop"/> to a specific <see cref="IUISurface"/>.</summary>
    public class UIView : IUIView
    {
        public MGDesktop Desktop { get; }
        public IUISurface Surface { get; }

        public UIView(MGDesktop Desktop, IUISurface Surface)
        {
            this.Desktop = Desktop ?? throw new ArgumentNullException(nameof(Desktop));
            this.Surface = Surface ?? throw new ArgumentNullException(nameof(Surface));

            Desktop.AttachView(this);
        }

        public void Update() => Desktop.Update();

        public void Draw(DrawTransaction DT, float opacity = 1.0f)
        {
            if (DT == null)
                throw new ArgumentNullException(nameof(DT));

            using (Surface.GetRenderTarget() != null ? DT.SetRenderTargetTemporary(Surface.GetRenderTarget(), null) : null)
            {
                Desktop.Draw(DT, opacity);
            }
        }
    }
}