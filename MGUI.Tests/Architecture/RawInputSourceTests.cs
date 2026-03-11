using System.Reflection;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class RawInputSourceTests
{
    [Fact]
    public void IRenderHost_NoLongerExposesRawInputMethods()
    {
        string[] methodNames = typeof(IRenderHost)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(nameof(IRawInputSource.GetMouseState), methodNames);
        Assert.DoesNotContain(nameof(IRawInputSource.GetKeyboardState), methodNames);
    }

    [Fact]
    public void GameRenderHost_RemainsRenderHostOnly()
    {
        Type openGenericHostType = typeof(GameRenderHost<>);

        Assert.Contains(typeof(IRenderHost), openGenericHostType.GetInterfaces());
        Assert.DoesNotContain(typeof(IRawInputSource), openGenericHostType.GetInterfaces());
    }

    [Fact]
    public void MonoGameRawInputSource_ImplementsRawInputSource()
    {
        Assert.Contains(typeof(IRawInputSource), typeof(MonoGameRawInputSource).GetInterfaces());
    }
}