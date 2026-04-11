namespace MGUI.Shared.Assets
{
    /// <summary>Opaque image resource handle shared between core UI code and concrete rendering backends.</summary>
    public interface IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; }
    }
}