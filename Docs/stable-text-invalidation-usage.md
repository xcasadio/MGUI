# Stable text invalidation usage

`MGTextBlock` supports three text invalidation levels through `MGTextInvalidationMode`:

- `RelayoutParent`: safe default. Use for ordinary labels, wrapping text, localized text, first layout, unknown width, font/theme/padding changes, or any update that may change desired size.
- `ReflowLocal`: reparses local lines against the current layout bounds without requesting parent layout unless the desired size comparison detects a real size change.
- `ContentOnly`: for updates whose rendered footprint is known to be unchanged. The framework still keeps rendering data current and escalates safely if the stable-footprint contract is not satisfied.

New realtime labels should opt in explicitly:

```csharp
MGTextBlock fpsText = new(window, "FPS: 000")
{
    HasStableTextFootprint = true,
    MinLines = 1
};

fpsText.SetText($"FPS: {fps:000}", MGTextInvalidationMode.ReflowLocal);
```

Reserve the footprint with `MinLines`, `MaxLines`, `PreferredWidth`, a fixed host layout, or naturally fixed-width formatting. If the desired size still changes, `MGTextBlock` escalates to parent relayout and clears its measurement cache.

Do not use stable text invalidation for general prose, translated labels, freely wrapping descriptions, dynamic font/theme changes, or text blocks that have not completed layout. The `Text` property keeps the safe relayout default, and the legacy bool overload remains only for compatibility with older call sites.