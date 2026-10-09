using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Действие «Помолиться»: молитва святого уходит администрации с пометкой капеллана.
/// </summary>
public sealed partial class ImperialPrayActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum HolyArmamentsUiKey : byte
{
    Key,
}

/// <summary>
/// Клиент выбрал набор в радиальном меню маяка вооружения.
/// </summary>
[Serializable, NetSerializable]
public sealed class HolyArmamentsPickMessage(EntProtoId kit) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Kit = kit;
}

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

[Serializable, NetSerializable]
public enum ImperialBibleSkinUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ImperialBibleSkinVisuals : byte
{
    Skin,
    Layer,
}

/// <summary>
/// Верховный жрец выбрал обложку библии в радиальном меню.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialBibleSkinPickMessage(string skin) : BoundUserInterfaceMessage
{
    public readonly string Skin = skin;
}

/// <summary>
/// Приглашение вступить в религию станции перед посвящением в дьяконы (invite_deacon из SS13).
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialDeaconInviteEvent(string deity) : EntityEventArgs
{
    public readonly string Deity = deity;
}

/// <summary>
/// Ответ кандидата на приглашение.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialDeaconInviteResponseEvent(bool accepted) : EntityEventArgs
{
    public readonly bool Accepted = accepted;
}

/// <summary>
/// Одно воззвание обряда посвящения в дьяконы.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ImperialDeaconizeDoAfterEvent : SimpleDoAfterEvent;
