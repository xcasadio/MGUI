using Microsoft.Xna.Framework;
using System.ComponentModel;
using System.Diagnostics;
using MGUI.Core.UI.DataBinding.Converters;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI;

[TypeConverter(typeof(StringToToolTipTypeConverter))]
public class MGToolTip : MGWindow
{
    public MGElement Host { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _ShowOnDisabled;
    /// <summary>True if this <see cref="MGToolTip"/> can be shown on an <see cref="MGElement"/> where <see cref="MGElement.IsEnabled"/>==false.<para/>
    /// Default value: false</summary>
    public bool ShowOnDisabled
    {
        get => _ShowOnDisabled;
        set
        {
            if (_ShowOnDisabled != value)
            {
                _ShowOnDisabled = value;
                NotifyPropertyChanged(nameof(ShowOnDisabled));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Point _DrawOffset;
    /// <summary>An offset from the current mouse cursor position to draw this <see cref="MGToolTip"/> at.<para/>
    /// Default value: <see cref="MGTheme.ToolTipOffset"/></summary>
    public Point DrawOffset
    {
        get => _DrawOffset;
        set
        {
            if (_DrawOffset != value)
            {
                _DrawOffset = value;
                NotifyPropertyChanged(nameof(DrawOffset));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TimeSpan? _ShowDelayOverride;
    /// <summary>The amount of time that the mouse must hover a particular <see cref="MGElement"/> before its <see cref="MGElement.ToolTip"/> can be shown.<para/>
    /// If not null, this value takes precedence over <see cref="MGDesktop.ToolTipShowDelay"/>.<para/>
    /// See also: <see cref="ActualShowDelay"/>, <see cref="MGDesktop.ToolTipShowDelay"/></summary>
    public TimeSpan? ShowDelayOverride
    {
        get => _ShowDelayOverride;
        set
        {
            if (_ShowDelayOverride != value)
            {
                _ShowDelayOverride = value;
                NotifyPropertyChanged(nameof(ShowDelayOverride));
                NotifyPropertyChanged(nameof(ActualShowDelay));
            }
        }
    }

    public TimeSpan ActualShowDelay => ShowDelayOverride ?? GetDesktop().ToolTipShowDelay;

    /// <summary>Without an explicit <paramref name="Theme"/>, the tooltip inherits the theme of <paramref name="Window"/>'s resource scope and follows its changes.</summary>
    public MGToolTip(MGWindow Window, MGElement Host, int Width, int Height, MGTheme Theme = null)
        : base(Window.Desktop, Theme, Window, MGElementType.ToolTip, 0, 0, Width, Height)
    {
        using (BeginInitializing())
        {
            this.Host = Host;
            DefaultControlTemplateName = MGControlTemplateCatalog.ToolTipTemplateName;
            ShowOnDisabled = false;
            ShowDelayOverride = null;
            IsUserResizable = false;
            IsTitleBarVisible = false;
            IsCloseButtonVisible = false;
            //  Excluded from window click-activation (decision utilisateur, Docs/input-window-activation-design.md section 3.a):
            //  a tooltip is a popup, not an interactive window whose click should reorder nested windows.
            ActivatesOnClick = false;

#if NEVER
                Host.OnLayoutBoundsChanged += (sender, e) =>
                {
                    //this.Left = (int)e.NewValue.BottomLeft.X;
                    //this.Top = (int)e.NewValue.BottomLeft.Y;
                };

                Window.ToolTipOpened += (sender, e) =>
                {
                    if (e == this)
                    {
                        //this.Left = (int)Host.LayoutBounds.Center.X;
                        //this.Top = (int)Host.LayoutBounds.Center.Y;
                    }
                };
#endif
        }
    }

    public void DrawAtDefaultPosition(ElementDrawArgs DA) => DrawAtMousePosition(DA, DrawOffset.X, DrawOffset.Y);
    public void DrawAtMousePosition(ElementDrawArgs DA, int XOffset = 5, int YOffset = 5)
    {
        var CurrentMousePosition = InputTracker.Mouse.CurrentPosition;
        Draw(DA with { Offset = DA.Offset + CurrentMousePosition + new Point(XOffset, YOffset) });
    }
}