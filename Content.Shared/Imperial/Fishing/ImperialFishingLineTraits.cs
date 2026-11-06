namespace Content.Shared.Imperial.Fishing;

/// <summary>fishing_line_traits.</summary>
[Flags]
public enum ImperialFishingLineTraits
{
    None = 0,
    Cloaked = 1 << 0,
    Bouncy = 1 << 1,
    Stiff = 1 << 2,
    Autoreel = 1 << 3,
    Phase = 1 << 4,
}
