using Content.Shared.Imperial.Fishing;

namespace Content.Server.Imperial.Fishing;

/// <summary>Удочка со снаряжением на момент рыбалки.</summary>
public readonly record struct ImperialFishingRodInfo(
    EntityUid Rod,
    ImperialFishingRodComponent Comp,
    EntityUid? Line,
    ImperialFishingLineComponent? LineComp,
    EntityUid? Hook,
    ImperialFishingHookComponent? HookComp,
    EntityUid? Bait)
{
    public bool LineHas(ImperialFishingLineTraits trait) => LineComp != null && (LineComp.Traits & trait) != 0;

    public bool HookHas(ImperialFishingHookTraits trait) => HookComp != null && (HookComp.Traits & trait) != 0;
}
