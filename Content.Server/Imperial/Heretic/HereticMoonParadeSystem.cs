using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonParadeSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem                  _chat           = default!;
    [Dependency] private readonly SharedAudioSystem          _audio          = default!;
    [Dependency] private readonly SharedPhysicsSystem        _physics        = default!;
    [Dependency] private readonly SharedTransformSystem      _xform          = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly MobStateSystem             _mobs           = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticMoonParadeActionEvent>(OnMoonParade);
        SubscribeLocalEvent<HereticMoonParadeProjectileComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<HereticMoonParadeLeashComponent, DamageChangedEvent>(OnLeashDamage);
    }

    private void OnMoonParade(EntityUid uid, HereticComponent comp, HereticMoonParadeActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var originWorldPos = _xform.GetWorldPosition(uid);
        var targetWorldPos = _xform.ToMapCoordinates(args.Target).Position;

        var direction = targetWorldPos - originWorldPos;
        if (direction.LengthSquared() < 0.001f)
            direction = new Vector2(1f, 0f);
        direction = Vector2.Normalize(direction);

        var projectile = Spawn("ProjectileMoonParade", Transform(uid).Coordinates);
        var projComp = EnsureComp<HereticMoonParadeProjectileComponent>(projectile);
        projComp.Shooter = uid;

        if (TryComp<PhysicsComponent>(projectile, out var physics))
            _physics.SetLinearVelocity(projectile, direction * projComp.Speed, body: physics);

        _xform.SetLocalRotation(projectile, direction.ToAngle());
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_moon_parade.ogg"), uid);
    }

    private void OnCollide(EntityUid uid, HereticMoonParadeProjectileComponent comp, ref StartCollideEvent args)
    {
        switch (args.OurFixtureId)
        {
            case "mob_sensor":
                if (args.OtherEntity == comp.Shooter) return;
                if (!HasComp<MobStateComponent>(args.OtherEntity)) return;
                if (HasComp<HereticComponent>(args.OtherEntity)) return;
                if (!comp.MobsHit.Add(args.OtherEntity)) return;

                // Leash: жертва тянется за снарядом на 20 секунд
                var leash = EnsureComp<HereticMoonParadeLeashComponent>(args.OtherEntity);
                leash.Projectile = uid;
                leash.TimeRemaining = 20f;
                leash.DamageAccumulated = 0f;

                // Рассудок -20 + галлюцинации
                if (!TryComp<HereticMoonBrainDamageComponent>(args.OtherEntity, out var brainComp))
                    brainComp = AddComp<HereticMoonBrainDamageComponent>(args.OtherEntity);
                brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);
                _hereticEffects.ApplyHallucination(args.OtherEntity, TimeSpan.FromSeconds(20));
                break;

            case "wall_stop":
                if (!TryComp<PhysicsComponent>(uid, out var physics)) { QueueDel(uid); return; }

                var velocity  = _physics.GetMapLinearVelocity(uid, component: physics);
                var normal    = args.WorldNormal;
                var reflected = velocity - 2f * Vector2.Dot(velocity, normal) * normal;

                _physics.SetLinearVelocity(uid, reflected, body: physics);
                _xform.SetLocalRotation(uid, reflected.ToAngle());

                comp.BounceCount++;
                if (comp.BounceCount >= comp.MaxBounces)
                    QueueDel(uid);
                break;
        }
    }

    private void OnLeashDamage(EntityUid uid, HereticMoonParadeLeashComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null) return;

        // Снимаем leash при получении 50+ урона с момента захвата
        foreach (var val in args.DamageDelta.DamageDict.Values)
        {
            if (val > 0) comp.DamageAccumulated += val.Float();
        }
        if (comp.DamageAccumulated >= HereticMoonParadeLeashComponent.DamageThreshold)
            RemComp<HereticMoonParadeLeashComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticMoonParadeLeashComponent>();
        while (query.MoveNext(out var uid, out var leash))
        {
            // Снаряд уничтожен — освобождаем
            if (!Exists(leash.Projectile))
            {
                RemComp<HereticMoonParadeLeashComponent>(uid);
                continue;
            }

            leash.TimeRemaining -= frameTime;
            if (leash.TimeRemaining <= 0f)
            {
                RemComp<HereticMoonParadeLeashComponent>(uid);
                continue;
            }

            // Мёртв — освобождаем
            if (!_mobs.IsAlive(uid))
            {
                RemComp<HereticMoonParadeLeashComponent>(uid);
                continue;
            }

            // Тянем жертву к снаряду
            var victimPos = _xform.GetWorldPosition(uid);
            var projPos   = _xform.GetWorldPosition(leash.Projectile);
            var dir       = projPos - victimPos;

            if (dir.LengthSquared() < 0.25f)
                continue;

            dir = Vector2.Normalize(dir) * HereticMoonParadeLeashComponent.PullSpeed;
            if (TryComp<PhysicsComponent>(uid, out var phys))
                _physics.SetLinearVelocity(uid, dir, body: phys);
        }
    }
}
