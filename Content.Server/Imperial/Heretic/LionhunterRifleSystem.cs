using Content.Server.Damage.Systems;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;

namespace Content.Server.Imperial.Heretic;

public sealed class LionhunterRifleSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly HereticSystem _hereticSystem = default!;

    private const float MinAimedDistance = 4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LionhunterBulletComponent, ProjectileHitEvent>(OnBulletHit);
    }

    private void OnBulletHit(Entity<LionhunterBulletComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter) return;

        var shooterPos = _transform.GetMapCoordinates(shooter).Position;
        var hitPos = _transform.GetMapCoordinates(ent.Owner).Position;

        _transform.SetCoordinates(shooter, Transform(ent.Owner).Coordinates);

        if (!HasComp<MobStateComponent>(args.Target)) return;

        var dist = (hitPos - shooterPos).Length();

        if (dist >= MinAimedDistance)
        {
            _stamina.TakeStaminaDamage(args.Target, 60f, visual: true);
            _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(0.5), true);
            _statusEffects.TryAddStatusEffect(args.Target, "Stutter",
                TimeSpan.FromSeconds(6), true, "StutteringAccentComponent");

            if (TryComp<HereticComponent>(shooter, out var hereticComp))
                _hereticSystem.ApplyGraspMark(shooter, hereticComp, args.Target);

            _popup.PopupEntity(Loc.GetString("heretic-lionhunter-aimed-hit"), shooter, shooter);
        }
        else
        {
            _stamina.TakeStaminaDamage(args.Target, 30f, visual: true);
            _popup.PopupEntity(Loc.GetString("heretic-lionhunter-teleport"), shooter, shooter);
        }
    }
}
