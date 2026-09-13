using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>Minimal, non-visible <see cref="Game"/> used only to obtain a real, GPU-backed <see cref="GraphicsDevice"/>.<para/>
/// Constructing a <see cref="GraphicsDeviceManager"/> and calling <see cref="Game.RunOneFrame"/> is enough to create the
/// device (<c>Game.DoInitialize</c> calls <c>IGraphicsDeviceManager.CreateDevice()</c> before <c>Initialize()</c> runs) without
/// ever showing the window: MonoGame's SDL platform only calls <c>Sdl.Window.Show</c> from <c>RunLoop()</c>, which backs
/// <see cref="Game.Run()"/>, not <see cref="Game.RunOneFrame"/>. The window itself is still created (hidden) by the
/// <c>SdlGamePlatform</c>/<c>SdlGameWindow</c> constructors, which is unavoidable - MonoGame's device creation is wired
/// through the platform's window handle.</summary>
internal sealed class HeadlessGame : Game, IObservableUpdate
{
    public GraphicsDeviceManager Gdm { get; }

    // IObservableUpdate: required by GameRenderHost<T>/MainRenderer, but never raised - these GPU tests draw directly
    // through DrawTransaction and don't need MainRenderer's input/update pipeline.
    public event EventHandler<TimeSpan> PreviewUpdate;
    public event EventHandler<EventArgs> EndUpdate;

    public HeadlessGame()
    {
        Gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 128,
            PreferredBackBufferHeight = 128,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            SynchronizeWithVerticalRetrace = false,
        };
        Content.RootDirectory = "Content";
        IsFixedTimeStep = false;
    }
}

/// <summary>Owns the single dedicated thread that every real-GPU regression test in <see cref="MGUI.Tests"/> must use.<para/>
/// MonoGame's internal <c>Microsoft.Xna.Framework.Threading</c> helper records
/// whichever thread first touches it - lazily, in a static constructor - as "the UI thread", and
/// <c>GraphicsDevice.PlatformBeginApplyState</c> (invoked on every draw call) calls <c>Threading.EnsureUIThread()</c>, throwing
/// on any other thread. There is no pump running <c>Threading.Run()</c> in this test host (that only happens inside
/// <c>SdlGamePlatform.RunLoop</c>, which we never enter - see <see cref="HeadlessGame"/>), so <c>Threading.BlockOnUIThread</c>
/// would simply deadlock if some GPU work ever ran on a different thread than the one that first touched MonoGame.<para/>
/// The fix is architectural rather than defensive: dedicate one long-lived background thread to ALL MonoGame work (device
/// creation, drawing, GetData, disposal) and never let any other thread touch a MonoGame type, so this thread is
/// guaranteed to be the one MonoGame recorded as its UI thread for the lifetime of the test process - regardless of which
/// thread xunit happens to run a given test on.</summary>
internal sealed class GpuDeviceHost
{
    public static readonly GpuDeviceHost Instance = new();

    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly Lazy<(bool Available, string Reason)> _probe;

    private HeadlessGame _game;

    private GpuDeviceHost()
    {
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "MGUI-GPU-Test-Thread" };
        _thread.Start();
        _probe = new Lazy<(bool, string)>(Probe, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>True if a real <see cref="GraphicsDevice"/> could be created in this process. Probed exactly once, lazily,
    /// the first time this - or <see cref="UnavailableReason"/> - is read (typically from a <see cref="GpuFactAttribute"/>
    /// during test discovery).</summary>
    public bool IsAvailable => _probe.Value.Available;

    /// <summary>Null when <see cref="IsAvailable"/>; otherwise a human-readable explanation (including the causing
    /// exception) suitable for use as an xunit <c>Skip</c> reason.</summary>
    public string UnavailableReason => _probe.Value.Reason;

    /// <summary>The real <see cref="GraphicsDevice"/> backing every GPU test in this process. Only valid to use from
    /// inside an <see cref="Invoke"/> callback (i.e. on <see cref="_thread"/>).</summary>
    public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;

    /// <summary>The <see cref="HeadlessGame"/> backing <see cref="GraphicsDevice"/>. Only valid to use from inside an
    /// <see cref="Invoke"/> callback (i.e. on <see cref="_thread"/>).</summary>
    public HeadlessGame Game => _game;

    private (bool, string) Probe()
    {
        try
        {
            Invoke(() =>
            {
                _game = new HeadlessGame();
                _game.RunOneFrame();
                if (_game.GraphicsDevice == null)
                {
                    throw new InvalidOperationException($"{nameof(HeadlessGame)}.{nameof(HeadlessGame.GraphicsDevice)} was still null after {nameof(Game.RunOneFrame)}().");
                }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No real GraphicsDevice could be created in this process (no GPU/display?): {ex}");
        }
    }

    /// <summary>Runs <paramref name="action"/> on the dedicated GPU thread and blocks the caller until it completes,
    /// re-throwing any exception on the calling thread. Safe to call from any thread except the dedicated one itself.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        ExceptionDispatchInfo capturedError = null;
        using ManualResetEventSlim done = new(false);

        _work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedError = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();
        capturedError?.Throw();
    }

    /// <summary>Runs <paramref name="func"/> on the dedicated GPU thread and returns its result on the caller.</summary>
    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        T result = default;
        Invoke(() => { result = func(); });
        return result;
    }

    private void RunLoop()
    {
        foreach (Action action in _work.GetConsumingEnumerable())
        {
            action();
        }
    }
}

/// <summary>Like <see cref="FactAttribute"/>, but automatically skips (rather than fails or errors) when no real
/// <see cref="GraphicsDevice"/> can be created in this process - e.g. a headless CI agent with no GPU/display.</summary>
public sealed class GpuFactAttribute : FactAttribute
{
    public GpuFactAttribute()
    {
        if (!GpuDeviceHost.Instance.IsAvailable)
        {
            Skip = GpuDeviceHost.Instance.UnavailableReason;
        }
    }
}

/// <summary>All real-GPU tests share one <see cref="GraphicsDevice"/> and one dedicated thread (<see cref="GpuDeviceHost"/>),
/// so they must not run concurrently with each other (xunit runs collections concurrently by default).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GpuDeviceCollection
{
    public const string Name = "MGUI real-GPU rendering";
}
