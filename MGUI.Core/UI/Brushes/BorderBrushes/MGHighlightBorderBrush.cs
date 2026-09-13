using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Diagnostics;
using MGUI.Core.UI.Shapes;

namespace MGUI.Core.UI.Brushes.BorderBrushes;

public enum HighlightAnimation : byte
{
	/// <summary>The highlight will appear, fade out, wait a moment, then repeat.<para/>
	/// See also: <see cref="MGHighlightBorderBrush.PulseFadeDuration"/>, <see cref="MGHighlightBorderBrush.PulseDelay"/></summary>
	Pulse,
	/// <summary>The highlight will continually swap between and on and off state to create a flashing animation</summary>
	Flash,
	/// <summary>The highlight will cover a small slice of the border, and travel around the border similar to a circular progress bar's animation</summary>
	Progress,
	/// <summary>The highlight will move either vertically or horizontally across the bounds of the border, looping around when reaching one end</summary>
	Scan
}

public enum HighlightFlowDirection : byte
{
	Clockwise,
	CounterClockwise
}

/// <summary>An <see cref="IBorderBrush"/> that draws a simple animation (using a <see cref="HighlightColor"/>) overtop of the border, typically to direct the user's attention
/// to the bordered element. <see cref="MGHighlightBorderBrush"/>es are particularly useful for tutorials or directing the user's focus to a newly-unlocked piece of content on the UI.<para/>
/// See also: <see cref="MGUniformBorderBrush"/>, <see cref="MGDockedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGBandedBorderBrush"/>, <see cref="MGCompositedBorderBrush"/></summary>
public class MGHighlightBorderBrush : ViewModelBase, IBorderBrush
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private IBorderBrush _Underlay;
	/// <summary>The underlying border which the highlight will be rendered overtop of.</summary>
	public IBorderBrush Underlay
	{
		get => _Underlay;
		set
		{
			if (_Underlay != value)
			{
				_Underlay = value;
				NPC(nameof(Underlay));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private Color _HighlightColor;
	/// <summary>The color to use when drawing the highlight overtop of the <see cref="Underlay"/></summary>
	public Color HighlightColor
	{
		get => _HighlightColor;
		set
		{
			if (_HighlightColor != value)
			{
				_HighlightColor = value;
				NPC(nameof(HighlightColor));
				HighlightFillBrush = HighlightColor.AsFillBrush();
				HighlightBorderBrush = new MGUniformBorderBrush(HighlightFillBrush);
			}
		}
	}

	private MGUniformBorderBrush HighlightBorderBrush = MGUniformBorderBrush.Transparent;
	private MGSolidFillBrush HighlightFillBrush = SolidFillBrushes.Transparent;

	#region Animation Settings
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private HighlightAnimation _AnimationType;
	/// <summary>The animation to use when drawing the highlight overtop of the <see cref="Underlay"/>.<para/>
	/// For further control over the animation:<br/>
	/// If set to <see cref="HighlightAnimation.Pulse"/>: Set <see cref="PulseFadeDuration"/> and <see cref="PulseDelay"/><br/>
	/// If set to <see cref="HighlightAnimation.Flash"/>: Set <see cref="FlashShowDuration"/> and <see cref="FlashHideDuration"/><br/>
	/// If set to <see cref="HighlightAnimation.Progress"/>: Set <see cref="ProgressFlowDirection"/>, <see cref="ProgressDuration"/>, <see cref="ProgressSize"/><br/>
	/// If set to <see cref="HighlightAnimation.Scan"/>: Set <see cref="ScanOrientation"/>, <see cref="ScanIsReversed"/>, <see cref="ScanDuration"/>, <see cref="ScanSize"/><para/>
	/// See also: <see cref="AnimationProgress"/></summary>
	public HighlightAnimation AnimationType
	{
		get => _AnimationType;
		set
		{
			if (_AnimationType != value)
			{
				_AnimationType = value;
				NPC(nameof(AnimationType));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private double _AnimationProgress;
	/// <summary>Determines what percentage of the current animation cycle is complete. Min = 0.0 (0%), Max = 1.0 (100%)<para/>
	/// For example, if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Flash"/>, one cycle represents showing the highlight for a set amount of time, then hiding for a set amount of time.<para/>
	/// Note: For <see cref="HighlightAnimation.Progress"/>, this progress value represents where the CENTER of the highlight should appear on the perimeter. 0.0 = topleft, 0.50 = bottomright etc.</summary>
	/// <remarks>If this value is not in the range [0.0,1.0], only the decimal portion is used. EX: 2.35 is equivalent to 0.35.<br/>
	/// See also: <see cref="ActualAnimationProgress"/></remarks>
	public double AnimationProgress
	{
		get => _AnimationProgress;
		set
		{
			if (_AnimationProgress != value)
			{
				_AnimationProgress = value;
				NPC(nameof(AnimationProgress));
				NPC(nameof(ActualAnimationProgress));

				if (AnimationProgress < 0.0)
				{
					throw new InvalidOperationException($"{nameof(MGBandedBorderBrush)}.{nameof(AnimationProgress)} cannot be negative. Value: {AnimationProgress}");
				}
			}
		}
	}

	/// <summary>Same value as <see cref="AnimationProgress"/>, except this is converted to the range [0.0, 1.0]</summary>
	public double ActualAnimationProgress => 
		AnimationProgress == (int)AnimationProgress && AnimationProgress != 0.0 ? 1.0 : // Whole numbers above zero should be treated as 100% completion instead of 0%
			AnimationProgress - Math.Truncate(AnimationProgress);

	#region Pulse Settings
	/// <summary>Default value: 2.0s</summary>
	public static TimeSpan DefaultPulseFadeDuration { get; set; } = TimeSpan.FromSeconds(2.0);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _PulseFadeDuration;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Pulse"/>.<para/>
	/// The amount of time it will take for the <see cref="HighlightColor"/> to fade out from 100% to 0% opacity.<para/>
	/// Default value: <see cref="DefaultPulseFadeDuration"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="PulseDelay"/></summary>
	public TimeSpan PulseFadeDuration
	{
		get => _PulseFadeDuration;
		set
		{
			if (_PulseFadeDuration != value)
			{
				_PulseFadeDuration = value;
				NPC(nameof(PulseFadeDuration));
			}
		}
	}

	/// <summary>Default value: 0.2s</summary>
	public static TimeSpan DefaultPulseDelay { get; set; } = TimeSpan.FromSeconds(0.2);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _PulseDelay;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Pulse"/>.<para/>
	/// The amount of time that the <see cref="HighlightColor"/> remains at 0% opacity before cycling back to 100% opacity.<para/>
	/// Default value: <see cref="DefaultPulseDelay"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="PulseFadeDuration"/></summary>
	public TimeSpan PulseDelay
	{
		get => _PulseDelay;
		set
		{
			if (_PulseDelay != value)
			{
				_PulseDelay = value;
				NPC(nameof(PulseDelay));
			}
		}
	}

	private TimeSpan PulseCycleDuration => PulseFadeDuration + PulseDelay;
	#endregion Pulse Settings

	#region Flash Settings
	/// <summary>Default value: 0.4s</summary>
	public static TimeSpan DefaultFlashShowDuration { get; set; } = TimeSpan.FromSeconds(0.4);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _FlashShowDuration;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Flash"/>.<para/>
	/// The amount of time that the <see cref="HighlightColor"/> is visible during each flash cycle.<para/>
	/// Default value: <see cref="DefaultFlashShowDuration"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="FlashHideDuration"/></summary>
	public TimeSpan FlashShowDuration
	{
		get => _FlashShowDuration;
		set
		{
			if (_FlashShowDuration != value)
			{
				_FlashShowDuration = value;
				NPC(nameof(FlashShowDuration));
			}
		}
	}

	/// <summary>Default value: 0.4s</summary>
	public static TimeSpan DefaultFlashHideDuration { get; set; } = TimeSpan.FromSeconds(0.4);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _FlashHideDuration;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Flash"/>.<para/>
	/// The amount of time that the <see cref="HighlightColor"/> is hidden during each flash cycle.<para/>
	/// Default value: <see cref="DefaultFlashHideDuration"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="FlashShowDuration"/></summary>
	public TimeSpan FlashHideDuration
	{
		get => _FlashHideDuration;
		set
		{
			if (_FlashHideDuration != value)
			{
				_FlashHideDuration = value;
				NPC(nameof(FlashHideDuration));
			}
		}
	}

	private TimeSpan FlashCycleDuration => FlashShowDuration + FlashHideDuration;
	#endregion Flash Settings

	#region Progress Settings
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private HighlightFlowDirection _ProgressFlowDirection;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Progress"/>.<para/>
	/// The direction that the animation moves in.<para/>
	/// Default value: <see cref="HighlightFlowDirection.Clockwise"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ProgressDuration"/></summary>
	public HighlightFlowDirection ProgressFlowDirection
	{
		get => _ProgressFlowDirection;
		set
		{
			if (_ProgressFlowDirection != value)
			{
				_ProgressFlowDirection = value;
				NPC(nameof(ProgressFlowDirection));
			}
		}
	}

	/// <summary>Default value: 3s</summary>
	public static TimeSpan DefaultProgressDuration { get; set; } = TimeSpan.FromSeconds(3.0);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _ProgressDuration;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Progress"/>.<para/>
	/// Determines how long it takes for the highlight to make one complete loop around the perimeter.<para/>
	/// Default value: <see cref="DefaultProgressDuration"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ProgressFlowDirection"/>,<br/><see cref="ProgressSize"/></summary>
	public TimeSpan ProgressDuration
	{
		get => _ProgressDuration;
		set
		{
			if (_ProgressDuration != value)
			{
				_ProgressDuration = value;
				NPC(nameof(ProgressDuration));
			}
		}
	}

	/// <summary>Default value: 0.25</summary>
	public static double DefaultProgressSize { get; set; } = 0.25;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private double _ProgressSize;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Progress"/>.<para/>
	/// Determines what percentage of the perimeter is highlighted.<para/>
	/// Default value: <see cref="DefaultProgressSize"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ProgressFlowDirection"/>,<br/><see cref="ProgressDuration"/></summary>
	public double ProgressSize
	{
		get => _ProgressSize;
		set
		{
			if (_ProgressSize != value)
			{
				_ProgressSize = value;
				NPC(nameof(ProgressSize));
			}
		}
	}
	#endregion Progress Settings

	#region Scan Settings
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private Orientation _ScanOrientation;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Scan"/>.<para/>
	/// The orientation of the highlight.<br/>
	/// If <see cref="Orientation.Vertical"/>, the highlight will be a vertical line that moves horizontally along the bounds of the border.<br/>
	/// If <see cref="Orientation.Horizontal"/>, the highlight will be a horizontal line that moves vertically along the bounds of the border.<para/>
	/// Default value: <see cref="Orientation.Horizontal"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ScanIsReversed"/>,<br/><see cref="ScanDuration"/>,<br/><see cref="ScanSize"/></summary>
	public Orientation ScanOrientation
	{
		get => _ScanOrientation;
		set
		{
			if (_ScanOrientation != value)
			{
				_ScanOrientation = value;
				NPC(nameof(ScanOrientation));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _ScanIsReversed;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Scan"/>.<para/>
	/// If <see langword="false"/>, the highlight will move either top-to-bottom or left-to-right, depending on <see cref="ScanOrientation"/>.<br/>
	/// If <see langword="true"/>, the highlight will move either bottom-to-top or right-to-left, depending on <see cref="ScanOrientation"/>.<para/>
	/// Default value: <see langword="false"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ScanOrientation"/>,<br/><see cref="ScanDuration"/>,<br/><see cref="ScanSize"/></summary>
	public bool ScanIsReversed
	{
		get => _ScanIsReversed;
		set
		{
			if (_ScanIsReversed != value)
			{
				_ScanIsReversed = value;
				NPC(nameof(ScanIsReversed));
			}
		}
	}

	/// <summary>Default value: 1.5s</summary>
	public static TimeSpan DefaultScanDuration { get; set; } = TimeSpan.FromSeconds(1.5);

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private TimeSpan _ScanDuration;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Scan"/>.<para/>
	/// Determines how long it takes for the highlight to fully move across the bounds of the border.<para/>
	/// Default value: <see cref="DefaultScanDuration"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ScanOrientation"/>,<br/><see cref="ScanIsReversed"/>,<br/><see cref="ScanSize"/></summary>
	public TimeSpan ScanDuration
	{
		get => _ScanDuration;
		set
		{
			if (_ScanDuration != value)
			{
				_ScanDuration = value;
				NPC(nameof(ScanDuration));
			}
		}
	}

	/// <summary>Default value: 0.2</summary>
	public static double DefaultScanSize { get; set; } = 0.2;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private double _ScanSize;
	/// <summary>Only relevant if <see cref="AnimationType"/> is set to <see cref="HighlightAnimation.Scan"/>.<para/>
	/// Determines the percentage of the border that is filled by the highlight. 0.0 = 0%, 1.0 = 100%<br/>
	/// If <see cref="ScanOrientation"/> is <see cref="Orientation.Vertical"/>, this determines the width of the highlight.<br/>
	/// If <see cref="ScanOrientation"/> is <see cref="Orientation.Horizontal"/>, this determines the height of the highlight.<para/>
	/// Default value: <see cref="DefaultScanSize"/><para/>
	/// See also:<br/><see cref="AnimationType"/>,<br/><see cref="ScanOrientation"/>,<br/><see cref="ScanIsReversed"/>,<br/><see cref="ScanDuration"/></summary>
	public double ScanSize
	{
		get => _ScanSize;
		set
		{
			if (_ScanSize != value)
			{
				_ScanSize = value;
				NPC(nameof(ScanSize));
			}
		}
	}
	#endregion Scan Settings
	#endregion Animation Settings

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _IsEnabled;
	/// <summary>If <see langword="true"/>, the <see cref="HighlightColor"/> will be used to draw the highlight overtop of the <see cref="Underlay"/>.</summary>
	public bool IsEnabled
	{
		get => _IsEnabled;
		set
		{
			if (_IsEnabled != value)
			{
				_IsEnabled = value;
				NPC(nameof(IsEnabled));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private MGElement _Target;
	/// <summary>The <see cref="MGElement"/> that this border brush is being drawn on. This value is optional and only used if <see cref="StopOnMouseOver"/> or <see cref="StopOnClick"/> are set to <see langword="true"/></summary>
	public MGElement Target
	{
		get => _Target;
		set
		{
			if (_Target != value)
			{
				_Target = value;
				NPC(nameof(Target));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _StopOnMouseOver;
	/// <summary>If <see langword="true"/>, <see cref="IsEnabled"/> will automatically be set to <see langword="false"/> when the <see cref="Target"/> <see cref="MGElement"/> is hovered by the mouse.</summary>
	public bool StopOnMouseOver
	{
		get => _StopOnMouseOver;
		set
		{
			if (_StopOnMouseOver != value)
			{
				_StopOnMouseOver = value;
				NPC(nameof(StopOnMouseOver));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _StopOnClick;
	/// <summary>If <see langword="true"/>, <see cref="IsEnabled"/> will automatically be set to <see langword="false"/> when the <see cref="Target"/> <see cref="MGElement"/> 
	/// is interacted with by the mouse via a Left mouse button press. (Press only, does not need to be a full press including a mouse button released event)</summary>
	public bool StopOnClick
	{
		get => _StopOnClick;
		set
		{
			if (_StopOnClick != value)
			{
				_StopOnClick = value;
				NPC(nameof(StopOnClick));
			}
		}
	}

	/// <param name="Underlay">The underlying border which the highlight will be rendered overtop of.</param>
	/// <param name="HighlightColor">The color to use when drawing the highlight overtop of the <see cref="Underlay"/></param>
	/// <param name="AnimationType">The animation to use when drawing the highlight overtop of the <see cref="Underlay"/>.</param>
	/// <param name="Target">The <see cref="MGElement"/> that this border brush is being drawn on. This value is optional and only used if <see cref="StopOnMouseOver"/> or <see cref="StopOnClick"/> are set to <see langword="true"/></param>
	public MGHighlightBorderBrush(IBorderBrush Underlay, Color HighlightColor, HighlightAnimation AnimationType, MGElement Target = null)
	{
		this.Underlay = Underlay;
		this.HighlightColor = HighlightColor;
		this.AnimationType = AnimationType;
		AnimationProgress = 0.0;
		this.Target = Target;

		IsEnabled = true;

		PulseFadeDuration = DefaultPulseFadeDuration;
		PulseDelay = DefaultPulseDelay;

		FlashShowDuration = DefaultFlashShowDuration;
		FlashHideDuration = DefaultFlashHideDuration;

		ProgressFlowDirection = HighlightFlowDirection.Clockwise;
		ProgressDuration = DefaultProgressDuration;
		ProgressSize = DefaultProgressSize;

		ScanOrientation = Orientation.Horizontal;
		ScanIsReversed = false;
		ScanDuration = DefaultScanDuration;
		ScanSize = DefaultScanSize;

		StopOnMouseOver = false;
		StopOnClick = false;
	}

	void IBorderBrush.Update(UpdateBaseArgs UA)
	{
		//  Forwards the per-frame lifecycle call to Underlay via PaintLifecycle, deduplicated by reference against
		//  every other slot/element that references it for the frame (see Docs/drawing-architecture.md, Limites connues).
		PaintLifecycle.Update(Underlay, UA);

		if (Target != null && 
		    ((StopOnMouseOver && Target.VisualState.Secondary == SecondaryVisualState.Hovered) ||
		     (StopOnClick && Target.VisualState.Secondary == SecondaryVisualState.Pressed)))
		{
			IsEnabled = false;
		}

		if (IsEnabled)
		{
			var CycleDuration = AnimationType switch
			{
				HighlightAnimation.Pulse => PulseCycleDuration,
				HighlightAnimation.Flash => FlashCycleDuration,
				HighlightAnimation.Progress => ProgressDuration,
				HighlightAnimation.Scan => ScanDuration,
				_ => throw new NotImplementedException($"Unrecognized {nameof(HighlightAnimation)}: {AnimationType}")
			};

			var ElapsedPercent = UA.FrameElapsed / CycleDuration;
			AnimationProgress += ElapsedPercent;
		}
	}

	private static int PositiveModulo(int x, int m)
	{
		var r = x % m;
		return r < 0 ? r + m : r;
	}

	private static double PositiveModulo(double x, double m)
	{
		var r = x % m;
		return r < 0 ? r + m : r;
	}

	public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
	{
		Underlay?.Draw(DA, Element, Bounds, BT);

		if (!IsEnabled || HighlightColor == Color.Transparent)
		{
			return;
		}

		var Progress = ActualAnimationProgress;
		switch (AnimationType)
		{
			case HighlightAnimation.Pulse:
				var FadePercent = PulseFadeDuration / PulseCycleDuration;
				if (Progress < FadePercent)
				{
					var OpacityScalar = 1.0f - (float)(Progress / FadePercent);
					HighlightBorderBrush.Draw(DA.SetOpacity(DA.Opacity * OpacityScalar), Element, Bounds, BT);
				}
				break;
			case HighlightAnimation.Flash:
				var IsVisible = Progress <= FlashShowDuration / FlashCycleDuration;
				if (IsVisible)
				{
					HighlightBorderBrush.Draw(DA, Element, Bounds, BT);
				}

				break;
			case HighlightAnimation.Progress:
			{
				var Edges = new List<Rectangle>();
				if (BT.Left > 0)
				{
					Edges.Add(new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
				}

				if (BT.Right > 0)
				{
					Edges.Add(new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
				}

				if (BT.Top > 0)
				{
					Edges.Add(new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
				}

				if (BT.Bottom > 0)
				{
					Edges.Add(new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
				}

				var OuterPerimeterLength = Bounds.Width * 2 + (Bounds.Height - 1) * 2;
				var InnerPerimeterLength = OuterPerimeterLength - BT.Width * 2 - BT.Height * 2;
				var AvgPerimeterLength = (OuterPerimeterLength + InnerPerimeterLength) / 2;

				//  Returns a point along the outer perimeter of the Rectangle bounds, where the point represents travelling along the perimeter by the given percent
				//  0.0 = topleft corner, moves clockwise (or counterclockwise if IsReversed=true) along the perimeter
				Point GetPerimeterPosition(double Percent, bool IsReversed, out int EdgeIndex)
				{
					var LinearPosition = PositiveModulo((int)(Percent * OuterPerimeterLength), OuterPerimeterLength);

					if (!IsReversed)
					{
						// Top edge
						if (LinearPosition <= Bounds.Width)
						{
							EdgeIndex = 0;
							return Bounds.TopLeft() + new Point(LinearPosition, 0);
						}
						// Right edge
						else if (LinearPosition <= Bounds.Width + Bounds.Height - 2)
						{
							EdgeIndex = 1;
							return Bounds.TopRight() + new Point(0, LinearPosition - Bounds.Width);
						}
						// Bottom edge
						else if (LinearPosition <= Bounds.Width + Bounds.Height - 2 + Bounds.Width)
						{
							EdgeIndex = 2;
							return Bounds.BottomRight() - new Point(LinearPosition - Bounds.Width - Bounds.Height + 1, 0);
						}
						// Left edge
						else
						{
							EdgeIndex = 3;
							return Bounds.BottomLeft() - new Point(0, LinearPosition - Bounds.Width - Bounds.Height + 2 - Bounds.Width);
						}
					}
					else
					{
						// Top edge
						if (LinearPosition == 0)
						{
							EdgeIndex = 0;
							return Bounds.TopLeft();
						}
						//  Left edge
						else if (LinearPosition <= Bounds.Height - 1)
						{
							EdgeIndex = 3;
							return Bounds.TopLeft() + new Point(0, LinearPosition);
						}
						//  Bottom edge
						else if (LinearPosition <= Bounds.Height - 1 + Bounds.Width)
						{
							EdgeIndex = 2;
							return Bounds.BottomLeft() + new Point(LinearPosition - Bounds.Height - 1, 0);
						}
						//  Right edge
						else if (LinearPosition <= Bounds.Height - 1 + Bounds.Width + Bounds.Height - 2)
						{
							EdgeIndex = 1;
							return Bounds.BottomRight() - new Point(0, LinearPosition - Bounds.Height + 1 - Bounds.Width);
						}
						//  Top edge
						else
						{
							EdgeIndex = 0;
							return Bounds.TopRight() - new Point(LinearPosition - Bounds.Height + 1 - Bounds.Width - Bounds.Height + 1, 0);
						}
					}
				}

				var StartPosition = GetPerimeterPosition(Progress - ProgressSize / 2.0, ProgressFlowDirection == HighlightFlowDirection.CounterClockwise, out var StartEdge);
				var EndPosition = GetPerimeterPosition(Progress + ProgressSize / 2.0, ProgressFlowDirection == HighlightFlowDirection.CounterClockwise, out var EndEdge);

				//  Create a list of vertices representing the outer bounds of the polygon we want to fill in
				List<Point> OuterVertices = new() { StartPosition };
				if (StartEdge != EndEdge) // If the start and end are on different edges of the bounds, append all the corners that they pass through
				{
					IReadOnlyDictionary<int, Point> OuterCornerLookup = new Dictionary<int, Point>()
					{
						{ 0, Bounds.TopLeft() },
						{ 1, Bounds.TopRight() },
						{ 2, Bounds.BottomRight() },
						{ 3, Bounds.BottomLeft() }
					};

					const int NumEdges = 4;
					var CurrentEdge = StartEdge;
					if (ProgressFlowDirection == HighlightFlowDirection.Clockwise)
					{
						while (CurrentEdge != EndEdge)
						{
							CurrentEdge = (CurrentEdge + 1) % NumEdges;
							OuterVertices.Add(OuterCornerLookup[CurrentEdge]);
						}
					}
					else if (ProgressFlowDirection == HighlightFlowDirection.CounterClockwise)
					{
						while (CurrentEdge != EndEdge)
						{
							OuterVertices.Add(OuterCornerLookup[CurrentEdge]);
							CurrentEdge = PositiveModulo(CurrentEdge - 1, NumEdges);
						}
					}
					else
					{
						throw new NotImplementedException($"Unrecognized {nameof(HighlightFlowDirection)}: {ProgressFlowDirection}");
					}
				}
				OuterVertices.Add(EndPosition);
				OuterVertices = OuterVertices.Distinct().ToList(); // There will be duplicate vertices if a start or end point was on a corner

				//  Key = Bounds of a corner, Value = the inner corner point
				var CornerBounds = new Dictionary<Rectangle, Point>()
				{
					{ new Rectangle(Bounds.Left, Bounds.Top, BT.Left, BT.Top), Bounds.TopLeft() + new Point(BT.Left, BT.Top) },
					{ new Rectangle(Bounds.Right - BT.Right, Bounds.Top, BT.Right, BT.Top), Bounds.TopRight() + new Point(-BT.Right, BT.Top) },
					{ new Rectangle(Bounds.Right - BT.Right, Bounds.Bottom - BT.Bottom, BT.Right, BT.Bottom), Bounds.BottomRight() + new Point(-BT.Right, -BT.Bottom) },
					{ new Rectangle(Bounds.Left, Bounds.Bottom - BT.Bottom, BT.Left, BT.Bottom), Bounds.BottomLeft() + new Point(BT.Left, -BT.Bottom) }
				};

				List<Point> Polygon = new(OuterVertices);
				foreach (var OuterVertex in OuterVertices.AsEnumerable().Reverse())
				{
					//  Compute the inner vertex that opposes this outer vertex
					var InnerVertex = Point.Zero;

					//  First check if the vertex is within a corner region and if so use the inner corner vertex
					var IsCorner = false;
					foreach (var KVP in CornerBounds)
					{
						if (KVP.Key.ContainsInclusive(OuterVertex))
						{
							InnerVertex = KVP.Value;
							IsCorner = true;
							break;
						}
					}

					//  Otherwise just move the vertex inwards by the corresponding edge's BorderThickness
					if (!IsCorner)
					{
						if (OuterVertex.Y == Bounds.Top) // Top Edge
						{
							InnerVertex = OuterVertex + new Point(0, BT.Top);
						}
						else if (OuterVertex.Y == Bounds.Bottom) // Bottom Edge
						{
							InnerVertex = OuterVertex + new Point(0, -BT.Bottom);
						}
						else if (OuterVertex.X == Bounds.Right) // Right Edge
						{
							InnerVertex = OuterVertex + new Point(-BT.Right, 0);
						}
						else if (OuterVertex.X == Bounds.Left) // Left Edge
						{
							InnerVertex = OuterVertex + new Point(BT.Left, 0);
						}
						else
						{
							throw new InvalidOperationException($"Flawed logic in {nameof(MGHighlightBorderBrush)}.{nameof(Draw)}: Vertex was not along the outer edge of the bounds.");
						}
					}

					Polygon.Add(InnerVertex);
				}
				Polygon = Polygon.Distinct().ToList();

				DA.DT.FillPolygon(DA.Offset.ToVector2(), Polygon.Select(x => x.ToVector2()), HighlightColor);
			}
				break;
			case HighlightAnimation.Scan:
			{
				var Edges = new List<Rectangle>();
				if (BT.Left > 0)
				{
					Edges.Add(new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
				}

				if (BT.Right > 0)
				{
					Edges.Add(new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
				}

				if (BT.Top > 0)
				{
					Edges.Add(new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
				}

				if (BT.Bottom > 0)
				{
					Edges.Add(new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
				}

				if (Edges.Any())
				{
					var Scanlines = new List<Rectangle>();

					switch (ScanOrientation)
					{
						case Orientation.Horizontal:
						{
							var Left = Bounds.Left;
							var Right = Bounds.Right;

							var Width = Bounds.Width;
							var Height = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * Bounds.Height));

							var Center = !ScanIsReversed ? Bounds.Top + (int)(Progress * Bounds.Height) : Bounds.Bottom - ((int)(Progress * Bounds.Height));
							var Top = Center - Height / 2;
							var Bottom = Center + Height / 2;

							//  Handle cases where the scanline needs to loop around to opposite side
							if (Top < Bounds.Top)
							{
								var Overflow = Bounds.Top - Top;
								Scanlines.Add(new Rectangle(Left, Bounds.Top, Width, Height - Overflow));
								Scanlines.Add(new Rectangle(Left, Bounds.Bottom - Overflow, Width, Overflow));
							}
							else if (Bottom > Bounds.Bottom)
							{
								var Overflow = Bottom - Bounds.Bottom;
								Scanlines.Add(new Rectangle(Left, Bounds.Bottom - Height + Overflow, Width, Height - Overflow));
								Scanlines.Add(new Rectangle(Left, Bounds.Top, Width, Overflow));
							}
							else
							{
								Scanlines.Add(new Rectangle(Left, Top, Width, Height));
							}
						}
							break;
						case Orientation.Vertical:
						{
							var Top = Bounds.Top;
							var Bottom = Bounds.Bottom;

							var Width = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * Bounds.Width));
							var Height = Bounds.Height;

							var Center = !ScanIsReversed ? Bounds.Left + (int)(Progress * Bounds.Width) : Bounds.Right - ((int)(Progress * Bounds.Width));
							var Left = Center - Width / 2;
							var Right = Center + Width / 2;

							//  Handle cases where the scanline needs to loop around to opposite side
							if (Left < Bounds.Left)
							{
								var Overflow = Bounds.Left - Left;
								Scanlines.Add(new Rectangle(Bounds.Left, Top, Width - Overflow, Height));
								Scanlines.Add(new Rectangle(Bounds.Right - Overflow, Top, Overflow, Height));
							}
							else if (Right > Bounds.Right)
							{
								var Overflow = Right - Bounds.Right;
								Scanlines.Add(new Rectangle(Bounds.Right - Width + Overflow, Top, Width - Overflow, Height));
								Scanlines.Add(new Rectangle(Bounds.Left, Top, Overflow, Height));
							}
							else
							{
								Scanlines.Add(new Rectangle(Left, Top, Width, Height));
							}
						}
							break;
						default: throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {ScanOrientation}");
					}

					foreach (var Scanline in Scanlines)
					{
						foreach (var Edge in Edges)
						{
							var Intersection = Rectangle.Intersect(Scanline, Edge);
							if (!Intersection.IsEmpty)
							{
								HighlightFillBrush.Draw(DA, Element, Intersection);
							}
						}
					}
				}
			}
				break;
			default: throw new NotImplementedException($"Unrecognized {nameof(HighlightAnimation)}: {AnimationType}");
		}
	}

	public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
	{
		Underlay?.Draw(DA, Element, Shape, Geometry);

		if (!IsEnabled || HighlightColor == Color.Transparent)
		{
			return;
		}

		var progress = ActualAnimationProgress;

		if (AnimationType is HighlightAnimation.Progress or HighlightAnimation.Scan)
		{
			if (Geometry.UsesRectangleFastPath || !Geometry.HasBorderRingMesh)
			{
				//  Rectangle fast path, or the residual case where the border thickness consumes the whole box so there is no ring mesh
				//  to parameterize (Docs/drawing-architecture.md, Limites connues;
				//  MGBoxGeometryBuilder.BuildInnerContour).
				Draw(DA, Element, Shape.OuterBounds, Shape.NormalizedBorderThickness);
				return;
			}

			if (AnimationType == HighlightAnimation.Progress)
			{
				DrawProgressOnRing(DA, Geometry, progress);
			}
			else
			{
				DrawScanOnRing(DA, Geometry, progress);
			}
			return;
		}

		switch (AnimationType)
		{
			case HighlightAnimation.Pulse:
				var fadePercent = PulseFadeDuration / PulseCycleDuration;
				if (progress < fadePercent)
				{
					var opacityScalar = 1.0f - (float)(progress / fadePercent);
					HighlightBorderBrush.Draw(DA.SetOpacity(DA.Opacity * opacityScalar), Element, Shape, Geometry);
				}
				break;
			case HighlightAnimation.Flash:
				var isVisible = progress <= FlashShowDuration / FlashCycleDuration;
				if (isVisible)
				{
					HighlightBorderBrush.Draw(DA, Element, Shape, Geometry);
				}
				break;
		}
	}

	/// <summary>Progress on the rounded ring: parameterizes the highlighted span by cumulative arc length along <see cref="MGBoxGeometry.OuterContour"/>
	/// instead of the rectangular perimeter. Percentage 0 is anchored at the contour vertex that is top-most then left-most (the end of the
	/// top-left arc / start of the top edge - the rounded-corner equivalent of the rectangle path's top-left corner, since a zero radius collapses
	/// that arc to a single point at the same location), and the sweep runs along the top edge to the right first (clockwise on screen) for
	/// <see cref="HighlightFlowDirection.Clockwise"/>, honouring <see cref="ProgressFlowDirection"/> exactly as the rectangle path does with
	/// <c>IsReversed</c>. Ring quad indices <c>i, i+1</c> (outer) / <c>i, i+1</c> (inner) are walked in sweep order starting at the anchor; a quad
	/// fully inside the span is emitted whole, a quad straddling a span boundary is split by lerping both contours at the boundary's fractional
	/// position within that quad, and a span that wraps past the end of the contour is emitted as two sub-spans.</summary>
	private void DrawProgressOnRing(ElementDrawArgs DA, MGBoxGeometry Geometry, double progress)
	{
		var outer = Geometry.OuterContour;
		var inner = Geometry.InnerContour;
		var n = outer.Count;
		if (n < 2 || inner.Count != n)
		{
			return;
		}

		var anchorIndex = FindTopLeftAnchorIndex(outer);
		var isReversed = ProgressFlowDirection == HighlightFlowDirection.CounterClockwise;

		//  order[k] = contour index of the k-th vertex visited in sweep order, starting at the anchor.
		//  arcPos[k] = cumulative arc length (along the outer contour) from the anchor to order[k]; arcPos[n] closes the loop.
		var order = new int[n];
		var arcPos = new double[n + 1];
		for (var k = 0; k < n; k++)
		{
			order[k] = isReversed ? PositiveModulo(anchorIndex - k, n) : PositiveModulo(anchorIndex + k, n);
		}
		for (var k = 1; k <= n; k++)
		{
			arcPos[k] = arcPos[k - 1] + Vector2.Distance(outer[order[k - 1]], outer[order[k % n]]);
		}
		var total = arcPos[n];
		if (total <= 0.0)
		{
			return;
		}

		var startPercent = progress - ProgressSize / 2.0;
		var startArc = PositiveModulo(startPercent * total, total);
		var endArc = startArc + ProgressSize * total;

		if (endArc <= total)
		{
			EmitProgressSpan(startArc, endArc);
		}
		else
		{
			EmitProgressSpan(startArc, total);
			EmitProgressSpan(0.0, endArc - total);
		}

		void EmitProgressSpan(double rangeStart, double rangeEnd)
		{
			if (rangeEnd - rangeStart <= 1e-9)
			{
				return;
			}

			var origin = DA.Offset.ToVector2();
			for (var k = 0; k < n; k++)
			{
				var quadStart = arcPos[k];
				var quadEnd = arcPos[k + 1];
				var overlapStart = Math.Max(quadStart, rangeStart);
				var overlapEnd = Math.Min(quadEnd, rangeEnd);
				if (overlapEnd - overlapStart <= 1e-6)
				{
					continue;
				}

				var quadLength = quadEnd - quadStart;
				var tStart = quadLength > 1e-9 ? (overlapStart - quadStart) / quadLength : 0.0;
				var tEnd = quadLength > 1e-9 ? (overlapEnd - quadStart) / quadLength : 1.0;

				var i0 = order[k];
				var i1 = order[(k + 1) % n];
				var outerA = outer[i0];
				var outerB = outer[i1];
				var innerA = inner[i0];
				var innerB = inner[i1];

				//  Ring quad = outer at tStart, outer at tEnd, inner at tEnd, inner at tStart (BuildBorderRingIndices emits (o, o2, i2) then (i2, i, o)).
				var oStart = Vector2.Lerp(outerA, outerB, (float)tStart);
				var oEnd = Vector2.Lerp(outerA, outerB, (float)tEnd);
				var iStart = Vector2.Lerp(innerA, innerB, (float)tStart);
				var iEnd = Vector2.Lerp(innerA, innerB, (float)tEnd);

				DA.DT.FillTriangle(origin, oStart, HighlightColor, oEnd, HighlightColor, iEnd, HighlightColor);
				DA.DT.FillTriangle(origin, iEnd, HighlightColor, iStart, HighlightColor, oStart, HighlightColor);
			}
		}
	}

	/// <summary>The contour vertex that is top-most then left-most (ties broken by smallest X): the end of the top-left corner arc / start of
	/// the top edge, which collapses to the rectangle path's top-left corner when the radius is 0. This is the anchor for
	/// <see cref="DrawProgressOnRing"/>'s percent-0 position.</summary>
	private static int FindTopLeftAnchorIndex(IReadOnlyList<Vector2> outer)
	{
		var anchor = 0;
		for (var i = 1; i < outer.Count; i++)
		{
			var candidate = outer[i];
			var current = outer[anchor];
			if (candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))
			{
				anchor = i;
			}
		}
		return anchor;
	}

	/// <summary>Scan on the rounded ring: computes the band rectangle exactly as the rectangle path does, then clips every ring quad
	/// (<see cref="MGBoxGeometry.OuterContour"/> / <see cref="MGBoxGeometry.InnerContour"/>, matched by index) against that band with
	/// <see cref="MGConvexPolygonClipper.ClipToRectangle"/> and fills what remains, so the highlight never paints outside the ring.</summary>
	private void DrawScanOnRing(ElementDrawArgs DA, MGBoxGeometry Geometry, double progress)
	{
		var outer = Geometry.OuterContour;
		var inner = Geometry.InnerContour;
		var n = outer.Count;
		if (n < 2 || inner.Count != n)
		{
			return;
		}

		var bounds = Geometry.Shape.OuterBounds;
		List<Rectangle> scanlines = new();

		switch (ScanOrientation)
		{
			case Orientation.Horizontal:
			{
				var width = bounds.Width;
				var height = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * bounds.Height));
				var center = !ScanIsReversed ? bounds.Top + (int)(progress * bounds.Height) : bounds.Bottom - (int)(progress * bounds.Height);
				var top = center - height / 2;
				var bottom = top + height;

				if (top < bounds.Top)
				{
					var overflow = bounds.Top - top;
					scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width, height - overflow));
					scanlines.Add(new Rectangle(bounds.Left, bounds.Bottom - overflow, width, overflow));
				}
				else if (bottom > bounds.Bottom)
				{
					var overflow = bottom - bounds.Bottom;
					scanlines.Add(new Rectangle(bounds.Left, bounds.Bottom - height + overflow, width, height - overflow));
					scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width, overflow));
				}
				else
				{
					scanlines.Add(new Rectangle(bounds.Left, top, width, height));
				}
			}
				break;
			case Orientation.Vertical:
			{
				var height = bounds.Height;
				var width = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * bounds.Width));
				var center = !ScanIsReversed ? bounds.Left + (int)(progress * bounds.Width) : bounds.Right - (int)(progress * bounds.Width);
				var left = center - width / 2;
				var right = left + width;

				if (left < bounds.Left)
				{
					var overflow = bounds.Left - left;
					scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width - overflow, height));
					scanlines.Add(new Rectangle(bounds.Right - overflow, bounds.Top, overflow, height));
				}
				else if (right > bounds.Right)
				{
					var overflow = right - bounds.Right;
					scanlines.Add(new Rectangle(bounds.Right - width + overflow, bounds.Top, width - overflow, height));
					scanlines.Add(new Rectangle(bounds.Left, bounds.Top, overflow, height));
				}
				else
				{
					scanlines.Add(new Rectangle(left, bounds.Top, width, height));
				}
			}
				break;
			default: throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {ScanOrientation}");
		}

		var origin = DA.Offset.ToVector2();
		List<Vector2> quadPolygon = new(4);
		List<Vector2> clipped = new(8);
		List<Vector2> scratch = new(8);

		foreach (var scanline in scanlines)
		{
			for (var i = 0; i < n; i++)
			{
				var next = (i + 1) % n;

				//  Ring quad i = outer i, outer i+1, inner i+1, inner i (BuildBorderRingIndices emits (o, o2, i2) then (i2, i, o)).
				quadPolygon.Clear();
				quadPolygon.Add(outer[i]);
				quadPolygon.Add(outer[next]);
				quadPolygon.Add(inner[next]);
				quadPolygon.Add(inner[i]);

				MGConvexPolygonClipper.ClipToRectangle(quadPolygon, scanline, clipped, scratch);
				if (clipped.Count < 3)
				{
					continue;
				}

				for (var t = 1; t + 1 < clipped.Count; t++)
				{
					DA.DT.FillTriangle(origin, clipped[0], HighlightColor, clipped[t], HighlightColor, clipped[t + 1], HighlightColor);
				}
			}
		}
	}

	public IBorderBrush Copy()
	{
		var Copy = new MGHighlightBorderBrush(Underlay, HighlightColor, AnimationType, Target)
		{
			AnimationProgress = AnimationProgress,
			IsEnabled = IsEnabled,
			PulseFadeDuration = PulseFadeDuration,
			PulseDelay = PulseDelay,
			FlashShowDuration = FlashShowDuration,
			FlashHideDuration = FlashHideDuration,
			ProgressFlowDirection = ProgressFlowDirection,
			ProgressDuration = ProgressDuration,
			ProgressSize = ProgressSize,
			ScanOrientation = ScanOrientation,
			ScanIsReversed = ScanIsReversed,
			ScanDuration = ScanDuration,
			ScanSize = ScanSize,
			StopOnMouseOver = StopOnMouseOver,
			StopOnClick = StopOnClick
		};
		return Copy;
	}
}