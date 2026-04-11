namespace MGUI.Shared.Rendering
{
    /// <summary>Represents a runtime UI view attached to a logical surface.</summary>
    public interface IUIView
    {
        public IUISurface Surface { get; }
        public void Update();
        public void Draw(IUIDrawTransaction DT, float opacity = 1.0f);
    }
}