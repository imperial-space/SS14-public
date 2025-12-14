using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Interaction;
using Content.Server.Myrmex.Components;
using Content.Server.Stealth;
using Content.Server.Stunnable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Myrmex;

public sealed partial class MyrmexSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeedSys = default!;
    [Dependency] private readonly GunSystem _gunSys = default!;
    [Dependency] private readonly StunSystem _stunSys = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSys = default!;
    [Dependency] private readonly InteractionSystem _actSys = default!;
    [Dependency] private readonly StealthSystem _stealthSys = default!;
    [Dependency] private readonly ITimerManager _timerMan = default!;

    public void InitializeActions()
    {
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexShootEvent>(OnShoot);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexBoostEvent>(OnBoost);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleArmorEvent>(OnToggleArmor);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleStunEvent>(OnToggleStun);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexSpawnEvent>(OnSpawn);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleStealthEvent>(OnToggleStealth);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexHealEvent>(OnHeal);

        SubscribeLocalEvent<MyrmexComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexSpawnDoAfterEvent>(OnSpawnDoAfter);
    }

    private void OnShoot(Entity<MyrmexComponent> ent, ref ActionMyrmexShootEvent args)
    {
        EntityUid projectileEnt = Spawn(args.ProjectileProto, _formSys.GetMapCoordinates(ent));
        Vector2 userPos = _formSys.GetWorldPosition(args.Performer);
        _gunSys.ShootProjectile(projectileEnt, _formSys.ToMapCoordinates(args.Target).Position - userPos, Vector2.Zero, null, args.Performer, args.Speed);

        args.Handled = true;
    }

    private void OnBoost(Entity<MyrmexComponent> ent, ref ActionMyrmexBoostEvent args)
    {
        float multiplier = args.Multiplier; //cause lambda is compaining about ref
        ModifyMoveSpeed(ent, multiplier);
        Timer timer = new((int)args.Duration.TotalMilliseconds, false, () =>
        {
            if (Exists(ent))
                ModifyMoveSpeed(ent, -multiplier);
        });
        _timerMan.AddTimer(timer);

        args.Handled = true;
    }

    private void OnToggleArmor(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleArmorEvent args)
    {
        ent.Comp.ArmorActive = !ent.Comp.ArmorActive;
        _damageableSystem.SetDamageModifierSetId(ent, ent.Comp.ArmorActive ? ent.Comp.ActiveArmorProto : ent.Comp.StandardArmorProto);
        ModifyMoveSpeed(ent, ent.Comp.ArmorActive ? -ent.Comp.ActiveArmorSpeedMultiplier : ent.Comp.ActiveArmorSpeedMultiplier);

        args.Handled = true;
    }

    private void OnToggleStun(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleStunEvent args)
    {
        ent.Comp.StunActive = true;
        args.Handled = true;
    }

    private void OnSpawn(Entity<MyrmexComponent> ent, ref ActionMyrmexSpawnEvent args)
    {
        if (!_actSys.InRangeUnobstructed(ent, GetPosInfront(ent)))
            return;

        ActionMyrmexSpawnDoAfterEvent ev = new() { Proto = args.Proto };
        DoAfterArgs doAfterArgs = new(EntityManager, ent, args.DoAfterDuration, ev, ent) { BreakOnMove = true, BreakOnDamage = true };
        _doAfterSys.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }

    private void OnToggleStealth(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleStealthEvent args)
    {
        ent.Comp.StealthActive = !ent.Comp.StealthActive;
        _stealthSys.SetEnabled(ent, ent.Comp.StealthActive);
        args.Handled = true;
    }

    private void OnHeal(Entity<MyrmexComponent> ent, ref ActionMyrmexHealEvent args)
    {
        _damageableSystem.TryChangeDamage(args.Target, args.HealedDamage, true);
        args.Handled = true;
    }

    //increases original movement speed by 'multiplier' percents (0-1)
    private void ModifyMoveSpeed(Entity<MyrmexComponent> ent, float multiplier)
    {
        MovementSpeedModifierComponent moveSpeed = EnsureComp<MovementSpeedModifierComponent>(ent);

        float oldMultiplier = ent.Comp.CurrentSpeedMultiplier;
        ent.Comp.CurrentSpeedMultiplier += multiplier;
        float realMultiplier = ent.Comp.CurrentSpeedMultiplier / oldMultiplier;

        _moveSpeedSys.ChangeBaseSpeed(
            ent,
            moveSpeed.BaseWalkSpeed * realMultiplier,
            moveSpeed.BaseSprintSpeed * realMultiplier,
            moveSpeed.BaseAcceleration * realMultiplier,
            moveSpeed);
    }

    private void OnHit(Entity<MyrmexComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.StunActive)
            return;

        ent.Comp.StunActive = false;

        foreach (EntityUid hitEnt in args.HitEntities)
        {
            _stunSys.TryAddStunDuration(hitEnt, ent.Comp.StunDuration);
        }
    }

    private MapCoordinates GetPosInfront(EntityUid ent)
    {
        TransformComponent form = Transform(ent);
        Vector2 localPos = form.LocalPosition.Floored() + new Vector2(0.5f) + form.LocalRotation.GetCardinalDir().ToVec();
        return new MapCoordinates(Vector2.Transform(localPos, _formSys.GetWorldMatrix(form.ParentUid)), form.MapID);
    }

    private void OnSpawnDoAfter(Entity<MyrmexComponent> ent, ref ActionMyrmexSpawnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        MapCoordinates coords = GetPosInfront(ent);
        if (_actSys.InRangeUnobstructed(ent, coords))
        {
            Spawn(args.Proto, coords);
            args.Handled = true;
        }
    }
}
