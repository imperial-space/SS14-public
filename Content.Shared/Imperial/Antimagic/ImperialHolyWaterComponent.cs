using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// В теле святая вода (TRAIT_HOLY из SS13). Сколько секунд она уже действует — для порогов эффектов.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialHolyWaterComponent : Component
{
    [ViewVariables]
    public TimeSpan Seconds;

    [ViewVariables]
    public TimeSpan NextSpellClear;

    [DataField]
    public TimeSpan TickTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan TickBoozePowerDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan TickDuration = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan TickDuration2 = TimeSpan.FromSeconds(10);

    [DataField]
    public float TickChance = 0.1f;
}
