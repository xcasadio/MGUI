using System.Reflection;
using System.Text.RegularExpressions;
using System.IO;
using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

public class HostRuntimeContractTests
{
    [Fact]
    public void MainRenderer_Constructor_RequiresExplicitRenderHost()
    {
        ConstructorInfo? constructor = typeof(MainRenderer).GetConstructor(new[]
        {
            typeof(IRenderHost),
            typeof(IRawInputSource),
            typeof(IUISurface),
            typeof(IUIAssetProvider)
        });

        Assert.NotNull(constructor);

        ParameterInfo[] parameters = constructor!.GetParameters();
        Assert.Equal(typeof(IRenderHost), parameters[0].ParameterType);
        Assert.Equal(typeof(IRawInputSource), parameters[1].ParameterType);
        Assert.Equal(typeof(IUISurface), parameters[2].ParameterType);
        Assert.Equal(typeof(IUIAssetProvider), parameters[3].ParameterType);
        Assert.All(parameters.Skip(1), parameter => Assert.True(parameter.IsOptional));
    }

    [Fact]
    public void GameRenderHost_RemainsSupportedMonoGameHostPath()
    {
        Type hostType = typeof(GameRenderHost<>);
        Type genericParameter = hostType.GetGenericArguments()[0];
        Type[] constraints = genericParameter.GetGenericParameterConstraints();

        Assert.Contains(typeof(IRenderHost), hostType.GetInterfaces());
        Assert.Contains(typeof(IDisposable), hostType.GetInterfaces());
        Assert.Contains(typeof(Game), constraints);
        Assert.Contains(typeof(IObservableUpdate), constraints);
    }

    [Fact]
    public void MGDesktop_CurrentlyDependsOnConcreteMainRendererAtConstructorBoundary()
    {
        ConstructorInfo? constructor = typeof(MGDesktop).GetConstructor(new[] { typeof(MainRenderer) });

        Assert.NotNull(constructor);
        Assert.Equal(typeof(MainRenderer), typeof(MGDesktop).GetProperty(nameof(MGDesktop.Renderer))!.PropertyType);
    }

    [Fact]
    public void MGDesktop_Source_UsesBoundedSetOfMainRendererMembers()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGDesktop.cs");
        HashSet<string> members = Regex.Matches(source, @"Renderer\.([A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] expectedMembers =
        {
            "AssetProvider",
            "FontManager",
            "Input",
            "RegisterView",
            "Surface",
            "TextEngine",
            "TextEngineChanged",
            "UpdateArgs"
        };

        Assert.Equal(expectedMembers.OrderBy(x => x), members.OrderBy(x => x));
    }

    [Fact]
    public void Samples_KeepExplicitMainRendererBootstrapPath()
    {
        string sampleSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Samples\Game1.cs");
        string miniGameSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.MiniGame\MiniGame.cs");

        Assert.Contains("new(new GameRenderHost<Game1>(this), new MonoGameRawInputSource())", sampleSource);
        Assert.Contains("new MainRenderer(new GameRenderHost<MiniGame>(this), new MonoGameRawInputSource())", miniGameSource);
    }

    [Fact]
    public void MainRenderer_ImplementsSmallDesktopRuntimeContract()
    {
        Type contractType = typeof(IUIDesktopRuntime);
        string[] propertyNames = contractType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();
        string[] methodNames = contractType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => !x.IsSpecialName)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();
        string[] eventNames = contractType
            .GetEvents(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();

        Assert.Contains(contractType, typeof(MainRenderer).GetInterfaces());
        Assert.Equal(new[]
        {
            "AssetProvider",
            "FontManager",
            "Input",
            "Surface",
            "TextEngine",
            "UpdateArgs"
        }, propertyNames);
        Assert.Equal(new[] { "RegisterView" }, methodNames);
        Assert.Equal(new[] { "TextEngineChanged" }, eventNames);
        Assert.DoesNotContain(nameof(MainRenderer.Host), propertyNames);
        Assert.DoesNotContain(nameof(MainRenderer.GraphicsDevice), propertyNames);
        Assert.DoesNotContain(nameof(MainRenderer.GetViewport), methodNames);
    }

    [Fact]
    public void DelegateRenderHost_ProvidesAlternativeMonoGameHostPath()
    {
        ConstructorInfo? constructor = typeof(DelegateRenderHost).GetConstructor(new[]
        {
            typeof(Game),
            typeof(Func<Rectangle>),
            typeof(IServiceProvider)
        });

        Assert.Null(constructor);
        Assert.Contains(typeof(IRenderHost), typeof(DelegateRenderHost).GetInterfaces());
        Assert.DoesNotContain(typeof(IRawInputSource), typeof(DelegateRenderHost).GetInterfaces());

        ConstructorInfo? expectedConstructor = typeof(DelegateRenderHost).GetConstructor(new[]
        {
            typeof(Microsoft.Xna.Framework.Graphics.GraphicsDevice),
            typeof(Func<Rectangle>),
            typeof(IServiceProvider)
        });

        Assert.NotNull(expectedConstructor);
        Assert.NotNull(typeof(DelegateRenderHost).GetMethod(nameof(DelegateRenderHost.NotifyPreviewUpdate), BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(typeof(DelegateRenderHost).GetMethod(nameof(DelegateRenderHost.NotifyEndUpdate), BindingFlags.Instance | BindingFlags.Public));
    }
}