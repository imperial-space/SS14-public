using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Верховный жрец подтвердил (или закрыл) окно выбора религии.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialReligionSetupSubmitEvent(string religion, string deity, string bible) : EntityEventArgs
{
    public readonly string Religion = religion;
    public readonly string Deity = deity;
    public readonly string Bible = bible;
}
