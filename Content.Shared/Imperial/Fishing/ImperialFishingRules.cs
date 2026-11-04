namespace Content.Shared.Imperial.Fishing;

/// <summary>FISHING_MINIGAME_RULE_*.</summary>
[Flags]
public enum ImperialFishingRules
{
    None = 0,
    Bidirectional = 1 << 0,
    NoEscape = 1 << 1,
    Kill = 1 << 2,
    NoExp = 1 << 3,
    Antigrav = 1 << 4,
    Flip = 1 << 5,
    Autoreel = 1 << 6,
    Camo = 1 << 7,
}
