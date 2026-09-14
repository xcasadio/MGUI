using System.Globalization;

namespace MGUI.Core.UI.Animation.Easing;

/// <summary>
/// A CSS <c>cubic-bezier(x1,y1,x2,y2)</c> easing: the two control points of a cubic Bezier curve whose endpoints are pinned
/// at (0,0) and (1,1). <see cref="X1"/> and <see cref="X2"/> must stay in [0,1] so that x(u) is monotonic and the curve
/// answers exactly one y per x (a CSS requirement); <see cref="Y1"/> and <see cref="Y2"/> are free, so the curve may
/// overshoot or undershoot [0,1] like <see cref="UIEasing.BackOut"/> (ADR-0008, decision 1).<para/>
/// Accepted text forms (<see cref="TryParse"/> / <see cref="Parse"/>): the canonical CSS form <c>cubic-bezier(x1,y1,x2,y2)</c>
/// (case-insensitive prefix) and the shorter <c>bezier:x1,y1,x2,y2</c>; both tolerate surrounding whitespace around each number.
/// <see cref="ToString"/> always renders the canonical CSS form.<para/>
/// Stateless and allocation-free after construction: <see cref="Ease"/> only reads the precomputed polynomial coefficients.
/// </summary>
public sealed class UICubicBezierEasing : IUIEasingFunction
{
    private const int NewtonIterations = 8;
    private const int BisectionIterations = 32;
    private const float Epsilon = 1e-6f;

    // Bezier(u) = A*u^3 + B*u^2 + C*u, with the endpoints pinned at 0 and 1 (see https://www.w3.org/TR/css-easing-1/#cubic-bezier-algo).
    private readonly float _ax;
    private readonly float _bx;
    private readonly float _cx;
    private readonly float _ay;
    private readonly float _by;
    private readonly float _cy;

    public float X1 { get; }
    public float Y1 { get; }
    public float X2 { get; }
    public float Y2 { get; }

    public UICubicBezierEasing(float x1, float y1, float x2, float y2)
    {
        RequireUnitInterval(x1, nameof(x1));
        RequireFinite(y1, nameof(y1));
        RequireUnitInterval(x2, nameof(x2));
        RequireFinite(y2, nameof(y2));

        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;

        _cx = 3f * x1;
        _bx = 3f * (x2 - x1) - _cx;
        _ax = 1f - _cx - _bx;

        _cy = 3f * y1;
        _by = 3f * (y2 - y1) - _cy;
        _ay = 1f - _cy - _by;
    }

    /// <summary>Maps the linear progress <paramref name="t"/> to the eased progress: solves x(u) = t for u, then returns y(u).</summary>
    public float Ease(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        return SampleCurveY(SolveCurveX(t));
    }

    private float SampleCurveX(float u) => ((_ax * u + _bx) * u + _cx) * u;
    private float SampleCurveY(float u) => ((_ay * u + _by) * u + _cy) * u;
    private float SampleCurveDerivativeX(float u) => (3f * _ax * u + 2f * _bx) * u + _cx;

    private float SolveCurveX(float t)
    {
        // Newton-Raphson first: fast, and exact for the vast majority of curves.
        float u = t;
        for (int i = 0; i < NewtonIterations; i++)
        {
            float x = SampleCurveX(u) - t;
            if (MathF.Abs(x) < Epsilon)
            {
                return u;
            }

            float derivative = SampleCurveDerivativeX(u);
            if (MathF.Abs(derivative) < Epsilon)
            {
                break;
            }

            u -= x / derivative;
        }

        // Bisection fallback: a flat derivative (near-vertical control polygon) makes Newton unreliable, never x(u) itself.
        float lo = 0f;
        float hi = 1f;
        u = t;
        for (int i = 0; i < BisectionIterations; i++)
        {
            float x = SampleCurveX(u);
            if (MathF.Abs(x - t) < Epsilon)
            {
                return u;
            }

            if (x < t)
            {
                lo = u;
            }
            else
            {
                hi = u;
            }

            u = (lo + hi) / 2f;
        }

        return u;
    }

    /// <summary>Renders the canonical CSS form, e.g. <c>cubic-bezier(0.42,0,1,1)</c>, invariant and space-free.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"cubic-bezier({Format(X1)},{Format(Y1)},{Format(X2)},{Format(Y2)})");

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses <c>cubic-bezier(x1,y1,x2,y2)</c> or <c>bezier:x1,y1,x2,y2</c> (case-insensitive prefix, whitespace tolerated
    /// around each number). Returns false, never throws, on null, empty, a wrong number count, non-numeric text, an
    /// unbalanced <c>cubic-bezier(...)</c>, <c>x1</c> / <c>x2</c> outside [0,1], or a non-finite <c>y1</c> / <c>y2</c>
    /// (<c>NaN</c> or <c>Infinity</c>).
    /// </summary>
    public static bool TryParse(string text, out UICubicBezierEasing easing)
    {
        easing = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        string body;

        if (trimmed.StartsWith("cubic-bezier", StringComparison.OrdinalIgnoreCase))
        {
            string rest = trimmed.Substring("cubic-bezier".Length).TrimStart();
            if (rest.Length < 2 || rest[0] != '(' || rest[rest.Length - 1] != ')')
            {
                return false;
            }

            body = rest.Substring(1, rest.Length - 2);
        }
        else if (trimmed.StartsWith("bezier:", StringComparison.OrdinalIgnoreCase))
        {
            body = trimmed.Substring("bezier:".Length);
        }
        else
        {
            return false;
        }

        string[] parts = body.Split(',');
        if (parts.Length != 4)
        {
            return false;
        }

        Span<float> values = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        if (!IsUnitInterval(values[0]) || !IsUnitInterval(values[2]))
        {
            return false;
        }

        if (!float.IsFinite(values[1]) || !float.IsFinite(values[3]))
        {
            return false;
        }

        easing = new UICubicBezierEasing(values[0], values[1], values[2], values[3]);
        return true;
    }

    /// <summary>Same as <see cref="TryParse"/>, throwing <see cref="FormatException"/> instead of returning false.</summary>
    public static UICubicBezierEasing Parse(string text)
    {
        if (!TryParse(text, out var easing))
        {
            throw new FormatException($"Cannot parse '{text}' as a Bezier easing: use 'cubic-bezier(x1,y1,x2,y2)' or 'bezier:x1,y1,x2,y2' with x1 and x2 in [0,1].");
        }

        return easing;
    }

    private static bool IsUnitInterval(float value) => float.IsFinite(value) && value >= 0f && value <= 1f;

    private static void RequireUnitInterval(float value, string paramName)
    {
        if (!IsUnitInterval(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Must be a finite number in [0, 1].");
        }
    }

    private static void RequireFinite(float value, string paramName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Must be a finite number.");
        }
    }
}
