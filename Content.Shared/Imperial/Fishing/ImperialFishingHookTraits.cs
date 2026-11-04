namespace Content.Shared.Imperial.Fishing;

/// <summary>fishing_hook_traits.</summary>
[Flags]
public enum ImperialFishingHookTraits
{
    None = 0,
    Weighted = 1 << 0,
    Bidirectional = 1 << 1,
    NoEscape = 1 << 2,
    Ensnare = 1 << 3,
    Kill = 1 << 4,
}
