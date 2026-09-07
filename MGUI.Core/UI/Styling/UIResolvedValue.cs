namespace MGUI.Core.UI.Styling
{
    public readonly record struct UIResolvedValue<T>(T Value, UIValueResolutionSource Source, bool IsSet = true)
    {
        public bool IsAnimated => Source.Kind == UIValueSourceKind.Animation;

        public bool IsLocal => Source.IsLocal;

        /// <summary>
        /// True when the source's invalidation carries ALL of the given flags (an exact-or-superset match).
        /// Use this to test for a specific combined invalidation requirement.
        /// </summary>
        public bool HasInvalidation(UIInvalidationKind invalidation)
            => (Source.Invalidation & invalidation) == invalidation;

        /// <summary>
        /// True when the source's invalidation carries AT LEAST ONE of the given flags. Use this to test
        /// "does this value's invalidation overlap with any of these kinds" (the check the template guard
        /// and <c>MGElement.InvalidateTemplateValue</c> actually need, as opposed to <see cref="HasInvalidation"/>'s
        /// all-flags match).
        /// </summary>
        public bool HasAnyInvalidation(UIInvalidationKind invalidation)
            => (Source.Invalidation & invalidation) != UIInvalidationKind.None;

        public static UIResolvedValue<T> Unset(UIInvalidationKind invalidation = UIInvalidationKind.None)
            => new(default, UIValueResolutionSource.Default(invalidation), false);
    }
}