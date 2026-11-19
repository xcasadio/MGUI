using MGUI.Core.UI.Text;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

/// <summary>
/// <see cref="TextBoundsTracker"/> against what it replaces: a dictionary of lists, cleared at the start of each draw, that a draw fills with a new list the first time it meets a name. After any
/// sequence of adds and clears the same names are enumerated in the same order with the same rectangles, and <see cref="TextBoundsTracker.Any"/> says what <c>Any()</c> said.
/// </summary>
public class TextBoundsTrackerTests
{
    private static void AssertSame(Dictionary<string, List<Rectangle>> expected, TextBoundsTracker actual, string context)
    {
        Assert.True((expected.Count > 0) == actual.Any, $"{context}: Any");
        Assert.True(expected.Count == actual.NameCount, $"{context}: {expected.Count} names, got {actual.NameCount}");
        int index = 0;
        foreach (KeyValuePair<string, List<Rectangle>> entry in expected)
        {
            Assert.True(entry.Key == actual.GetName(index), $"{context}: name #{index} is {actual.GetName(index)}, expected {entry.Key}");
            Assert.True(entry.Value.SequenceEqual(actual.GetBounds(index)), $"{context}: rectangles of {entry.Key}");
            index++;
        }
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(10, 2)]
    [InlineData(70, 3)]
    [InlineData(300, 4)]
    public void AnySequenceOfAddsAndClears_GivesWhatTheDictionaryOfListsGave(int distinctNames, int seed)
    {
        Random random = new(seed);
        string[] names = Enumerable.Range(0, distinctNames).Select(x => $"name{x}").ToArray();
        TextBoundsTracker actual = new();
        Dictionary<string, List<Rectangle>> expected = new();
        int clears = 0;
        int maxNames = 0;

        for (int step = 0; step < 4000; step++)
        {
            if (random.Next(12) == 0)
            {
                expected.Clear();
                actual.Clear();
                clears++;
            }
            else
            {
                //  A draw meets a few names, some of them several times (a text that wraps)
                string name = names[random.Next(names.Length)];
                Rectangle bounds = new(random.Next(100), random.Next(100), random.Next(1, 50), random.Next(1, 20));
                if (!expected.TryGetValue(name, out List<Rectangle> list))
                {
                    list = new();
                    expected.Add(name, list);
                }

                list.Add(bounds);
                actual.Add(name, bounds);
            }

            AssertSame(expected, actual, $"names={distinctNames} seed={seed} step={step}");
            maxNames = Math.Max(maxNames, expected.Count);
        }

        Assert.True(clears > 100);
        Assert.True(maxNames >= Math.Min(distinctNames, 3));
    }

    [Fact]
    public void AfterAClear_TheNamesAreEnumeratedInTheOrderTheyAreAddedAgain()
    {
        TextBoundsTracker tracker = new();
        tracker.Add("first", new Rectangle(0, 0, 1, 1));
        tracker.Add("second", new Rectangle(0, 0, 2, 2));
        tracker.Clear();
        Assert.False(tracker.Any);

        tracker.Add("second", new Rectangle(0, 0, 3, 3));
        tracker.Add("first", new Rectangle(0, 0, 4, 4));

        Assert.Equal("second", tracker.GetName(0));
        Assert.Equal("first", tracker.GetName(1));
        Assert.Equal(new[] { new Rectangle(0, 0, 3, 3) }, tracker.GetBounds(0));
        Assert.Equal(new[] { new Rectangle(0, 0, 4, 4) }, tracker.GetBounds(1));
    }

    [Fact]
    public void AClearedTracker_KeepsTheListsOfItsNames_ThenAddsAndClearsAllocateNothing()
    {
        TextBoundsTracker tracker = new();
        string[] names = { "a", "b", "c" };
        for (int i = 0; i < 100; i++)
        {
            foreach (string name in names)
            {
                tracker.Add(name, new Rectangle(i, i, 10, 10));
                tracker.Add(name, new Rectangle(i, i + 10, 10, 10));
            }

            tracker.Clear();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            foreach (string name in names)
            {
                tracker.Add(name, new Rectangle(i, i, 10, 10));
                tracker.Add(name, new Rectangle(i, i + 10, 10, 10));
            }

            tracker.Clear();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ATrackerThatSeesManyNames_DoesNotKeepThemAll()
    {
        //  Names that are not in use are forgotten once the pool is full, so a text block that keeps showing new names does not grow without bound.
        TextBoundsTracker tracker = new();
        for (int i = 0; i < 5000; i++)
        {
            tracker.Add($"unique{i}", new Rectangle(0, 0, 1, 1));
            tracker.Clear();
        }

        tracker.Add("last", new Rectangle(0, 0, 2, 2));
        Assert.Equal(1, tracker.NameCount);
        Assert.Equal("last", tracker.GetName(0));
    }
}
