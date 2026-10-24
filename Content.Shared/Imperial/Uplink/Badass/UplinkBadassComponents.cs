using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Карты Синдиката (toy/cards/deck/syndicate): колода, из которой тянут острые карты.</summary>
[RegisterComponent]
public sealed partial class UplinkCardDeckComponent : Component
{
    [DataField]
    public EntProtoId Card = "ImperialSyndicateCard";

    [DataField]
    public int Cards = 52;

    [DataField]
    public int MaxCards = 52;
}

[Serializable, NetSerializable]
public enum UplinkCardDeckVisuals : byte
{
    State,
}

/// <summary>Одиночная карта из колоды Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkCardComponent : Component
{
    [DataField]
    public string CardName = string.Empty;

    [DataField]
    public bool FaceUp;
}

/// <summary>Стикер Синдиката (sticker/syndicate): клеится на что угодно.</summary>
[RegisterComponent]
public sealed partial class UplinkStickerComponent : Component
{
    /// <summary>Наклеенная версия стикера.</summary>
    [DataField(required: true)]
    public EntProtoId Attached;
}

/// <summary>Наклеенный стикер; помнит, каким предметом был.</summary>
[RegisterComponent]
public sealed partial class UplinkAttachedStickerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;
}

/// <summary>Сущность, на которую наклеены стикеры.</summary>
[RegisterComponent]
public sealed partial class UplinkStickeredComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Stickers = new();
}

/// <summary>Свёрнутый крамольный плакат (poster/traitor).</summary>
[RegisterComponent]
public sealed partial class UplinkTraitorPosterComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Posters = new();
}

[Serializable, NetSerializable]
public sealed partial class UplinkPosterPlaceDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Крамольный баллончик (traitor_spraycan): рисует скользкое граффити Синдиката 3x3.</summary>
[RegisterComponent]
public sealed partial class UplinkSpraycanComponent : Component
{
    [DataField]
    public EntProtoId RuneProto = "ImperialTraitorRune";

    [DataField]
    public TimeSpan DrawTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public bool Expended;

    [ViewVariables]
    public bool Drawing;

    /// <summary>Граффити, которое рисуется сейчас.</summary>
    [ViewVariables]
    public EntityUid? Rune;

    /// <summary>Место нового граффити до появления контура.</summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? Spot;
}

[Serializable, NetSerializable]
public sealed partial class UplinkSpraycanDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Граффити Синдиката (effect/decal/cleanable/traitor_rune).</summary>
[RegisterComponent]
public sealed partial class UplinkTraitorRuneComponent : Component
{
    [ViewVariables]
    public UplinkRuneStage Stage = UplinkRuneStage.Outline;

    /// <summary>Пять минут после завершения граффити нельзя отмыть.</summary>
    [ViewVariables]
    public TimeSpan? RemovableAt;
}

[Serializable, NetSerializable]
public enum UplinkRuneStage : byte
{
    Outline,
    Coloured,
    Complete,
    Removable,
}

[Serializable, NetSerializable]
public enum UplinkRuneVisuals : byte
{
    Stage,
}

/// <summary>Сломанный хамелеон: при создании принимает случайный облик и больше не меняется.</summary>
[RegisterComponent]
public sealed partial class UplinkBrokenChameleonComponent : Component;

/// <summary>Ящик излишков Синдиката: при создании набивается случайными позициями аплинка на заданную сумму.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkSurplusCrateComponent : Component
{
    [DataField]
    public int Value = 40;

    /// <summary>Объединённый ящик: содержимое появляется только после ключа.</summary>
    [DataField, AutoNetworkedField]
    public bool United;

    [ViewVariables]
    public List<EntProtoId> Contents = new();

    [ViewVariables, AutoNetworkedField]
    public bool Unlocked;
}

/// <summary>Ключ объединённого ящика излишков.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicrateKeyComponent : Component;

/// <summary>Имплант вступления (implant/nuclear_operative): делает антагониста ядерным оперативником.</summary>
[RegisterComponent]
public sealed partial class UplinkInductionImplantComponent : Component;
