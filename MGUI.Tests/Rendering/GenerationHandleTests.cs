using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Rendering;

/// <summary>The handles of a temporary draw state change and of a clip designate one use of a slot of their backend (ADR-0031): only the first
/// dispose of a use restores, a handle of a use that is over does nothing even after its slot was reused, the default handle does nothing, and
/// nothing is allocated.</summary>
public class GenerationHandleTests
{
    [Fact]
    public void Slots_GiveBackTheStateOfAUse_Once()
    {
        GenerationSlots<string> slots = new();

        int slot = slots.Acquire("first", out int generation);

        Assert.NotEqual(0, generation);
        Assert.True(slots.IsLive(slot, generation));
        Assert.Equal(1, slots.InUseCount);
        Assert.True(slots.TryGet(slot, generation, out string read));
        Assert.Equal("first", read);

        Assert.True(slots.TryRelease(slot, generation, out string released));
        Assert.Equal("first", released);
        Assert.Equal(0, slots.InUseCount);

        Assert.False(slots.IsLive(slot, generation));
        Assert.False(slots.TryGet(slot, generation, out _));
        Assert.False(slots.TrySet(slot, generation, "changed"));
        Assert.False(slots.TryRelease(slot, generation, out string again));
        Assert.Null(again);
        Assert.Equal(0, slots.InUseCount);
    }

    [Fact]
    public void Slots_AStalePair_DoesNotDesignateTheNextUseOfItsSlot()
    {
        GenerationSlots<string> slots = new();
        int slot = slots.Acquire("first", out int staleGeneration);
        Assert.True(slots.TryRelease(slot, staleGeneration, out _));

        int reused = slots.Acquire("second", out int generation);

        Assert.Equal(slot, reused);
        Assert.NotEqual(staleGeneration, generation);
        Assert.False(slots.TryRelease(slot, staleGeneration, out _));
        Assert.False(slots.TrySet(slot, staleGeneration, "changed"));
        Assert.True(slots.TryGet(reused, generation, out string state));
        Assert.Equal("second", state);
        Assert.Equal(1, slots.InUseCount);
    }

    [Fact]
    public void Slots_AreReleasedInAnyOrder_AndGrow()
    {
        GenerationSlots<int> slots = new(InitialCapacity: 2);
        int[] slotOf = new int[20];
        int[] generationOf = new int[20];
        for (int i = 0; i < 20; i++)
        {
            slotOf[i] = slots.Acquire(i * 10, out generationOf[i]);
        }

        Assert.Equal(20, slots.InUseCount);
        Assert.Equal(20, slotOf.Distinct().Count());

        for (int i = 0; i < 20; i += 2)
        {
            Assert.True(slots.TryRelease(slotOf[i], generationOf[i], out int state));
            Assert.Equal(i * 10, state);
        }

        for (int i = 1; i < 20; i += 2)
        {
            Assert.True(slots.TryGet(slotOf[i], generationOf[i], out int state));
            Assert.Equal(i * 10, state);
        }

        Assert.True(slots.TrySet(slotOf[1], generationOf[1], 7));
        Assert.True(slots.TryRelease(slotOf[1], generationOf[1], out int changed));
        Assert.Equal(7, changed);
        Assert.Equal(9, slots.InUseCount);
    }

    [Fact]
    public void Slots_ReleaseAll_EndsEveryUse()
    {
        GenerationSlots<string> slots = new();
        int first = slots.Acquire("first", out int firstGeneration);
        int second = slots.Acquire("second", out int secondGeneration);

        slots.ReleaseAll();

        Assert.Equal(0, slots.InUseCount);
        Assert.False(slots.TryRelease(first, firstGeneration, out _));
        Assert.False(slots.TryRelease(second, secondGeneration, out _));

        int reused = slots.Acquire("third", out int generation);
        Assert.False(slots.IsLive(reused, reused == first ? firstGeneration : secondGeneration));
        Assert.True(slots.IsLive(reused, generation));
    }

    [Fact]
    public void Slots_ASlotNumberNeverHandedOut_IsNotLive()
    {
        GenerationSlots<string> slots = new();
        slots.Acquire("first", out _);

        Assert.False(slots.IsLive(-1, 1));
        Assert.False(slots.IsLive(1, 1));
        Assert.False(slots.IsLive(1000, 1));
        Assert.False(slots.IsLive(0, 0));
    }

    [Fact]
    public void Slots_AcquireAndRelease_AllocateNothing_OnceTheSlotsExist()
    {
        GenerationSlots<Matrix> slots = new();
        Matrix state = Matrix.CreateTranslation(1, 2, 3);
        for (int i = 0; i < 4; i++)
        {
            int outer = slots.Acquire(state, out int outerGeneration);
            int inner = slots.Acquire(state, out int innerGeneration);
            slots.TryRelease(inner, innerGeneration, out _);
            slots.TryRelease(outer, outerGeneration, out _);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            int outer = slots.Acquire(state, out int outerGeneration);
            int inner = slots.Acquire(state, out int innerGeneration);
            slots.TryRelease(inner, innerGeneration, out _);
            slots.TryRelease(outer, outerGeneration, out _);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ADrawStateHandle_RestoresOnce_AndAStaleCopyDoesNotReleaseTheNextUse()
    {
        FakeOwner owner = new();
        DrawStateHandle first = owner.SetTemporary("first");
        DrawStateHandle copy = first;

        first.Dispose();
        first.Dispose();
        Assert.Equal(new[] { "first" }, owner.Restored);

        DrawStateHandle second = owner.SetTemporary("second");
        copy.Dispose();
        Assert.Equal(new[] { "first" }, owner.Restored);

        second.Dispose();
        Assert.Equal(new[] { "first", "second" }, owner.Restored);
    }

    [Fact]
    public void ADrawStateHandle_OfAUseEndedByARearm_DoesNothing()
    {
        FakeOwner owner = new();
        DrawStateHandle handle = owner.SetTemporary("first");

        owner.Rearm();
        DrawStateHandle next = owner.SetTemporary("second");
        handle.Dispose();

        Assert.Empty(owner.Restored);
        next.Dispose();
        Assert.Equal(new[] { "second" }, owner.Restored);
    }

    [Fact]
    public void TheDefaultHandles_DesignateNothing()
    {
        DrawStateHandle drawState = default;
        ClipHandle clip = default;

        Assert.True(drawState.IsDefault);
        Assert.True(clip.IsDefault);
        Assert.False(clip.IsActive);
        Assert.False(clip.TryGetResolution(out _));
        Assert.Throws<InvalidOperationException>(() => clip.Resolution);
        drawState.Dispose();
        clip.Dispose();

        FakeOwner owner = new();
        Assert.False(owner.SetTemporary("first").IsDefault);
        Assert.False(owner.PushClip(new Rectangle(1, 2, 3, 4)).IsDefault);
    }

    [Fact]
    public void AHandle_NeedsAnOwner()
    {
        Assert.Throws<ArgumentNullException>(() => new DrawStateHandle(null, 0, 1));
        Assert.Throws<ArgumentNullException>(() => new ClipHandle(null, 0, 1));
    }

    [Fact]
    public void AClipHandle_ReadsItsResolutionWhileItsClipIsInForce()
    {
        FakeOwner owner = new();
        ClipHandle first = owner.PushClip(new Rectangle(1, 2, 3, 4));
        ClipHandle copy = first;

        Assert.True(first.IsActive);
        Assert.Equal(new Rectangle(1, 2, 3, 4), first.Resolution.Effective.Shape.Bounds);
        Assert.True(copy.TryGetResolution(out ClipResolveResult read));
        Assert.Equal(ClipStrategy.Scissor, read.Strategy);

        first.Dispose();
        Assert.Equal(1, owner.ReleasedClips);
        Assert.False(first.IsActive);
        Assert.False(copy.IsActive);
        Assert.False(copy.TryGetResolution(out _));
        Assert.Throws<InvalidOperationException>(() => copy.Resolution);

        ClipHandle second = owner.PushClip(new Rectangle(5, 6, 7, 8));
        copy.Dispose();
        Assert.Equal(1, owner.ReleasedClips);
        Assert.False(copy.IsActive);
        Assert.Equal(new Rectangle(5, 6, 7, 8), second.Resolution.Effective.Shape.Bounds);

        second.Dispose();
        Assert.Equal(2, owner.ReleasedClips);
    }

    [Fact]
    public void Handles_AreMadeAndDisposed_WithoutAllocating()
    {
        FakeOwner owner = new();
        ClipResolveResult resolution = FakeOwner.RectangleResolution(new Rectangle(1, 2, 3, 4));
        for (int i = 0; i < 4; i++)
        {
            UseHandles(owner, resolution);
        }

        owner.Restored.Clear();
        owner.KeepsRestoredStates = false;

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            UseHandles(owner, resolution);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void UseHandles(FakeOwner owner, ClipResolveResult resolution)
    {
        using (owner.SetTemporary("outer"))
        using (ClipHandle clip = owner.PushClip(resolution))
        using (owner.SetTemporary("inner"))
        using (default(DrawStateHandle))
        using (default(ClipHandle))
        {
            Assert.True(clip.IsActive);
        }
    }

    /// <summary>A backend reduced to its slots: a temporary state that records what it restores, and clips that count their releases.</summary>
    private sealed class FakeOwner : IDrawStateHandleOwner, IClipHandleOwner
    {
        private readonly GenerationSlots<string> _States = new();
        private readonly GenerationSlots<ClipResolveResult> _Clips = new();

        public List<string> Restored { get; } = new();
        public bool KeepsRestoredStates { get; set; } = true;
        public int ReleasedClips { get; private set; }

        public static ClipResolveResult RectangleResolution(Rectangle bounds)
        {
            ClipDefinition definition = ClipDefinition.Rectangle(bounds);
            return new ClipResolveResult(definition, definition, ClipStrategy.Scissor, false);
        }

        public DrawStateHandle SetTemporary(string state)
        {
            int slot = _States.Acquire(state, out int generation);
            return new DrawStateHandle(this, slot, generation);
        }

        public ClipHandle PushClip(Rectangle bounds) => PushClip(RectangleResolution(bounds));

        public ClipHandle PushClip(ClipResolveResult resolution)
        {
            int slot = _Clips.Acquire(resolution, out int generation);
            return new ClipHandle(this, slot, generation);
        }

        public void Rearm()
        {
            _States.ReleaseAll();
            _Clips.ReleaseAll();
        }

        public void ReleaseDrawState(int Slot, int Generation)
        {
            if (_States.TryRelease(Slot, Generation, out string state) && KeepsRestoredStates)
            {
                Restored.Add(state);
            }
        }

        public void ReleaseClip(int Slot, int Generation)
        {
            if (_Clips.TryRelease(Slot, Generation, out _))
            {
                ReleasedClips++;
            }
        }

        public bool TryGetClipResolution(int Slot, int Generation, out ClipResolveResult Resolution)
            => _Clips.TryGet(Slot, Generation, out Resolution);
    }
}
