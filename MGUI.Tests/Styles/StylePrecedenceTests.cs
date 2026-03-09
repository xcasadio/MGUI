using System.Reflection;

namespace MGUI.Tests.Styles;

/// <summary>
/// Unit tests for the XAML style-precedence mechanism introduced in Task 2a/2b
/// (pure logic, no MonoGame runtime required).
/// </summary>
public class StylePrecedenceTests
{
    // ── Stub replicating ExplicitlySetProperties + IsXAMLPropertyUnset logic ────

    /// <summary>
    /// Minimal host that replicates:<br/>
    ///   • <c>ExplicitlySetProperties</c> tracking via property setters<br/>
    ///   • The <c>IsXAMLPropertyUnset(PropertyInfo)</c> helper
    /// </summary>
    private sealed class StyleableStub
    {
        // Non-nullable value-type property — must use full setter to track.
        private bool _boolValue;
        public bool BoolValue
        {
            get => _boolValue;
            set { _boolValue = value; ExplicitlySetProperties.Add(nameof(BoolValue)); }
        }

        private int _intValue;
        public int IntValue
        {
            get => _intValue;
            set { _intValue = value; ExplicitlySetProperties.Add(nameof(IntValue)); }
        }

        // Nullable value-type property — null check is sufficient, no tracking needed.
        public int? NullableInt { get; set; }

        // Reference-type property — null check is sufficient.
        public string RefValue { get; set; }

        internal HashSet<string> ExplicitlySetProperties { get; } = new();

        /// <summary>Direct port of <c>Element.IsXAMLPropertyUnset</c>.</summary>
        public bool IsXAMLPropertyUnset(PropertyInfo pi)
        {
            if (ExplicitlySetProperties.Contains(pi.Name))
                return false;

            Type type = pi.PropertyType;
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                return true;

            return pi.GetValue(this) == null;
        }

        private PropertyInfo PI(string name) =>
            GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;

        public bool BoolValueIsUnset    => IsXAMLPropertyUnset(PI(nameof(BoolValue)));
        public bool IntValueIsUnset     => IsXAMLPropertyUnset(PI(nameof(IntValue)));
        public bool NullableIntIsUnset  => IsXAMLPropertyUnset(PI(nameof(NullableInt)));
        public bool RefValueIsUnset     => IsXAMLPropertyUnset(PI(nameof(RefValue)));
    }

    // ── ExplicitlySetProperties population ───────────────────────────────────────

    [Fact]
    public void Setter_Called_AddsToExplicitlySetProperties()
    {
        var stub = new StyleableStub();
        stub.BoolValue = true;
        Assert.Contains(nameof(StyleableStub.BoolValue), stub.ExplicitlySetProperties);
    }

    [Fact]
    public void Setter_NotCalled_AbsentFromExplicitlySetProperties()
    {
        var stub = new StyleableStub();
        Assert.DoesNotContain(nameof(StyleableStub.BoolValue), stub.ExplicitlySetProperties);
    }

    [Fact]
    public void Setter_CalledWithDefaultValue_StillTracked()
    {
        // User explicitly wrote Value="false" in XAML — even though false is the C# default,
        // the property is explicitly set and must block style override.
        var stub = new StyleableStub();
        stub.BoolValue = false;
        Assert.Contains(nameof(StyleableStub.BoolValue), stub.ExplicitlySetProperties);
    }

    // ── IsXAMLPropertyUnset — non-nullable value type ─────────────────────────────

    [Fact]
    public void NonNullableValueType_NeverSet_IsUnset()
    {
        var stub = new StyleableStub();
        Assert.True(stub.BoolValueIsUnset,  "bool property that was never set should be treated as unset");
        Assert.True(stub.IntValueIsUnset,   "int property that was never set should be treated as unset");
    }

    [Fact]
    public void NonNullableValueType_SetToTrue_IsNotUnset()
    {
        var stub = new StyleableStub { BoolValue = true };
        Assert.False(stub.BoolValueIsUnset);
    }

    [Fact]
    public void NonNullableValueType_SetToFalse_IsNotUnset()
    {
        // Key regression test: user explicitly set Value=false.
        // The OLD code used == default(object) which returned (object)false == null → false,
        // incorrectly treating the property as "has a value" and blocking style.
        // The NEW code checks ExplicitlySetProperties — since setter was called, it is NOT unset.
        var stub = new StyleableStub { BoolValue = false };
        Assert.False(stub.BoolValueIsUnset,
            "bool explicitly set to false must NOT be treated as unset (style must not override)");
    }

    [Fact]
    public void NonNullableValueType_SetToNonDefaultInt_IsNotUnset()
    {
        var stub = new StyleableStub { IntValue = 42 };
        Assert.False(stub.IntValueIsUnset);
    }

    [Fact]
    public void NonNullableValueType_SetToZero_IsNotUnset()
    {
        // int explicitly set to 0 (the C# default) must still be considered explicitly set.
        var stub = new StyleableStub { IntValue = 0 };
        Assert.False(stub.IntValueIsUnset,
            "int explicitly set to 0 must NOT be unset — style must not override it");
    }

    // ── IsXAMLPropertyUnset — nullable value type  ───────────────────────────────

    [Fact]
    public void NullableValueType_NullValue_IsUnset()
    {
        var stub = new StyleableStub { NullableInt = null };
        Assert.True(stub.NullableIntIsUnset);
    }

    [Fact]
    public void NullableValueType_NonNullValue_IsNotUnset()
    {
        var stub = new StyleableStub { NullableInt = 7 };
        Assert.False(stub.NullableIntIsUnset);
    }

    // ── IsXAMLPropertyUnset — reference type ─────────────────────────────────────

    [Fact]
    public void ReferenceType_NullValue_IsUnset()
    {
        var stub = new StyleableStub { RefValue = null };
        Assert.True(stub.RefValueIsUnset);
    }

    [Fact]
    public void ReferenceType_NonNullValue_IsNotUnset()
    {
        var stub = new StyleableStub { RefValue = "hello" };
        Assert.False(stub.RefValueIsUnset);
    }
}
