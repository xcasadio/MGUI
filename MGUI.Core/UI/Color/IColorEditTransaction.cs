namespace MGUI.Core.UI;

/// <summary>Receives the logical lifecycle of a color edit. MGUI.Core does not own a global undo/redo stack, so callers can adapt this to their editor history.</summary>
public interface IColorEditTransaction
{
    void Begin(ColorValue initialValue);
    void Preview(ColorValue value);
    void Commit(ColorValue finalValue);
    void Cancel();
}

public sealed class NoOpColorEditTransaction : IColorEditTransaction
{
    public static NoOpColorEditTransaction Instance { get; } = new();

    private NoOpColorEditTransaction()
    {
    }

    public void Begin(ColorValue initialValue)
    {
    }

    public void Preview(ColorValue value)
    {
    }

    public void Commit(ColorValue finalValue)
    {
    }

    public void Cancel()
    {
    }
}