namespace MGUI.Core.UI
{
    public interface IColorEditTransaction
    {
        void Begin(ColorValue initialValue);
        void Preview(ColorValue value);
        void Commit(ColorValue finalValue);
        void Cancel();
    }
}