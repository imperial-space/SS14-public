using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Heretic;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Imperial.Heretic.MoonParade;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Heretic.MoonParade;

public sealed class MoonParadeProjectileSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float PullSpeed = 5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoonParadeProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MoonParadeProjectileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.HitMobs.Count == 0)
                continue;

            var projPos = _xform.GetWorldPosition(uid);
            var toRemove = new List<EntityUid>();

            foreach (var mob in comp.HitMobs)
            {
                if (TerminatingOrDeleted(mob))
                {
                    toRemove.Add(mob);
                    continue;
                }

                if (!TryComp<PhysicsComponent>(mob, out var mobPhysics))
                    continue;

                var mobPos = _xform.GetWorldPosition(mob);
                var delta = projPos - mobPos;

                if (delta.LengthSquared() < 0.01f)
                    continue;

                _physics.SetLinearVelocity(mob, delta.Normalized() * PullSpeed, body: mobPhysics);
            }

            foreach (var dead in toRemove)
                comp.HitMobs.Remove(dead);
        }
    }

    private void OnStartCollide(EntityUid uid, MoonParadeProjectileComponent comp, ref StartCollideEvent args)
    {
        switch (args.OurFixtureId)
        {
            case "wall_bounce":
                comp.BounceCount++;
                if (comp.BounceCount >= comp.MaxBounces)
                    QueueDel(uid);
                break;

            case "mob_sensor":
                TryCaptureMob(uid, comp, args.OtherEntity);
                break;
        }
    }

    private void TryCaptureMob(EntityUid uid, MoonParadeProjectileComponent comp, EntityUid target)
    {
        if (target == comp.Shooter)
            return;

        if (!HasComp<MobStateComponent>(target))
            return;

        if (comp.HitMobs.Add(target))
        {
            _hereticEffects.ApplyInsanity(target, TimeSpan.FromSeconds(20));
            Spawn("HereticEffectRingleader", Transform(target).Coordinates);
            _popup.PopupEntity(Loc.GetString("heretic-moon-parade-hit"), target, target, PopupType.LargeCaution);
            _chat.TrySendInGameICMessage(target, Loc.GetString("heretic-moon-parade-enslaved"), InGameICChatType.Emote, true);
        }
    }
}
