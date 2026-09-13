namespace MGUI.Core.UI.TextEditing;

internal sealed class MGTextUndoStack<T>
{
    private readonly List<T> _items;

    public int Count => _items.Count;
    public int Limit { get; private set; }

    public MGTextUndoStack(int limit)
    {
        ValidateLimit(limit);
        Limit = limit;
        _items = new List<T>(limit);
    }

    public void SetLimit(int limit)
    {
        ValidateLimit(limit);
        if (Limit == limit)
        {
            return;
        }

        Limit = limit;
        TrimToLimit();
    }

    public void Clear() => _items.Clear();

    public T Peek() => _items[^1];

    public void Push(T item)
    {
        if (_items.Count == Limit)
        {
            _items.RemoveAt(0);
        }

        _items.Add(item);
    }

    public T Pop()
    {
        T item = Peek();
        _items.RemoveAt(_items.Count - 1);
        return item;
    }

    public bool TryPop(out T item)
    {
        if (_items.Count == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    public override string ToString() => $"{nameof(MGTextUndoStack<T>)}: {Count} / {Limit}";

    private static void ValidateLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"{nameof(limit)} must be greater than zero.");
        }
    }

    private void TrimToLimit()
    {
        int overflow = _items.Count - Limit;
        if (overflow > 0)
        {
            _items.RemoveRange(0, overflow);
        }
    }
}