using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Сервер просит верховного жреца выбрать название религии, имя божества и название библии
/// (настройки religion_name / deity_name / bible_name из SS13).
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialReligionSetupOpenEvent(string religion, string deity, string bible) : EntityEventArgs
{
    public readonly string Religion = religion;
    public readonly string Deity = deity;
    public readonly string Bible = bible;
}
