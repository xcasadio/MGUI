using MGUI.Shared.Assets;
using System.Diagnostics;

namespace MGUI.Core.UI;

/// <summary>Plays a host-provided video (ADR-0026). It draws like an <see cref="MGImage"/> (same stretching and filtering) and takes its image from an
/// <see cref="IUIVideo"/> that the host creates for <see cref="MGImage.SourceName"/> through <see cref="IUIAssetProvider.TryCreateVideo(string, out IUIVideo)"/>.<para/>
/// The video advances in <see cref="UpdateSelf(ElementUpdateArgs)"/>, so a collapsed element does not advance it. Reaching the end raises
/// <see cref="Ended"/> once and runs <see cref="EndedCommandName"/>. <see cref="Skip"/> raises <see cref="Skipped"/> once and runs
/// <see cref="SkippedCommandName"/>, only when <see cref="IsSkippable"/>. Reading an input to skip is the host's job.<para/>
/// <see cref="MGImage.Source"/> (it is stored but never replaces the video) and the animation properties of <see cref="MGImage"/> have no effect on a video.
/// The video is disposed when <see cref="MGImage.SourceName"/> changes and by <see cref="Dispose"/>.</summary>
public class MGVideo : MGImage, IDisposable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IUIVideo _Video;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _HasEnded;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _HasBeenSkipped;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsDisposed;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsSkippable;
    /// <summary>Whether <see cref="Skip"/> may end the video. Default value: false</summary>
    public bool IsSkippable
    {
        get => _IsSkippable;
        set
        {
            if (_IsSkippable != value)
            {
                _IsSkippable = value;
                NotifyPropertyChanged(nameof(IsSkippable));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _EndedCommandName;
    /// <summary>The name of the command to execute when the video reaches its end, or null for none. It is looked up in <see cref="MGResources.Commands"/>
    /// at that moment, like <see cref="MGButton.CommandName"/>.</summary>
    public string EndedCommandName
    {
        get => _EndedCommandName;
        set
        {
            if (_EndedCommandName != value)
            {
                _EndedCommandName = value;
                NotifyPropertyChanged(nameof(EndedCommandName));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _SkippedCommandName;
    /// <summary>The name of the command to execute when the video is skipped, or null for none. It is looked up in <see cref="MGResources.Commands"/>
    /// at that moment, like <see cref="MGButton.CommandName"/>.</summary>
    public string SkippedCommandName
    {
        get => _SkippedCommandName;
        set
        {
            if (_SkippedCommandName != value)
            {
                _SkippedCommandName = value;
                NotifyPropertyChanged(nameof(SkippedCommandName));
            }
        }
    }

    /// <summary>Invoked once, when the video reaches its end by playing. Not invoked for a video that is skipped.</summary>
    public event EventHandler<EventArgs> Ended;

    /// <summary>Invoked once, when <see cref="Skip"/> ends the video.</summary>
    public event EventHandler<EventArgs> Skipped;

    /// <summary>True when a video is playing: a source is resolved, and it has neither ended nor been skipped.</summary>
    public bool IsPlaying => _Video != null && !_HasEnded && !_HasBeenSkipped;

    /// <param name="SourceName">The name the host resolves into a video. See <see cref="IUIAssetProvider.TryCreateVideo(string, out IUIVideo)"/></param>
    public MGVideo(MGWindow Window, string SourceName = null, Stretch Stretch = Stretch.Uniform)
        : base(Window, MGElementType.Video)
    {
        using (BeginInitializing())
        {
            this.Stretch = Stretch;
            this.SourceName = SourceName;
        }
    }

    protected override bool ResolvesTextureNames => false;

    protected override bool UsesSource => false;

    protected override void UpdateActualSource()
    {
        ReleaseVideo();

        if (!_IsDisposed && SourceName != null && GetResources().AssetProvider?.TryCreateVideo(SourceName, out var Video) == true)
        {
            _Video = Video;
            ApplyCurrentFrame();
        }
        else
        {
            SetActualSource(null);
        }
    }

    private void ReleaseVideo()
    {
        if (_Video != null)
        {
            _Video.Dispose();
            _Video = null;
        }
        _HasEnded = false;
        _HasBeenSkipped = false;
    }

    private void ApplyCurrentFrame()
    {
        var Image = _Video.CurrentImage;
        SetActualSource(Image == null ? null : new MGTextureData(Image, _Video.CurrentSourceRect));
    }

    /// <summary>Restarts the video from its first frame. <see cref="Ended"/> and <see cref="Skipped"/> may then be raised again.</summary>
    public void Restart()
    {
        if (_Video == null)
        {
            return;
        }

        _Video.Restart();
        _HasEnded = false;
        _HasBeenSkipped = false;
        ApplyCurrentFrame();
    }

    /// <summary>Ends the video early: raises <see cref="Skipped"/> and runs <see cref="SkippedCommandName"/>. The video stops advancing and shows the frame it was on.</summary>
    /// <returns>True if the video was skipped. False if <see cref="IsSkippable"/> is false, or if there is no video playing (none resolved, already ended, already skipped).</returns>
    public bool Skip()
    {
        if (!IsSkippable || !IsPlaying)
        {
            return false;
        }

        _HasBeenSkipped = true;
        Skipped?.Invoke(this, EventArgs.Empty);
        RunCommand(SkippedCommandName);
        return true;
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

        if (IsPlaying)
        {
            _Video.Advance(UA.BA.FrameElapsed);
            ApplyCurrentFrame();

            if (_Video.IsEnded)
            {
                _HasEnded = true;
                Ended?.Invoke(this, EventArgs.Empty);
                RunCommand(EndedCommandName);
            }
        }
    }

    private void RunCommand(string CommandName)
    {
        if (CommandName != null && GetResources().TryGetCommand(CommandName, out Action<MGElement> Command))
        {
            Command(this);
        }
    }

    /// <summary>Disposes the video. The element then shows nothing and plays nothing.</summary>
    public void Dispose()
    {
        if (!_IsDisposed)
        {
            _IsDisposed = true;
            ReleaseVideo();
            SetActualSource(null);
        }
    }
}
