using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

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
