using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStarTouchSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming             _timing        = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction   = default!;
    [Dependency] private readonly SharedTransformSystem   _xform         = default!;
    [Dependency] private readonly SharedStunSystem        _stun          = default!;
    [Dependency] private readonly SharedPopupSystem       _popup         = default!;
    [Dependency] private readonly SharedAudioSystem       _audio         = default!;
    [Dependency] private readonly StatusEffectsSystem     _statusEffects = default!;

    private const float BeamRange = 8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticStarTouchActionEvent>(OnStarTouch);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticStarTouchBeamComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(comp.Target))
            {
                RemComp<HereticStarTouchBeamComponent>(uid);
                continue;
            }

            if (!_interaction.InRangeUnobstructed(uid, comp.Target, BeamRange + 0.5f))
            {
                _popup.PopupEntity(Loc.GetString("heretic-star-touch-beam-broke"), uid, uid, PopupType.SmallCaution);
                RemComp<HereticStarTouchBeamComponent>(uid);
                continue;
            }

            if (now < comp.BeamEndTime)
                continue;

            var herXform = Transform(uid);
            _xform.SetCoordinates(comp.Target, herXform.Coordinates);
            var pullLevel = TryComp<HereticComponent>(uid, out var pullHeretic) ? pullHeretic.PassiveLevel : 0;
            SpawnCarpet(pullLevel, herXform.ParentUid, Snap(herXform.LocalPosition));
            _stun.TryKnockdown(comp.Target, TimeSpan.FromSeconds(8), true);
            _statusEffects.TryAddStatusEffectDuration(comp.Target, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(4));
            _statusEffects.TrySetStatusEffectDuration(comp.Target, "StarMarkStatusEffect", TimeSpan.FromSeconds(30));
            _popup.PopupEntity(Loc.GetString("heretic-star-touch-beam-complete"), uid, uid, PopupType.Large);
            RemComp<HereticStarTouchBeamComponent>(uid);
        }
    }

    private void OnStarTouch(EntityUid uid, HereticComponent comp, HereticStarTouchActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var target = args.Target;
        if (!HasComp<MobStateComponent>(target) || target == uid)
            return;

        SpawnCarpets(uid);

        if (HasComp<StarMarkComponent>(target))
        {
            var beam = EnsureComp<HereticStarTouchBeamComponent>(uid);
            beam.Target      = target;
            beam.BeamEndTime = _timing.CurTime + beam.BeamDuration;
            Dirty(uid, beam);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_cosmic_energy.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("heretic-star-touch-beam-started"), uid, uid, PopupType.Medium);
        }
        else
        {
            _statusEffects.TrySetStatusEffectDuration(target, "StarMarkStatusEffect", TimeSpan.FromSeconds(30));
            _popup.PopupEntity(Loc.GetString("heretic-star-touch-marked"), uid, uid, PopupType.Medium);
        }
    }

    private void SpawnCarpets(EntityUid uid)
    {
        var xform    = Transform(uid);
        var localPos = xform.LocalPosition;
        var snapped  = new Vector2(MathF.Floor(localPos.X) + 0.5f, MathF.Floor(localPos.Y) + 0.5f);
        var parentUid = xform.ParentUid;

        var fwd  = xform.LocalRotation.ToVec();
        var perp = new Vector2(-fwd.Y, fwd.X);

        var level = TryComp<HereticComponent>(uid, out var heretic) ? heretic.PassiveLevel : 0;

        SpawnCarpet(level, parentUid, Snap(snapped));
        SpawnCarpet(level, parentUid, Snap(snapped + perp));
        SpawnCarpet(level, parentUid, Snap(snapped - perp));
    }

    private static Vector2 Snap(Vector2 v) => new(MathF.Floor(v.X) + 0.5f, MathF.Floor(v.Y) + 0.5f);

    private void SpawnCarpet(int passiveLevel, EntityUid parent, Vector2 pos)
    {
        var carpet = Spawn("HereticCosmicCarpet", new EntityCoordinates(parent, pos));
        if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
            field.PassiveLevel = passiveLevel;
    }
}
