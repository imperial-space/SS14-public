using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Мораль бойца: демоны, обстрел, раны и гибель соседей её роняют, комиссар, священник и штандарт поднимают.
/// Космодесант страха не ведает: компонент у него есть по наследству от моба, но не работает.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaMoraleComponent : Component
{
    public const float ShakenBelow = 40f;
    public const float BrokenBelow = 15f;

    [DataField, AutoNetworkedField]
    public float Morale = 100f;

    /// <summary>Сама поднимается только до этого значения: дальше нужен кто-то, кто поведёт.</summary>
    [DataField]
    public float RecoverTo = 70f;

    [DataField]
    public float RecoverRate = 0.4f;

    [ViewVariables]
    public TimeSpan NextPanic;
}

/// <summary>Источник духа или страха: комиссар, священник, штандарт, демон. Действует на мораль вокруг себя.</summary>
[RegisterComponent]
public sealed partial class CadiaMoraleAuraComponent : Component
{
    [DataField]
    public float Radius = 7f;

    /// <summary>Своим, единиц в секунду.</summary>
    [DataField]
    public float FriendRate;

    /// <summary>Врагам, единиц в секунду.</summary>
    [DataField]
    public float EnemyRate;

    /// <summary>Сторона постройки. Без неё аура работает в руках бойца, и сторона берётся у него.</summary>
    [DataField]
    public CadiaFaction? Faction;
}

public sealed class CadiaMoraleEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float ShakenSpeed = 0.9f;
    private const float BrokenSpeed = 0.8f;
    private const uint BrokenMissPercent = 35;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaMoraleComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<CadiaMoraleComponent, ShotAttemptedEvent>(OnShot);
    }

    private void OnSpeed(Entity<CadiaMoraleComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (HasComp<CadiaAstartesComponent>(ent))
            return;
        if (ent.Comp.Morale < CadiaMoraleComponent.BrokenBelow)
            args.ModifySpeed(BrokenSpeed, BrokenSpeed);
        else if (ent.Comp.Morale < CadiaMoraleComponent.ShakenBelow)
            args.ModifySpeed(ShakenSpeed, ShakenSpeed);
    }

    /// <summary>Сломленному трясущиеся руки не дают выстрелить. Решение зависит от тика: клиент и сервер видят одно и то же.</summary>
    private void OnShot(Entity<CadiaMoraleComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Morale >= CadiaMoraleComponent.BrokenBelow || HasComp<CadiaAstartesComponent>(ent))
            return;
        var roll = unchecked(_timing.CurTick.Value * 2654435761u + (uint) ent.Owner.Id) % 100u;
        if (roll >= BrokenMissPercent)
            return;
        args.Cancel();
        _popup.PopupClient(Loc.GetString("cadia-morale-shaking"), ent, ent);
    }
}

/// <summary>Депо снабжения: копит очки и выдаёт за них ящики, которые потом тащат к позициям.</summary>
[RegisterComponent]
public sealed partial class CadiaDepotComponent : Component
{
    [DataField]
    public float Points = 40f;

    [DataField]
    public float MaxPoints = 120f;

    /// <summary>Очков в минуту.</summary>
    [DataField]
    public float PerMinute = 6f;

    [DataField]
    public List<CadiaDepotEntry> Entries = new();

    /// <summary>Чьи секторы пополняют это депо.</summary>
    [DataField]
    public CadiaFaction Faction = CadiaFaction.Imperium;
}

[DataDefinition]
public sealed partial class CadiaDepotEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Cost = 10;
}
