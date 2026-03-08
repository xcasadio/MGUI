using MGUI.Core.UI.DragDrop;
using Microsoft.Xna.Framework;
using System;

namespace MGUI.Tests.DragDrop;

/// <summary>Unit tests for the drag-and-drop base types (no MonoGame runtime required).</summary>
public class DragDropTests
{
    // ── DragDropEffect ────────────────────────────────────────────────────

    [Fact]
    public void DragDropEffect_None_IsZero()
    {
        Assert.Equal(0, (int)DragDropEffect.None);
    }

    [Fact]
    public void DragDropEffect_Flags_CanCombine()
    {
        DragDropEffect combined = DragDropEffect.Copy | DragDropEffect.Move;
        Assert.True(combined.HasFlag(DragDropEffect.Copy));
        Assert.True(combined.HasFlag(DragDropEffect.Move));
        Assert.False(combined.HasFlag(DragDropEffect.Link));
    }

    [Fact]
    public void DragDropEffect_All_ContainsAllFlags()
    {
        Assert.True(DragDropEffect.All.HasFlag(DragDropEffect.Copy));
        Assert.True(DragDropEffect.All.HasFlag(DragDropEffect.Move));
        Assert.True(DragDropEffect.All.HasFlag(DragDropEffect.Link));
    }

    // ── DragDropData ──────────────────────────────────────────────────────

    [Fact]
    public void DragDropData_StoredData_MatchesConstructorArg()
    {
        var payload = new object();
        var data = new DragDropData(payload);
        Assert.Same(payload, data.Data);
    }

    [Fact]
    public void DragDropData_DefaultAllowedEffects_IsCopyAndMove()
    {
        var data = new DragDropData("hello");
        Assert.True(data.AllowedEffects.HasFlag(DragDropEffect.Copy));
        Assert.True(data.AllowedEffects.HasFlag(DragDropEffect.Move));
    }

    [Fact]
    public void DragDropData_DefaultDropEffect_IsNone()
    {
        var data = new DragDropData("hello");
        Assert.Equal(DragDropEffect.None, data.DropEffect);
    }

    [Fact]
    public void DragDropData_DropEffect_CanBeSet()
    {
        var data = new DragDropData("hello") { DropEffect = DragDropEffect.Copy };
        Assert.Equal(DragDropEffect.Copy, data.DropEffect);
    }

    [Fact]
    public void DragDropData_GetData_ReturnsTypedPayload()
    {
        var payload = "test-string";
        var data = new DragDropData(payload);
        Assert.Equal(payload, data.GetData<string>());
    }

    [Fact]
    public void DragDropData_GetData_ReturnsDefault_WhenWrongType()
    {
        var data = new DragDropData(42);
        Assert.Null(data.GetData<string>());
    }

    [Fact]
    public void DragDropData_ThrowsOnNullPayload()
    {
        Assert.Throws<ArgumentNullException>(() => new DragDropData(null!));
    }

    // ── DragDropEventArgs ─────────────────────────────────────────────────

    [Fact]
    public void DragEnterEventArgs_StoresDataAndPosition()
    {
        var data = new DragDropData("payload");
        var pos  = new Point(10, 20);
        var args = new DragEnterEventArgs(data, null, pos);
        Assert.Same(data, args.Data);
        Assert.Equal(pos, args.Position);
        Assert.Null(args.Source);
    }

    [Fact]
    public void DropEventArgs_ThrowsOnNullData()
    {
        Assert.Throws<ArgumentNullException>(() => new DropEventArgs(null!, null, Point.Zero));
    }
}
