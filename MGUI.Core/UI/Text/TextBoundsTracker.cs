using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Text;

/// <summary>
/// The bounds a draw of an <see cref="MGTextBlock"/> keeps for the parts of its text that have a tooltip or an action: for each name, the rectangles of the pieces of text (and of the images)
/// that carry it, usually one rectangle, several when the text wraps. It is what a <c>Dictionary&lt;string, List&lt;Rectangle&gt;&gt;</c> that was cleared at the start of each draw gave, without
/// creating a list for each name at each draw: the list of a name is kept when the tracker is cleared, and given back (emptied) the next time that name is seen.<para/>
/// The names are enumerated in the order they were first added since the last <see cref="Clear"/>, like the dictionary enumerated them, so when two parts that overlap both
/// carry a name, the same one wins.
/// </summary>
internal sealed class TextBoundsTracker
{
    /// <summary>A tracker that keeps more names than this forgets the ones that are not in use before it takes a new one, so a text block that keeps showing new names does not grow without bound.</summary>
    internal const int PooledNameLimit = 64;

    private readonly Dictionary<string, List<Rectangle>> Pool = new();

    /// <summary>The names added since the last <see cref="Clear"/>, in the order they were first added. Their lists in <see cref="Pool"/> are not empty.</summary>
    private readonly List<string> Names = new();

    /// <summary>True if something was added since the last <see cref="Clear"/>.</summary>
    public bool Any => Names.Count > 0;

    public int NameCount => Names.Count;

    public string GetName(int Index) => Names[Index];

    public List<Rectangle> GetBounds(int Index) => Pool[Names[Index]];

    public void Clear()
    {
        for (var i = 0; i < Names.Count; i++)
        {
            Pool[Names[i]].Clear();
        }

        Names.Clear();
    }

    public void Add(string Name, Rectangle Bounds)
    {
        if (!Pool.TryGetValue(Name, out var List))
        {
            if (Pool.Count >= PooledNameLimit)
            {
                ForgetUnusedNames();
            }

            List = new List<Rectangle>();
            Pool.Add(Name, List);
        }

        if (List.Count == 0)
        {
            Names.Add(Name);
        }

        List.Add(Bounds);
    }

    private void ForgetUnusedNames()
    {
        //  Only the names that are not in use have an empty list; removing them while enumerating is not allowed, so collect them first (this only happens when the limit is reached).
        List<string> Unused = new();
        foreach (var Entry in Pool)
        {
            if (Entry.Value.Count == 0)
            {
                Unused.Add(Entry.Key);
            }
        }

        foreach (var Name in Unused)
        {
            Pool.Remove(Name);
        }
    }
}
