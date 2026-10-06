using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using System.Diagnostics;

namespace MGUI.Core.UI;

/// <summary>Describes how content is resized to fill its allocated space.</summary>
public enum Stretch
{
    /// <summary>The content preserves its original size.</summary>
    None = 0,
    /// <summary>The content is resized to fill the destination dimensions. The aspect ratio is not preserved.</summary>
    Fill = 1,
    /// <summary>The content is resized to fit in the destination dimensions while it preserves its native aspect ratio.</summary>
    Uniform = 2,
    /// <summary>The content is resized to fill the destination dimensions while it preserves its native aspect ratio. 
    /// If the aspect ratio of the destination rectangle differs from the source, the source content is clipped to fit in the destination dimensions.</summary>
    UniformToFill = 3
}

/// <summary>Describes how scaling applies to content and restricts scaling to named axis types.</summary>
public enum StretchDirection
{
    /// <summary>The content scales upward only when it is smaller than the parent. If the content is larger, no scaling downward is performed.</summary>
    UpOnly = 0,
    /// <summary>The content scales downward only when it is larger than the parent. If the content is smaller, no scaling upward is performed.</summary>
    DownOnly = 1,
    /// <summary>The content stretches to fit the parent according to the Stretch mode.</summary>
    Both = 2
}

public class MGImage : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _SourceName;
    /// <summary>The name of the <see cref="MGTextureData"/> resource to draw, 
    /// or null if the <see cref="MGTextureData"/> is explicitly specified via <see cref="Source"/>.<br/>
    /// This resource is retrieved from <see cref="MGResources.Textures"/><para/>
    /// See also:<br/><see cref="MGElement.GetResources"/><br/><see cref="MGResources.Textures"/><br/><see cref="MGResources.AddTexture(string, MGTextureData)"/></summary>
    public string SourceName
    {
        get => _SourceName;
        set
        {
            if (_SourceName != value)
            {
                _SourceName = value;
                NotifyPropertyChanged(nameof(SourceName));
                if (SourceName != null)
                {
                    SubscribeToTextureEventsOnce();
                }
                UpdateActualSource();
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGResources _SubscribedTextureResources;

    /// <summary>Subscribes to <see cref="MGResources.OnTextureAdded"/>/<see cref="MGResources.OnTextureRemoved"/> at most once
    /// for this element's lifetime, instead of on every change of <see cref="SourceName"/> (ADR-0016, "SourceName without event churn").</summary>
    private void SubscribeToTextureEventsOnce()
    {
        if (_SubscribedTextureResources == null)
        {
            _SubscribedTextureResources = GetResources();
            _SubscribedTextureResources.OnTextureAdded += Resources_OnTextureAddedRemoved;
            _SubscribedTextureResources.OnTextureRemoved += Resources_OnTextureAddedRemoved;
        }
    }

    private void Resources_OnTextureAddedRemoved(object sender, (string Name, MGTextureData Data) e)
    {
        if (e.Name == SourceName)
        {
            UpdateActualSource();
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGTextureData? _Source;
    /// <summary>The <see cref="MGTextureData"/> resource to draw,
    /// or null if the <see cref="MGTextureData"/> is instead referenced by name via <see cref="SourceName"/>.<para/>
    /// See also: <see cref="SourceName"/>, <see cref="ActualSource"/></summary>
    public MGTextureData? Source
    {
        get => _Source;
        set
        {
            if (_Source != value)
            {
                _Source = value;
                NotifyPropertyChanged(nameof(Source));
                UpdateActualSource();
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IUIAnimatedImage _AnimatedSource;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Point _AnimatedDrawOffset;

    private void UpdateActualSource()
    {
        if (_AnimatedSource != null)
        {
            _AnimatedSource.Dispose();
            _AnimatedSource = null;
        }
        _AnimatedDrawOffset = Point.Zero;

        if (Source != null)
        {
            ActualSource = Source;
        }
        else if (GetResources().TryGetTexture(SourceName, out var Texture))
        {
            ActualSource = Texture;
        }
        else if (SourceName != null && GetResources().AssetProvider?.TryCreateAnimatedImage(SourceName, out var AnimatedImage) == true)
        {
            _AnimatedSource = AnimatedImage;
            _AnimatedSource.Restart(AnimationStartOffset);
            ApplyAnimatedFrame();
        }
        else
        {
            ActualSource = null;
        }
    }

    /// <summary>Copies the current frame of <see cref="_AnimatedSource"/> into <see cref="ActualSource"/> and <see cref="_AnimatedDrawOffset"/>.
    /// Does not advance the animation; call <see cref="IUIAnimatedImage.Advance(TimeSpan)"/> first if that is desired.</summary>
    private void ApplyAnimatedFrame()
    {
        var Frame = _AnimatedSource.CurrentImage;
        _AnimatedDrawOffset = _AnimatedSource.CurrentDrawOffset;
        ActualSource = Frame == null ? null : new MGTextureData(Frame, _AnimatedSource.CurrentSourceRect);
    }

    /// <summary>Restarts the currently-set animated source (if any) at <see cref="AnimationStartOffset"/> and refreshes the
    /// displayed frame, without waiting for the next <see cref="UpdateSelf(ElementUpdateArgs)"/>.</summary>
    private void RestartAnimation()
    {
        if (_AnimatedSource == null)
        {
            return;
        }

        _AnimatedSource.Restart(AnimationStartOffset);
        ApplyAnimatedFrame();
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TimeSpan _AnimationStartOffset;
    /// <summary>The point in the animation's timeline to restart at. Setting this restarts the animation at the new offset.<para/>
    /// Default value: <see cref="TimeSpan.Zero"/><para/>
    /// See also: <see cref="IsAnimationPlaying"/></summary>
    public TimeSpan AnimationStartOffset
    {
        get => _AnimationStartOffset;
        set
        {
            if (_AnimationStartOffset != value)
            {
                _AnimationStartOffset = value;
                NotifyPropertyChanged(nameof(AnimationStartOffset));
                RestartAnimation();
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsAnimationPlaying = true;
    /// <summary>Whether the current animated source (if any) advances every frame in <see cref="UpdateSelf(ElementUpdateArgs)"/>.<para/>
    /// Setting this to false restarts the animation at <see cref="AnimationStartOffset"/> and holds its first frame there.<br/>
    /// Setting this to true resumes playing from <see cref="AnimationStartOffset"/>.<para/>
    /// Default value: true</summary>
    public bool IsAnimationPlaying
    {
        get => _IsAnimationPlaying;
        set
        {
            if (_IsAnimationPlaying != value)
            {
                _IsAnimationPlaying = value;
                NotifyPropertyChanged(nameof(IsAnimationPlaying));
                RestartAnimation();
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGTextureData? _ActualSource;
    /// <summary>Prioritizes <see cref="Source"/> if specified, otherwise attempts to retrieve the named texture resource from <see cref="MGResources.Textures"/> based on <see cref="SourceName"/></summary>
    public MGTextureData? ActualSource
    {
        get => _ActualSource;
        private set
        {
            if (ActualSource != value)
            {
                var PreviousSize = ActualSource?.RenderSize;
                _ActualSource = value;
                NotifyPropertyChanged(nameof(ActualSource));
                if (ActualSource?.RenderSize != PreviousSize)
                {
                    LayoutChanged(this, true);
                }
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Color? _TextureColor;
    /// <summary>A color to use when drawing the texture. Uses <see cref="Color.White"/> if null.</summary>
    public Color? TextureColor
    {
        get => _TextureColor;
        set
        {
            if (_TextureColor != value)
            {
                _TextureColor = value;
                NotifyPropertyChanged(nameof(TextureColor));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _Brightness = 1f;
    /// <summary>A multiplier applied to the texture's colors when drawing, on top of <see cref="TextureColor"/> (ADR-0021).<para/>
    /// 1 draws the texture unchanged. Below 1 darkens it with a single draw. Above 1 brightens it: the texture is drawn once unchanged,
    /// then a second time with <see cref="BlendType.Additive"/> and a mask of (<see cref="Brightness"/> - 1), so each channel saturates at 255,
    /// as a modulate-and-saturate color unit does (a multiplier cannot exceed 1 in a plain color mask).<para/>
    /// Default value: 1</summary>
    public float Brightness
    {
        get => _Brightness;
        set
        {
            if (_Brightness != value)
            {
                _Brightness = value;
                NotifyPropertyChanged(nameof(Brightness));
            }
        }
    }

    private int UnstretchedWidth => ActualSource?.RenderSize.Width ?? 0;
    private int UnstretchedHeight => ActualSource?.RenderSize.Height ?? 0;
    private double UnstretchedAspectRatio => UnstretchedHeight == 0 ? 1.0 : UnstretchedWidth * 1.0 / UnstretchedHeight;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _UseLinearFilteringWhenDownscaling = true;
    private DrawSettings _CachedLinearFilteringBaseSettings;
    private DrawSettings _CachedLinearFilteringSettings;
    /// <summary>When true, images temporarily switch to linear sampling while being rendered smaller than their source size.</summary>
    public bool UseLinearFilteringWhenDownscaling
    {
        get => _UseLinearFilteringWhenDownscaling;
        set
        {
            if (_UseLinearFilteringWhenDownscaling != value)
            {
                _UseLinearFilteringWhenDownscaling = value;
                NotifyPropertyChanged(nameof(UseLinearFilteringWhenDownscaling));
            }
        }
    }

    private DrawSettings GetLinearFilteringSettings(DrawSettings baseSettings)
    {
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (!ReferenceEquals(_CachedLinearFilteringBaseSettings, baseSettings))
        {
            _CachedLinearFilteringBaseSettings = baseSettings;
            _CachedLinearFilteringSettings = baseSettings with { SamplerType = SamplerType.LinearClamp };
        }

        return _CachedLinearFilteringSettings;
    }

    private DrawSettings _CachedAdditiveBaseSettings;
    private DrawSettings _CachedAdditiveSettings;

    private DrawSettings GetAdditiveSettings(DrawSettings baseSettings)
    {
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (!ReferenceEquals(_CachedAdditiveBaseSettings, baseSettings))
        {
            _CachedAdditiveBaseSettings = baseSettings;
            _CachedAdditiveSettings = baseSettings with { BlendType = BlendType.Additive };
        }

        return _CachedAdditiveSettings;
    }

    /// <summary>An opaque gray mask of <c>round(255 * Amount)</c> per RGB channel, clamped to 0..255.</summary>
    private static Color GetBrightnessMask(float Amount)
    {
        byte Channel = (byte)Math.Clamp((int)Math.Round(255f * Amount, MidpointRounding.AwayFromZero), 0, 255);
        return new Color(Channel, Channel, Channel, (byte)255);
    }

    private static byte MultiplyChannels(byte A, byte B) => (byte)((A * B + 127) / 255);

    private static Color Multiply(Color? TextureColor, Color Mask)
    {
        if (!TextureColor.HasValue)
        {
            return Mask;
        }

        Color Value = TextureColor.Value;
        return new Color(MultiplyChannels(Value.R, Mask.R), MultiplyChannels(Value.G, Mask.G), MultiplyChannels(Value.B, Mask.B), MultiplyChannels(Value.A, Mask.A));
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Stretch _Stretch;
    public Stretch Stretch
    {
        get => _Stretch;
        set
        {
            if (_Stretch != value)
            {
                _Stretch = value;
                NotifyPropertyChanged(nameof(Stretch));
                LayoutChanged(this, true);
            }
        }
    }

    //  I'm too lazy to try implementing this. Nobody will care, right?
    /*[DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private StretchDirection _StretchDirection;
    public StretchDirection StretchDirection
    {
        get => _StretchDirection;
        set
        {
            if (_StretchDirection != value)
            {
                _StretchDirection = value;
                NotifyPropertyChanged(nameof(StretchDirection));
                LayoutChanged(this, true);
            }
        }
    }*/

    /// <param name="SourceName">The name of the <see cref="MGTextureData"/> in <see cref="MGResources.Textures"/> that should be drawn by this <see cref="MGImage"/>.<para/>
    /// See also: <see cref="MGElement.GetResources"/>, <see cref="MGResources.Textures"/>, <see cref="MGImage.SourceName"/></param>
    public MGImage(MGWindow Window, string SourceName, Stretch Stretch = Stretch.Uniform)
        : base(Window, MGElementType.Image)
    {
        using (BeginInitializing())
        {
            this.SourceName = SourceName;
            TextureColor = null;
            this.Stretch = Stretch;

            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    public MGImage(MGWindow Window, IUIImageResource Image, Rectangle? SourceRect = null, Color? TextureColor = null, Stretch Stretch = Stretch.Uniform)
        : this(Window, new MGTextureData(Image, SourceRect), TextureColor, Stretch) { }

    public MGImage(MGWindow Window, MGTextureData Source, Color? TextureColor = null, Stretch Stretch = Stretch.Uniform)
        : base(Window, MGElementType.Image)
    {
        using (BeginInitializing())
        {
            this.Source = Source;
            this.TextureColor = TextureColor;
            this.Stretch = Stretch;

            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

        if (_AnimatedSource != null && IsAnimationPlaying)
        {
            _AnimatedSource.Advance(UA.BA.FrameElapsed);
            ApplyAnimatedFrame();
        }
    }

    private static int GetWidthByAspectRatio(int Height, double AspectRatio) => (int)Math.Round(Height * AspectRatio, MidpointRounding.ToEven);
    private static int GetHeightByAspectRatio(int Width, double AspectRatio) => (int)Math.Round(Width * 1 / AspectRatio, MidpointRounding.ToEven);

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        SharedSize = new(0);

        var AvailableWidth = AvailableSize.Width;
        var AvailableHeight = AvailableSize.Height;

        //  If Width or Height is arbitrarily large, this element is being measured within a ScrollViewer.
        //  That means we can't just request the AvailableSize, or we'd end up with infinitely-sized content inside the ScrollViewer.
        var IsPseudoInfiniteWidth = AvailableWidth >= 1000000;
        var IsPseduoInfiniteHeight = AvailableHeight >= 1000000;

        int Width;
        int Height;

        var AspectRatio = UnstretchedAspectRatio;
        if (Stretch == Stretch.None)
        {
            Width = UnstretchedWidth;
            Height = UnstretchedHeight;
        }
        else if (Stretch == Stretch.Uniform)
        {
            //int Width = Math.Min(AvailableSize.Width, UnstretchedWidth);
            //int Height = (int)Math.Round(Math.Min(AvailableSize.Width, UnstretchedWidth) * 1 / UnstretchedAspectRatio, MidpointRounding.ToEven);

            if (IsPseudoInfiniteWidth && IsPseudoInfiniteWidth)
            {
                //  If both dimensions are infinite, it's ambiguous as to how much space to request
                Width = UnstretchedWidth;
                Height = UnstretchedHeight;
            }
            else if (IsPseudoInfiniteWidth)
            {
                Height = AvailableHeight;
                Width = GetWidthByAspectRatio(Height, AspectRatio);
            }
            else if (IsPseduoInfiniteHeight)
            {
                Width = AvailableWidth;
                Height = GetHeightByAspectRatio(Width, AspectRatio);
            }
            else
            {
                Width = Math.Min(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
                Height = Math.Min(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            }
        }
        else if (Stretch == Stretch.UniformToFill)
        {
            //int Width = Math.Min(AvailableSize.Width, UnstretchedWidth);
            //int Height = Math.Max(UnstretchedHeight, (int)Math.Round(Math.Min(AvailableSize.Width, UnstretchedWidth) * 1 / UnstretchedAspectRatio, MidpointRounding.ToEven));

            if (IsPseudoInfiniteWidth && IsPseudoInfiniteWidth)
            {
                Width = UnstretchedWidth;
                Height = UnstretchedHeight;
            }
            else if (IsPseudoInfiniteWidth)
            {
                Height = AvailableHeight;
                Width = GetWidthByAspectRatio(Height, AspectRatio);
            }
            else if (IsPseduoInfiniteHeight)
            {
                Width = AvailableWidth;
                Height = GetHeightByAspectRatio(Width, AspectRatio);
            }
            else
            {
                Width = Math.Max(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
                Height = Math.Max(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            }
        }
        else if (Stretch == Stretch.Fill)
        {
            if (IsPseudoInfiniteWidth && IsPseudoInfiniteWidth)
            {
                Width = UnstretchedWidth;
                Height = UnstretchedHeight;
            }
            else if (IsPseudoInfiniteWidth)
            {
                Width = UnstretchedWidth;
                Height = AvailableHeight;
            }
            else if (IsPseduoInfiniteHeight)
            {
                Width = AvailableWidth;
                Height = UnstretchedHeight;
            }
            else
            {
                Width = AvailableWidth;
                Height = AvailableHeight;
            }
        }
        else
        {
            throw new NotImplementedException($"Unrecognized {nameof(Stretch)}: {Stretch}");
        }

        return new Thickness(Width, Height, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (ActualSource?.Image == null)
        {
            return;
        }

        var PaddedBounds = LayoutBounds.GetCompressed(Padding);
        var AspectRatio = UnstretchedAspectRatio;

        Rectangle Bounds;
        if (Stretch == Stretch.None)
        {
            Bounds = ApplyAlignment(PaddedBounds, HorizontalContentAlignment, VerticalContentAlignment, new Size(UnstretchedWidth, UnstretchedHeight));
        }
        else if (Stretch == Stretch.Uniform)
        {
            var AvailableWidth = PaddedBounds.Width;
            var AvailableHeight = PaddedBounds.Height;
            var ConsumedWidth = Math.Min(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            var ConsumedHeight = Math.Min(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            Bounds = ApplyAlignment(PaddedBounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(ConsumedWidth, ConsumedHeight));
        }
        else if (Stretch == Stretch.UniformToFill)
        {
            var AvailableWidth = PaddedBounds.Width;
            var AvailableHeight = PaddedBounds.Height;
            var ConsumedWidth = Math.Max(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            var ConsumedHeight = Math.Max(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            Bounds = ApplyAlignment(PaddedBounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(ConsumedWidth, ConsumedHeight));
        }
        else if (Stretch == Stretch.Fill)
        {
            Bounds = PaddedBounds;
        }
        else
        {
            throw new NotImplementedException($"Unrecognized {nameof(Stretch)}: {Stretch}");
        }

        var destinationBounds = Bounds.GetTranslated(DA.Offset).GetTranslated(_AnimatedDrawOffset);
        var isDownscaling = destinationBounds.Width < UnstretchedWidth || destinationBounds.Height < UnstretchedHeight;
        var shouldUseLinearFiltering = UseLinearFilteringWhenDownscaling
                                       && isDownscaling
                                       && (DA.Context.CurrentSettings.SamplerType == SamplerType.PointClamp || DA.Context.CurrentSettings.SamplerType == SamplerType.PointWrap);

        var previousSettings = DA.Context.CurrentSettings;
        var baseSettings = shouldUseLinearFiltering ? GetLinearFilteringSettings(previousSettings) : previousSettings;

        //  Brightness (ADR-0021): below 1 the texture is darkened by the mask of a single draw. Above 1 a color mask cannot exceed 1,
        //  so the texture is drawn unchanged, then a second time additively with a mask of (Brightness - 1): each channel saturates at 255.
        var firstColor = Brightness < 1f ? Multiply(TextureColor, GetBrightnessMask(Brightness)) : TextureColor;
        DrawSource(DA, destinationBounds, firstColor, baseSettings, previousSettings);

        if (Brightness > 1f)
        {
            DrawSource(DA, destinationBounds, Multiply(TextureColor, GetBrightnessMask(Brightness - 1f)), GetAdditiveSettings(baseSettings), previousSettings);
        }
    }

    /// <summary>Draws <see cref="ActualSource"/> with <paramref name="Settings"/>, then restores <paramref name="PreviousSettings"/>.
    /// The settings are only touched when they differ.</summary>
    private void DrawSource(ElementDrawArgs DA, Rectangle DestinationBounds, Color? Mask, DrawSettings Settings, DrawSettings PreviousSettings)
    {
        if (ReferenceEquals(Settings, PreviousSettings))
        {
            ActualSource.Value.Draw(DA.Context, DestinationBounds, Mask, DA.Opacity);
            return;
        }

        DA.Context.SetDrawSettings(Settings);
        try
        {
            ActualSource.Value.Draw(DA.Context, DestinationBounds, Mask, DA.Opacity);
        }
        finally
        {
            DA.Context.SetDrawSettings(PreviousSettings);
        }
    }
}