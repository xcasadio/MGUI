namespace MGUI.Core.UI.DragDrop;

/// <summary>Wraps the data payload and allowed effects for a drag-and-drop operation.</summary>
public class DragDropData
{
    /// <summary>The drag payload. Can be any object.</summary>
    public object Data { get; }

    /// <summary>The effects the source allows on its data (bitwise combination).</summary>
    public DragDropEffect AllowedEffects { get; }

    /// <summary>The effect negotiated between source and drop target. Set by the drop target during <c>DragOver</c> or <c>Drop</c>.</summary>
    public DragDropEffect DropEffect { get; set; } = DragDropEffect.None;

    public DragDropData(object data, DragDropEffect allowedEffects = DragDropEffect.Copy | DragDropEffect.Move)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        AllowedEffects = allowedEffects;
    }

    /// <summary>Returns the data as <typeparamref name="T"/>, or default if the payload is not of that type.</summary>
    public T GetData<T>() => Data is T t ? t : default;
}