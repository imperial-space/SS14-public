using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>gun_maintenance_supplies TG: чинит оружие, тратит заряд.</summary>
[RegisterComponent]
public sealed partial class ImperialGunMaintenanceComponent : Component
{
    [DataField]
    public int Uses = 3;

    [DataField]
    public int MaxUses = 3;
}

/// <summary>organ/tongue/robot TG: речь становится механической, пока язык во рту.</summary>
[RegisterComponent]
public sealed partial class ImperialRobotTongueComponent : Component
{
    [DataField]
    public ProtoId<SpeechVerbPrototype> SpeechVerb = "Robotic";

    [DataField]
    public ProtoId<SpeechSoundsPrototype> SpeechSounds = "Borg";
}

/// <summary>Прежняя речь тела, пока в нём механический язык.</summary>
[RegisterComponent]
public sealed partial class ImperialRobotTongueSpeakerComponent : Component
{
    [ViewVariables]
    public ProtoId<SpeechVerbPrototype> OldVerb = "Default";

    [ViewVariables]
    public ProtoId<SpeechSoundsPrototype>? OldSounds;
}

/// <summary>dyespray TG: перекрашивает волосы или бороду.</summary>
[RegisterComponent]
public sealed partial class ImperialHairDyeComponent : Component
{
    /// <summary>true — борода, false — волосы.</summary>
    [DataField]
    public bool Facial;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public Color? PendingColor;
}

[Serializable, NetSerializable]
public sealed partial class ImperialHairDyeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>ph_booklet TG: из книжки вырываются полоски индикатора.</summary>
[RegisterComponent]
public sealed partial class ImperialPhBookletComponent : Component
{
    [DataField]
    public int Pages = 50;

    [DataField]
    public EntProtoId Strip = "ImperialPhPaper";
}

[Serializable, NetSerializable]
public enum ImperialPhBookletVisuals : byte
{
    State,
}

/// <summary>ph_paper TG: окрашивается по pH раствора.</summary>
[RegisterComponent]
public sealed partial class ImperialPhPaperComponent : Component
{
    [DataField]
    public bool Used;
}

/// <summary>pH реагентов TG (reagent.ph) для полосок индикатора.</summary>
[Prototype]
public sealed partial class ImperialReagentPhPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, float> Ph = new();
}

/// <summary>camera_film TG: добавляет снимки в камеру.</summary>
[RegisterComponent]
public sealed partial class ImperialCameraFilmComponent : Component
{
    [DataField]
    public int Pictures = 10;
}

/// <summary>seeds/random TG: случайное растение со случайными реагентами.</summary>
[RegisterComponent]
public sealed partial class ImperialStrangeSeedComponent : Component
{
    [DataField]
    public List<string> Reagents = new();
}

/// <summary>dish_drive TG: всасывает посуду поблизости и отправляет мусор в утилизатор.</summary>
[RegisterComponent]
public sealed partial class ImperialDishDriveComponent : Component
{
    [DataField]
    public List<EntProtoId> Collectable = new();

    [DataField]
    public List<EntProtoId> Disposable = new();

    [DataField]
    public string Container = "dish_drive";

    /// <summary>2 + уровень сервопривода.</summary>
    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan TransmitInterval = TimeSpan.FromMinutes(1);

    [DataField]
    public float DisposalRange = 7f;

    [DataField]
    public bool Suction = true;

    [DataField]
    public bool Transmit = true;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    [ViewVariables]
    public TimeSpan NextSuck;

    [ViewVariables]
    public TimeSpan NextTransmit;
}

/// <summary>Плата дисковода: всасывание и автоотправку можно выключить (attack_self / alt-click).</summary>
[RegisterComponent]
public sealed partial class ImperialDishDriveBoardComponent : Component
{
    [DataField]
    public bool Suction = true;

    [DataField]
    public bool Transmit = true;
}

/// <summary>mailsorter TG: сортирует посылки по отделам получателей.</summary>
[RegisterComponent]
public sealed partial class ImperialMailSorterComponent : Component
{
    [DataField]
    public string Container = "mail_sorter";

    [DataField]
    public int Capacity = 100;

    [DataField]
    public TimeSpan SortDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier SortSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    [ViewVariables]
    public bool Sorting;
}

/// <summary>vending/custom TG: продаёт вложенные предметы по ценам владельца.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialCustomVendorComponent : Component
{
    [DataField]
    public string Container = "custom_vendor";

    [DataField]
    public int MaxItems = 20;

    /// <summary>Счёт владельца в банке Imperial.</summary>
    [DataField, AutoNetworkedField]
    public int? Account;

    [DataField, AutoNetworkedField]
    public string Owner = string.Empty;

    /// <summary>Цены вложенных предметов.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<NetEntity, int> Prices = new();
}

/// <summary>vending_refill/custom: хранит товар автомата при разборке и перевозке.</summary>
[RegisterComponent]
public sealed partial class ImperialCustomVendorRefillComponent : Component
{
    [DataField]
    public string Container = "storagebase";
}

[Serializable, NetSerializable]
public enum ImperialCustomVendorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorEntry(NetEntity item, string proto, string name, int price)
{
    public readonly NetEntity Item = item;
    public readonly string Proto = proto;
    public readonly string Name = name;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorState(string owner, bool isOwner, int cash, List<ImperialCustomVendorEntry> items) : BoundUserInterfaceState
{
    public readonly string Owner = owner;
    public readonly bool IsOwner = isOwner;
    public readonly int Cash = cash;
    public readonly List<ImperialCustomVendorEntry> Items = items;
}

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorBuyMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorPriceMessage(NetEntity item, int price) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
    public readonly int Price = price;
}

// ── Чёрный рынок (code/modules/cargo/markets) ──

/// <summary>/datum/market_item: товар чёрного рынка.</summary>
[Prototype]
public sealed partial class ImperialMarketItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Category = string.Empty;

    [DataField]
    public int PriceMin;

    [DataField]
    public int PriceMax;

    [DataField]
    public int StockMin = 1;

    [DataField]
    public int StockMax = 1;

    /// <summary>availability_prob, %.</summary>
    [DataField]
    public int Availability = 100;
}

/// <summary>market_uplink TG.</summary>
[RegisterComponent]
public sealed partial class ImperialMarketUplinkComponent : Component;

[Serializable, NetSerializable]
public enum ImperialMarketUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ImperialMarketShipping : byte
{
    /// <summary>SHIPPING_METHOD_LAUNCH: бесплатно, падает где-то рядом.</summary>
    Launch,

    /// <summary>SHIPPING_METHOD_TELEPORT: телепорт к покупателю.</summary>
    Teleport,
}

[Serializable, NetSerializable]
public sealed class ImperialMarketEntry(string id, string item, string name, string description, string category, int price, int stock)
{
    public readonly string Id = id;
    public readonly string Item = item;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly string Category = category;
    public readonly int Price = price;
    public readonly int Stock = stock;
}

[Serializable, NetSerializable]
public sealed class ImperialMarketState(string holder, int balance, int launchFee, int teleportFee, List<ImperialMarketEntry> items) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly int LaunchFee = launchFee;
    public readonly int TeleportFee = teleportFee;
    public readonly List<ImperialMarketEntry> Items = items;
}

[Serializable, NetSerializable]
public sealed class ImperialMarketBuyMessage(string id, ImperialMarketShipping shipping) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
    public readonly ImperialMarketShipping Shipping = shipping;
}
