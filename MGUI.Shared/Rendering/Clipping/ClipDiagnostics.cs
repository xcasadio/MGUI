namespace MGUI.Shared.Rendering.Clipping
{
    public readonly record struct ClipDiagnosticsSnapshot(
        int ScissorClipCount,
        int StencilClipCount,
        int MaskClipCount,
        int MaxStencilDepth,
        int TemporaryRenderTargetRentCount,
        int TemporaryRenderTargetReuseCount)
    {
        public string ToDebugString()
            => $"clips[s:{ScissorClipCount} st:{StencilClipCount} m:{MaskClipCount}] stencilDepth={MaxStencilDepth} rt[rent:{TemporaryRenderTargetRentCount} reuse:{TemporaryRenderTargetReuseCount}]";
    }
}