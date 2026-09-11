namespace MGUI.Core.UI.Styling
{
    /// <summary>
    /// The boxed, type-erased diagnostic view of one contribution recorded in a <see cref="UIResolvedPropertyStore"/>
    /// entry (ADR-0005). Produced by <see cref="UIResolvedPropertyStore.EnumerateContributions"/> for tooling that
    /// cannot know the entry's <c>T</c> ahead of time; production code that already knows <c>T</c> should keep using
    /// the typed <see cref="UIResolvedValue{T}"/> reads instead.
    /// </summary>
    public readonly record struct UIResolvedContribution(UIValueSourceKind Kind, UIValueResolutionSource Source, object Value);
}
