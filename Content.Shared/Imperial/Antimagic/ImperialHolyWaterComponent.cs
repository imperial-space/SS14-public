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
}
