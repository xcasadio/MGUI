using MGUI.Core.UI.Styling;

namespace MGUI.Tests.Architecture;

public class StyleValueResolutionModelTests
{
    [Fact]
    public void Precedence_Order_Is_Stable()
    {
        Assert.True(UIValuePrecedence.Animation > UIValuePrecedence.LocalValue);
        Assert.True(UIValuePrecedence.LocalValue > UIValuePrecedence.Template);
        Assert.True(UIValuePrecedence.Template > UIValuePrecedence.ExplicitStyle);
        Assert.True(UIValuePrecedence.ExplicitStyle > UIValuePrecedence.ImplicitStyle);
        Assert.True(UIValuePrecedence.ImplicitStyle > UIValuePrecedence.DynamicResource);
        Assert.True(UIValuePrecedence.DynamicResource > UIValuePrecedence.Theme);
        Assert.True(UIValuePrecedence.Theme > UIValuePrecedence.Inherited);
        Assert.True(UIValuePrecedence.Inherited > UIValuePrecedence.DefaultValue);
    }

    [Fact]
    public void Factory_Creates_Expected_Local_Source()
    {
        UIValueResolutionSource source = UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw | UIInvalidationKind.Measure, "Width");

        Assert.Equal(UIValueSourceKind.LocalValue, source.Kind);
        Assert.Equal(UIValuePrecedence.LocalValue, source.Precedence);
        Assert.True(source.IsLocal);
        Assert.Equal("Width", source.Name);
    }

    [Fact]
    public void Resolved_Value_Preserves_Source_Metadata()
    {
        UIResolvedValue<int> resolved = new(42, UIValueResolutionSource.ExplicitStyle(UIInvalidationKind.Draw, "AccentWidth"));

        Assert.True(resolved.IsSet);
        Assert.Equal(42, resolved.Value);
        Assert.True(resolved.HasInvalidation(UIInvalidationKind.Draw));
        Assert.False(resolved.HasInvalidation(UIInvalidationKind.Measure));
        Assert.Equal(UIValueSourceKind.ExplicitStyle, resolved.Source.Kind);
    }

    [Fact]
    public void Unset_Value_Is_Not_Set()
    {
        UIResolvedValue<string> resolved = UIResolvedValue<string>.Unset(UIInvalidationKind.Draw);

        Assert.False(resolved.IsSet);
        Assert.Equal(UIValueSourceKind.DefaultValue, resolved.Source.Kind);
        Assert.True(resolved.HasInvalidation(UIInvalidationKind.Draw));
    }
}