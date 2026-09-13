using System.Diagnostics;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>A brush that directs the user's focus to a particular region either by brightening the focused region or by dimming the unfocused region.<para/>
/// This brush is commonly used to apply a 35% transparent black color overlay to an entire window except for a particular rectangular region,
/// so that the uncolored region stands out visually.</summary>
public class MGHighlightFillBrush : XAMLBindableBase, IFillBrush, 
	IElementNameResolver, IResourcesResolver, IDesktopResolver
{
	/// <summary>Only used for databindings purposes in XAML. Do not modify.</summary>
	internal MGElement SourceElement { get; set; }

	public bool TryGetElementByName(string Name, out MGElement NamedElement)
	{
		if (SourceElement != null)
		{
			return SourceElement.TryGetElementByName(Name, out NamedElement);
		}
		else
		{
			NamedElement = null;
			return false;
		}
	}
	public MGResources GetResources() => SourceElement?.GetResources();
	public MGDesktop GetDesktop() => SourceElement?.GetDesktop();

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _IsEnabled;
	/// <summary>If <see langword="false"/>, this brush will not be drawn.<para/>Default value: <see langword="true"/></summary>
	public bool IsEnabled
	{
		get => _IsEnabled;
		set
		{
			if (_IsEnabled != value)
			{
				_IsEnabled = value;
				NotifyPropertyChanged(nameof(IsEnabled));
			}
		}
	}

	#region Graphics
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _FillFocusedRegion;
	/// <summary>If <see langword="true"/>, the <see cref="FocusedColor"/> will be drawn overtop of the focused region.<para/>
	/// Default value: <see langword="false"/></summary>
	public bool FillFocusedRegion
	{
		get => _FillFocusedRegion;
		set
		{
			if (_FillFocusedRegion != value)
			{
				_FillFocusedRegion = value;
				NotifyPropertyChanged(nameof(FillFocusedRegion));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private Color? _FocusedColor;
	/// <summary>Only used if <see cref="FillFocusedRegion"/> is <see langword="true"/>.<para/>
	/// The color to draw overtop of the focused region. This color should be translucent or else the focused region's content won't be visible.<para/>
	/// Default value: 35% transparent White</summary>
	public Color? FocusedColor
	{
		get => _FocusedColor;
		set
		{
			if (_FocusedColor != value)
			{
				_FocusedColor = value;
				NotifyPropertyChanged(nameof(FocusedColor));
			}
		}
	}

	private bool CanFillFocusedRegion => FillFocusedRegion && FocusedColor.HasValue && FocusedColor.Value != Color.Transparent;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool _FillUnfocusedRegion;
	/// <summary>If <see langword="true"/>, the <see cref="UnfocusedColor"/> will be drawn overtop of the unfocused region.<para/>
	/// Default value: <see langword="true"/></summary>
	public bool FillUnfocusedRegion
	{
		get => _FillUnfocusedRegion;
		set
		{
			if (_FillUnfocusedRegion != value)
			{
				_FillUnfocusedRegion = value;
				NotifyPropertyChanged(nameof(FillUnfocusedRegion));
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private Color? _UnfocusedColor;
	/// <summary>Only used if <see cref="FillUnfocusedRegion"/> is <see langword="true"/>.<para/>
	/// The color to draw overtop of the unfocused region. Recommended to use a translucent color or else the unfocused region's content won't be visible.<para/>
	/// Default value: 35% transparent Black</summary>
	public Color? UnfocusedColor
	{
		get => _UnfocusedColor;
		set
		{
			if (_UnfocusedColor != value)
			{
				_UnfocusedColor = value;
				NotifyPropertyChanged(nameof(UnfocusedColor));
			}
		}
	}

	private bool CanFillUnfocusedRegion => FillUnfocusedRegion && UnfocusedColor.HasValue && UnfocusedColor.Value != Color.Transparent;
	#endregion Graphics

	#region Bounds
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private IReadOnlyList<Rectangle> _FocusedBounds;
	/// <summary>The rectangular region(s) that are in focus.<para/>
	/// These regions will be filled with <see cref="FocusedColor"/> if <see cref="FillFocusedRegion"/> is <see langword="true"/><br/>
	/// All other areas will be filled with <see cref="UnfocusedColor"/> if <see cref="FillUnfocusedRegion"/> is <see langword="true"/>
	/// Note: The focused region is calculated by unioning <see cref="FocusedBounds"/> and the bounds of the <see cref="FocusedElements"/>, but usually only 1 or the other should be defined.<br/>
	/// See also: <see cref="FocusedElements"/></summary>
	public IReadOnlyList<Rectangle> FocusedBounds
	{
		get => _FocusedBounds;
		set
		{
			if (_FocusedBounds != value)
			{
				_FocusedBounds = value;
				NotifyPropertyChanged(nameof(FocusedBounds));
				CachedUnfocusedRegions.Clear();
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private IReadOnlyList<MGElement> _FocusedElements;
	/// <summary>The element(s) that are in focus.<para/>
	/// The screen-space bounds of these elements will be filled with <see cref="FocusedColor"/> if <see cref="FillFocusedRegion"/> is <see langword="true"/><br/>
	/// All other areas will be filled with <see cref="UnfocusedColor"/> if <see cref="FillUnfocusedRegion"/> is <see langword="true"/><para/>
	/// Note: The focused region is calculated by unioning <see cref="FocusedBounds"/> and the bounds of the <see cref="FocusedElements"/>, but usually only 1 or the other should be defined.<br/>
	/// See also: <see cref="FocusedBounds"/></summary>
	public IReadOnlyList<MGElement> FocusedElements
	{
		get => _FocusedElements;
		set
		{
			if (_FocusedElements != value)
			{
				void HandleLayoutBoundsChanged(object sender, EventArgs<Rectangle> e) => CachedUnfocusedRegions.Clear();

				if (FocusedElements != null)
				{
					foreach (var Element in FocusedElements)
					{
						Element.OnLayoutBoundsChanged -= HandleLayoutBoundsChanged;
					}
				}

				_FocusedElements = value;
				NotifyPropertyChanged(nameof(FocusedElements));
				NotifyPropertyChanged(nameof(FocusedElement));

				if (FocusedElements != null)
				{
					foreach (var Element in FocusedElements)
					{
						Element.OnLayoutBoundsChanged += HandleLayoutBoundsChanged;
					}
				}

				CachedUnfocusedRegions.Clear();
			}
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private int _FocusedElementPadding;
	/// <summary>Additional space to be added to the bounds of the <see cref="FocusedElements"/> when calculating the focused region.<para/>
	/// A positive value will expand the focused region.</summary>
	public int FocusedElementPadding
	{
		get => _FocusedElementPadding;
		set
		{
			if (_FocusedElementPadding != value)
			{
				_FocusedElementPadding = value;
				NotifyPropertyChanged(nameof(FocusedElementPadding));
				CachedUnfocusedRegions.Clear();
			}
		}
	}

	/// <summary>Convenience property that just sets <see cref="FocusedElements"/> to a list with 1 item.</summary>
	public MGElement FocusedElement
	{
		get => FocusedElements?.FirstOrDefault();
		set => FocusedElements = value == null ? new List<MGElement>() : new List<MGElement>() { value };
	}

	private readonly List<(Rectangle, List<Rectangle>)> CachedUnfocusedRegions = new List<(Rectangle, List<Rectangle>)>();
	private const int MaxCachedUnfocusedRegions = 20;
	#endregion Bounds

	/// <param name="FocusedOverlayColor">If <see langword="null"/>, uses default value of 35% transparent White.</param>
	/// <param name="UnfocusedOverlayColor">If <see langword="null"/>, uses default value of 35% transparent Black.</param>
	public MGHighlightFillBrush(bool FillFocusedRegion = false, Color? FocusedOverlayColor = null, bool FillUnfocusedRegion = true, Color? UnfocusedOverlayColor = null)
	{
		IsEnabled = true;

		this.FillFocusedRegion = FillFocusedRegion;
		FocusedColor = FocusedOverlayColor ?? Color.White * 0.35f;
		this.FillUnfocusedRegion = FillUnfocusedRegion;
		UnfocusedColor = UnfocusedOverlayColor ?? Color.Black * 0.35f;

		FocusedBounds = null;
		FocusedElements = null;
		FocusedElementPadding = 0;
	}

	public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
	{
		if (!IsEnabled || (!CanFillFocusedRegion && !CanFillUnfocusedRegion))
		{
			return;
		}

		var Offset = DA.Offset.ToVector2();
		var FocusedRects = GetFocusedRects();

		if (CanFillFocusedRegion)
		{
			//Limitation: If any of the focused rectangles overlap, the FocusedColor will be drawn with overlaps as well,
			//so the focused region wouldn't all be the same color overlay
			//(since drawing a transparent color overtop of the same spot twice in a row results in a different color)
			//We could union the rectangles that intersect one another, then decompose the polygon into a set of non-overlapping rectangles, but I'm too lazy to come up with that algorithm

			foreach (var r in FocusedRects)
			{
				DA.DT.FillRectangle(Offset, r, FocusedColor.Value);
			}
		}

		if (CanFillUnfocusedRegion)
		{
			var UnfocusedRegion = GetOrComputeUnfocusedRegion(Bounds, FocusedRects);
			if (UnfocusedRegion.Any())
			{
				foreach (var r in UnfocusedRegion)
				{
					DA.DT.FillRectangle(Offset, r, UnfocusedColor.Value);
				}
			}
		}
	}

	public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
	{
		if (!IsEnabled || (!CanFillFocusedRegion && !CanFillUnfocusedRegion))
		{
			return;
		}

		if (Geometry.UsesRectangleFastPath)
		{
			Draw(DA, Element, Shape.OuterBounds);
			return;
		}

		var Offset = DA.Offset.ToVector2();
		var OuterContour = Geometry.OuterContour;
		var FocusedRects = GetFocusedRects();

		List<Vector2> QuadPolygon = new(4);
		List<Vector2> Clipped = new(8);
		List<Vector2> Scratch = new(8);

		if (CanFillFocusedRegion)
		{
			foreach (var r in FocusedRects)
			{
				FillClippedRectangle(DA.DT, Offset, r, OuterContour, FocusedColor.Value, QuadPolygon, Clipped, Scratch);
			}
		}

		if (CanFillUnfocusedRegion)
		{
			var UnfocusedRegion = GetOrComputeUnfocusedRegion(Shape.OuterBounds, FocusedRects);
			foreach (var r in UnfocusedRegion)
			{
				FillClippedRectangle(DA.DT, Offset, r, OuterContour, UnfocusedColor.Value, QuadPolygon, Clipped, Scratch);
			}
		}
	}

	/// <summary>Unions <see cref="FocusedBounds"/> and the padded bounds of <see cref="FocusedElements"/> into a single list, exactly the set of
	/// rectangles that are filled with <see cref="FocusedColor"/> and subtracted out of the unfocused region on both the rectangle and the
	/// shape-aware draw path.</summary>
	private List<Rectangle> GetFocusedRects()
	{
		List<Rectangle> Result = new();

		if (FocusedBounds != null)
		{
			Result.AddRange(FocusedBounds);
		}

		if (FocusedElements != null)
		{
			Result.AddRange(FocusedElements.Select(x => x.LayoutBounds.GetExpanded(FocusedElementPadding)));
		}

		return Result;
	}

	/// <summary>Looks up <see cref="CachedUnfocusedRegions"/> for a previously-computed unfocused region for <paramref name="Bounds"/>, or computes
	/// and caches one by subtracting <paramref name="FocusedRects"/> from it. Shared by the rectangle and the shape-aware draw path so a rounded
	/// element doesn't invalidate the rectangle path's cache (and vice versa) - both key off <paramref name="Bounds"/>.</summary>
	private List<Rectangle> GetOrComputeUnfocusedRegion(Rectangle Bounds, IList<Rectangle> FocusedRects)
	{
		foreach (var Item in CachedUnfocusedRegions)
		{
			if (Item.Item1 == Bounds)
			{
				return Item.Item2;
			}
		}

		var UnfocusedRegion = Subtract(Bounds, FocusedRects);

		CachedUnfocusedRegions.Add((Bounds, UnfocusedRegion));
		if (CachedUnfocusedRegions.Count > MaxCachedUnfocusedRegions)
		{
			CachedUnfocusedRegions.RemoveAt(0);
		}

		return UnfocusedRegion;
	}

	/// <summary>Clips the axis-aligned <paramref name="rect"/> against the convex <paramref name="outerContour"/> (Sutherland-Hodgman) and fan-fills
	/// the surviving piece, so a region rectangle never paints outside a rounded silhouette. No-op when the piece is fully clipped away. A rectangle
	/// entirely inside <paramref name="outerContour"/> survives unclipped, in the same TopLeft/TopRight/BottomRight/BottomLeft vertex order it was
	/// built in.</summary>
	private static void FillClippedRectangle(IUIDrawContext DT, Vector2 origin, Rectangle rect, IReadOnlyList<Vector2> outerContour, Color color,
		List<Vector2> quadPolygon, List<Vector2> clipped, List<Vector2> scratch)
	{
		quadPolygon.Clear();
		quadPolygon.Add(new Vector2(rect.Left, rect.Top));
		quadPolygon.Add(new Vector2(rect.Right, rect.Top));
		quadPolygon.Add(new Vector2(rect.Right, rect.Bottom));
		quadPolygon.Add(new Vector2(rect.Left, rect.Bottom));

		MGConvexPolygonClipper.ClipToConvexPolygon(quadPolygon, outerContour, clipped, scratch);
		if (clipped.Count < 3)
		{
			return;
		}

		for (var i = 1; i + 1 < clipped.Count; i++)
		{
			DT.FillTriangle(origin, clipped[0], color, clipped[i], color, clipped[i + 1], color);
		}
	}

	public IFillBrush Copy()
	{
		MGHighlightFillBrush Copy = new(FillFocusedRegion, FocusedColor, FillUnfocusedRegion, UnfocusedColor)
		{
			IsEnabled = IsEnabled,
			FocusedColor = FocusedColor,
			UnfocusedColor = UnfocusedColor,
			FocusedBounds = FocusedBounds?.ToList(),
			FocusedElements = FocusedElements?.ToList(),
			FocusedElementPadding = FocusedElementPadding
		};

		return Copy;
	}

	private static List<Rectangle> Subtract(Rectangle Bounds, IList<Rectangle> Subtractions)
	{
		//  Keep dividing the result set by subtracting out the next rectangle in the subtractions list until all subtractions have been applied
		//  Yes, this is inefficient but who cares, the subtractions list will probably always be very small
		var Result = new List<Rectangle>() { Bounds };
		var RemainingSubtractions = Subtractions.ToList();
		while (RemainingSubtractions.Any())
		{
			var CurrentSubtraction = RemainingSubtractions[0];
			RemainingSubtractions.RemoveAt(0);

			List<Rectangle> Tmp = new();
			foreach (var Rect in Result)
			{
				Tmp.AddRange(Subtract(Rect, CurrentSubtraction));
			}

			Result = Tmp;
		}

		return Result;
	}

	//Taken from: https://stackoverflow.com/a/70136839/11689514
	//-------------------------
	//|          A            |
	//|-----------------------|
	//|  B  |   hole    |  C  |
	//|-----------------------|
	//|          D            |
	//-------------------------
	private static IEnumerable<Rectangle> Subtract(Rectangle minuend, Rectangle subtrahend)
	{
		subtrahend = Rectangle.Intersect(minuend, subtrahend);
		if ((subtrahend.Width | subtrahend.Height) == 0)
		{
			yield return minuend;
			yield break;
		}

		var heightA = subtrahend.Top - minuend.Top;
		if (heightA > 0)
		{
			yield return new Rectangle(minuend.Left, minuend.Top, minuend.Width, heightA);
		}

		var widthB = subtrahend.Left - minuend.Left;
		if (widthB > 0)
		{
			yield return new Rectangle(minuend.Left, subtrahend.Top, widthB, subtrahend.Height);
		}

		var widthC = minuend.Right - subtrahend.Right;
		if (widthC > 0)
		{
			yield return new Rectangle(subtrahend.Right, subtrahend.Top, widthC, subtrahend.Height);
		}

		var heightD = minuend.Bottom - subtrahend.Bottom;
		if (heightD > 0)
		{
			yield return new Rectangle(minuend.Left, subtrahend.Bottom, minuend.Width, heightD);
		}
	}
}