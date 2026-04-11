namespace MGUI.Shared.Text.Engines
{
    /// <summary>
    /// Composite MonoGame text backend contract.
    /// <see cref="ITextMeasurementEngine"/> covers the backend-neutral resolution and measurement
    /// surface consumed by core UI code, while <see cref="IMonoGameTextRenderer"/> covers the
    /// concrete SpriteBatch draw path used by the MonoGame renderer.
    /// </summary>
    public interface ITextEngine : ITextMeasurementEngine, IMonoGameTextRenderer { }
}
