using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Секвей (vehicle/ridden/secway) ────────────────────────────────────────

/// <summary>
/// Секвей СБ. Едет только с ключом в замке и без банана в выхлопной трубе.
/// Общий — чтобы скорость считалась одинаково на клиенте и сервере.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SecwayComponent : Component
{
    public const string KeySlot = "secway_key";
    public const string ExhaustContainer = "secway-exhaust";

    [DataField, AutoNetworkedField]
    public bool Blocked = true;

    /// <summary>message_cooldown «глохнет и отказывается ехать».</summary>
    [ViewVariables]
    public TimeSpan NextStallMessage;

    [ViewVariables]
    public TimeSpan NextSmoke;

    /// <summary>Мотор заведён: седок на месте, ключ в замке, труба чистая.</summary>
    [ViewVariables]
    public bool EngineRunning;
}

public sealed class SharedSecwaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecwayComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<SecwayComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Blocked)
            args.ModifySpeed(0f, 0f);
    }
}

/// <summary>Засунуть банан в выхлопную трубу.</summary>
[Serializable, NetSerializable]
public sealed partial class SecwayBananaDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Вычистить банан из трубы.</summary>
[Serializable, NetSerializable]
public sealed partial class SecwayCleanDoAfterEvent : SimpleDoAfterEvent;

// ─── Пейнтбол (paintball_gun, pepperball) ──────────────────────────────────

/// <summary>Цвет шарика или пятна краски.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PaintballColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}

[Serializable, NetSerializable]
public enum PaintballGunVisuals : byte
{
    Overlay,
}

[Serializable, NetSerializable]
public enum PaintballGunOverlay : byte
{
    None,
    Blue,
    BlueEmpty,
    Red,
    RedEmpty,
    Green,
    GreenEmpty,
    Yellow,
    YellowEmpty,
    Pepper,
    PepperEmpty,
}

// ─── Плакаты розыска (wanted_poster) ───────────────────────────────────────

[Serializable, NetSerializable]
public enum WantedPosterVisuals : byte
{
    Missing,
}

[Serializable, NetSerializable]
public sealed partial class WantedPosterPlaceDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum WantedPrinterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum WantedPrintType : byte
{
    Wanted,
    Missing,
    Rapsheet,
}

[Serializable, NetSerializable]
public sealed class WantedPrinterState : BoundUserInterfaceState
{
    public List<string> Names = new();
    public List<bool> HasCrimes = new();
    public bool Printing;
}

[Serializable, NetSerializable]
public sealed class WantedPrintMessage(string name, WantedPrintType type, string alias, string header, string description)
    : BoundUserInterfaceMessage
{
    public readonly string Name = name;
    public readonly WantedPrintType Type = type;
    public readonly string Alias = alias;
    public readonly string Header = header;
    public readonly string Description = description;
}

// ─── Задания СБ (civilian bounty, security) ────────────────────────────────

[Serializable, NetSerializable]
public enum SecurityBountyUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SecurityBountyEntry
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Reward;
}

[Serializable, NetSerializable]
public sealed class SecurityBountyState : BoundUserInterfaceState
{
    public bool HasPad;
    public bool HasId;
    public string? IdName;
    public string StatusReport = string.Empty;
    public bool Sending;
    public SecurityBountyEntry? Current;
    public string? CurrentProgress;
    public List<SecurityBountyEntry> Options = new();
}

[Serializable, NetSerializable]
public enum SecurityBountyAction : byte
{
    Request,
    Recalc,
    Send,
    Eject,
}

[Serializable, NetSerializable]
public sealed class SecurityBountyActionMessage(SecurityBountyAction action) : BoundUserInterfaceMessage
{
    public readonly SecurityBountyAction Action = action;
}

[Serializable, NetSerializable]
public sealed class SecurityBountyPickMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

// ─── Пакет для улик (evidencebag) ──────────────────────────────────────────

/// <summary>Пакет для улик. Общий — клиент рисует лежащий внутри предмет.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EvidenceBagComponent : Component
{
    public const string ContainerId = "storagebase";
}

[Serializable, NetSerializable]
public enum EvidenceBagVisuals : byte
{
    Filled,
}

// ─── Щит со вспышкой (shield/riot/flash) ───────────────────────────────────

[Serializable, NetSerializable]
public enum StrobeShieldVisuals : byte
{
    Flashing,
}

[Serializable, NetSerializable]
public sealed partial class StrobeShieldBulbDoAfterEvent : SimpleDoAfterEvent;
