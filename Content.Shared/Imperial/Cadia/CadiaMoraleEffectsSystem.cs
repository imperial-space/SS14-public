using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

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
