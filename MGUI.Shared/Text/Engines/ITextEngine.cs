namespace MGUI.Shared.Text.Engines
{
    /// <summary>
    /// Composite text engine contract combining backend-neutral measurement and draw responsibilities.
    /// <see cref="ITextMeasurementEngine"/> covers the backend-neutral resolution and measurement
    /// surface consumed by core UI code, while <see cref="ITextDrawEngine"/> covers the
    /// backend-owned draw path used by the active renderer.
    /// </summary>
    public interface ITextEngine : ITextMeasurementEngine, ITextDrawEngine { }
}
