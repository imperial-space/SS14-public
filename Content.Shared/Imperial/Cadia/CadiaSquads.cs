using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaSquadOrder : byte
{
    None,
    Hold,
    Advance,
    Retreat,
}

[Serializable, NetSerializable]
public enum CadiaVoxCommand : byte
{
    /// <summary>«Огонь на подавление!» — точнее стрельба.</summary>
    Suppress,

    /// <summary>«Стоять насмерть!» — меньше урона.</summary>
    Hold,

    /// <summary>«Вперёд, за Императора!» — быстрее бег.</summary>
    Charge,
}

/// <summary>
/// Боец отряда: номер, цвет и название отряда, командир ли он. Свои видят над его головой знак отряда.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSquadMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Number;

    [DataField, AutoNetworkedField]
    public string Name = string.Empty;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public bool Leader;

    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    /// <summary>Действующий приказ отряда — показывается над головой и в окне.</summary>
    [DataField, AutoNetworkedField]
    public CadiaSquadOrder Order;

    /// <summary>Куда отдан приказ: стрелка цели ведёт сюда.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 OrderPoint;

    [DataField, AutoNetworkedField]
    public CadiaFlightLevel OrderLevel;
}

/// <summary>Временные бонусы вокс-команд и воодушевления комиссара.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSquadBuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SuppressUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan HoldUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan ChargeUntil;
}

/// <summary>Бонус к скорости от «Вперёд!» — общий для сервера и клиента, чтобы бег не дёргался.</summary>
public sealed class CadiaSquadBuffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public const float ChargeSpeed = 1.18f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaSquadBuffComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
    }

    private void OnSpeed(Entity<CadiaSquadBuffComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime < ent.Comp.ChargeUntil)
            args.ModifySpeed(ChargeSpeed);
    }
}

// ═══════════════════════ Окно отрядов ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaSquadUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CadiaSquadInfo
{
    public int Number;
    public string Name = string.Empty;
    public Color Color;
    public NetEntity? Leader;
    public CadiaSquadOrder Order;
    public List<NetEntity> Members = new();
}

[Serializable, NetSerializable]
public readonly record struct CadiaSquadFighter(NetEntity Entity, string Name, string Role, int Squad, bool Alive, float Distance);

[Serializable, NetSerializable]
public sealed class CadiaSquadState : BoundUserInterfaceState
{
    public CadiaFaction Faction;
    public List<CadiaSquadInfo> Squads = new();
    public List<CadiaSquadFighter> Fighters = new();
    public TimeSpan VoxCooldownUntil;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadCreateMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadDisbandMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadAssignMessage(NetEntity fighter, int number) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fighter = fighter;
    public readonly int Number = number;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadLeaderMessage(NetEntity fighter) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fighter = fighter;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadOrderMessage(int number, CadiaSquadOrder order) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly CadiaSquadOrder Order = order;
}

[Serializable, NetSerializable]
public sealed class CadiaSquadVoxMessage(int number, CadiaVoxCommand command) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly CadiaVoxCommand Command = command;
}

/// <summary>Приказ отряду с гололитического стола: точка на карте этажа.</summary>
[Serializable, NetSerializable]
public sealed class CadiaHoloOrderMessage(int squad, CadiaSquadOrder order, Vector2 point, CadiaFlightLevel level) : BoundUserInterfaceMessage
{
    public readonly int Squad = squad;
    public readonly CadiaSquadOrder Order = order;
    public readonly Vector2 Point = point;
    public readonly CadiaFlightLevel Level = level;
}

[Serializable, NetSerializable]
public readonly record struct CadiaHoloSquad(int Number, string Name, Color Color);

// ═══════════════════════ Действия ═══════════════════════

/// <summary>Действие визора: открыть окно отрядов.</summary>
public sealed partial class CadiaSquadMenuActionEvent : InstantActionEvent;

/// <summary>Комиссар: воодушевить бойца — он стоит насмерть.</summary>
public sealed partial class CadiaInspireActionEvent : EntityTargetActionEvent;

/// <summary>Комиссар: показательная казнь труса.</summary>
public sealed partial class CadiaExecuteActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class CadiaExecuteDoAfterEvent : SimpleDoAfterEvent;
