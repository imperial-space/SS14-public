namespace Content.Shared.Imperial.Fishing;

/// <summary>Вид движения рыбы в мини-игре (/datum/fish_movement/*).</summary>
public enum ImperialFishMovementType : byte
{
    Default,
    Slow,
    Zippy,
    Accelerando,
    Choppy,
    Plunger,
}
