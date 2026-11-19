using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Reflection;

namespace MGUI.Tests.Architecture;

/// <summary>
/// The ordered recording transaction is the proof of "same draw calls in the same order" of the allocation-free desktop frame: these tests check
/// that it records every member of <see cref="IUIDrawTransaction"/>, in call order, and that a journal comparison notices a missing or swapped call.
/// </summary>
public class OrderedRecordingDrawTransactionTests
{
    private static readonly Rectangle Surface = new(0, 0, 640, 360);

    /// <summary>The methods of <see cref="IUIDrawTransaction"/> and of every interface it extends, without the property accessors.</summary>
    private static List<MethodInfo> InterfaceMethods()
    {
        List<Type> interfaces = new() { typeof(IUIDrawTransaction) };
        interfaces.AddRange(typeof(IUIDrawTransaction).GetInterfaces());
        return interfaces
            .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(x => !x.IsSpecialName)
            .ToList();
    }

    private static void CallEveryMember(OrderedRecordingDrawTransaction t)
    {
        IUIImageResource texture = new GraphTestImageResource("tex", 16, 16);
        ResolvedFont font = new GraphTestTextEngine("TestSans").ResolveFont(FontSpec.Normal("TestSans", 12));
        Vector2 v = new(1, 2);
        Color c = Color.Red;
        RectangleF rf = new(1, 2, 3, 4);
        List<Vector2> vertices = new() { v, v * 2, v * 3 };
        List<int> indices = new() { 0, 1, 2 };

        t.DrawTextureTo(texture, null, new Rectangle(0, 0, 4, 4), c);
        t.DrawTextureTo(texture, new Rectangle(0, 0, 2, 2), new Rectangle(0, 0, 4, 4), c, v, 0.5f, 0.25f, UIDrawFlip.Horizontal);
        t.DrawTextureAt(texture, null, v, c, v, 0.5f, 2f, 3f, 0.25f, UIDrawFlip.Vertical);
        t.DrawTextViaEngine(font, "text", v, c, v, 1f);
        t.DrawTextViaEngine(font, "span".AsSpan(), v, c, v, 1f);
        t.FillRectangle(v, rf, c);
        t.FillPoint(v, c, 3f);
        t.StrokeRectangle(v, rf, c, new Thickness(1, 2, 3, 4));
        t.StrokeAndFillRectangle(v, rf, c, Color.Blue, new Thickness(2));
        t.FillPolygon(v, vertices, c);
        t.StrokeAndFillPolygon(v, vertices, c, Color.Blue);
        t.FillTriangle(v, v, c, v * 2, c, v * 3, c);
        t.DrawTexturedTriangleList(v, texture, vertices, vertices, indices, c);
        t.FillQuadrilateralLinearClamp(v, v, c, v, c, v, c, v, c);
        t.StrokeLineSegment(v, v, v * 2, c);
        t.FillCircle(v, c, 3f);
        t.StrokeCircle(v, c, 3f);
        t.StrokeAndFillCircle(v, c, Color.Blue, 3f);
        t.SetDrawSettings(DrawSettings.Default);
        t.SetDrawSettingsTemporary(DrawSettings.Default).Dispose();
        t.SetRenderTargetTemporary(new GraphTestRenderTarget(8, 8), Color.Black).Dispose();
        t.SetTransformTemporary(Matrix.CreateTranslation(1, 2, 0)).Dispose();
        t.ResolveClip(ClipDefinition.Rectangle(new Rectangle(0, 0, 5, 5)));
        t.PushClipTemporary(ClipDefinition.Rectangle(new Rectangle(0, 0, 5, 5))).Dispose();
        t.PushRectangleClip(new Rectangle(0, 0, 6, 6), true).Dispose();
        t.SetClipTargetTemporary(null, false).Dispose();
        t.Dispose();
    }

    [Fact]
    public void EveryMemberOfTheTransactionInterfaceWritesALine()
    {
        OrderedRecordingDrawTransaction transaction = new(new GraphTestRuntime(Surface), DrawSettings.Default);
        CallEveryMember(transaction);

        List<MethodInfo> methods = InterfaceMethods();
        Assert.True(methods.Count >= 25, "The interface lost methods or the reflection missed some: " + methods.Count);

        HashSet<string> written = transaction.Lines.Select(x => x.Split(' ')[0]).ToHashSet();
        List<string> labels = methods.Select(OrderedRecordingDrawTransaction.Label).ToList();
        Assert.Equal(labels.Count, labels.Distinct().Count());
        foreach (string label in labels)
        {
            Assert.True(written.Contains(label), $"No line starts with {label}; lines: {string.Join(" | ", written)}");
        }
    }

    [Fact]
    public void EveryMemberOfTheTransactionInterfaceIsImplementedByTheRecorderItself()
    {
        //  A member left to a default interface implementation (the span overload of DrawTextViaEngine has one) would write no line of its own.
        foreach (Type contract in new[] { typeof(IUIDrawTransaction) }.Concat(typeof(IUIDrawTransaction).GetInterfaces()))
        {
            InterfaceMapping map = typeof(OrderedRecordingDrawTransaction).GetInterfaceMap(contract);
            for (int i = 0; i < map.TargetMethods.Length; i++)
            {
                Assert.True(map.TargetMethods[i].DeclaringType == typeof(OrderedRecordingDrawTransaction),
                    $"{contract.Name}.{map.InterfaceMethods[i].Name} is not implemented by the recorder");
            }
        }
    }

    [Fact]
    public void ClipAndTemporarySettingsScopesWriteTheirDisposalInOrder()
    {
        OrderedRecordingDrawTransaction transaction = new(new GraphTestRuntime(Surface), DrawSettings.Default);
        using (transaction.PushRectangleClip(new Rectangle(0, 0, 100, 50), true))
        {
            using (transaction.PushRectangleClip(new Rectangle(10, 10, 20, 20), true))
            {
                Assert.Equal(new Rectangle(10, 10, 20, 20), transaction.CurrentClipBounds);
            }

            Assert.Equal(new Rectangle(0, 0, 100, 50), transaction.CurrentClipBounds);
        }

        Assert.Null(transaction.CurrentClipBounds);
        string[] labels = transaction.Lines.Select(x => x.Split(' ')[0]).ToArray();
        Assert.Equal(new[] { "PushRectangleClip/2", "PushRectangleClip/2", "DisposeClip", "DisposeClip" }, labels);
        Assert.Contains("scope=1", transaction.Lines[2]);
        Assert.Contains("scope=0", transaction.Lines[3]);
    }

    [Fact]
    public void TwoIdleFramesOfTheFullFixtureWriteTheSameJournal()
    {
        DesktopFrameAllocationMeasurements.Harness harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.Full);
        OrderedRecordingDrawTransaction first = FrameJournal(harness);
        OrderedRecordingDrawTransaction second = FrameJournal(harness);

        Assert.True(first.Lines.Count > 20, "The idle frame of the Full fixture drew almost nothing: " + first.Lines.Count);
        Assert.Equal(first.Journal, second.Journal);
    }

    [Fact]
    public void AMissingOrSwappedCallMakesTheJournalComparisonFail()
    {
        DesktopFrameAllocationMeasurements.Harness harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.Full);
        List<string> journal = FrameJournal(harness).Lines.ToList();
        string hash = FrozenJournal.Hash(string.Join('\n', journal));
        FrozenJournal.Assert("unchanged", journal.Count, hash, journal);

        foreach (string label in new[] { "FillRectangle/3", "StrokeLineSegment/5", "DrawTextViaEngine/9s", "FillPoint/3" })
        {
            int index = journal.FindIndex(x => x.StartsWith(label + " "));
            Assert.True(index >= 0, $"The Full fixture journal has no {label} call: " + string.Join('\n', journal));
            List<string> missing = new(journal);
            missing.RemoveAt(index);
            Assert.ThrowsAny<Exception>(() => FrozenJournal.Assert(label + " removed", journal.Count, hash, missing));
        }

        //  Swap two neighbouring lines of different kinds: same lines, same count, different order.
        int swapAt = Enumerable.Range(0, journal.Count - 1).First(i => journal[i].Split(' ')[0] != journal[i + 1].Split(' ')[0]);
        List<string> swapped = new(journal);
        (swapped[swapAt], swapped[swapAt + 1]) = (swapped[swapAt + 1], swapped[swapAt]);
        Assert.Equal(journal.Count, swapped.Count);
        Assert.ThrowsAny<Exception>(() => FrozenJournal.Assert("swapped", journal.Count, hash, swapped));
    }

    /// <summary>One more idle frame (mouse outside the window), drawn into a fresh recording transaction.</summary>
    private static OrderedRecordingDrawTransaction FrameJournal(DesktopFrameAllocationMeasurements.Harness harness)
    {
        harness.Update(new Point(600, 340), ButtonState.Released);
        OrderedRecordingDrawTransaction transaction = new(harness.GetRuntime(), DrawSettings.Default);
        harness.Draw(transaction);
        return transaction;
    }
}
