using System.Numerics;
using Content.Server.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticHallucinationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming           _timing     = default!;
    [Dependency] private readonly IRobustRandom          _random     = default!;
    [Dependency] private readonly SharedAudioSystem      _audio      = default!;
    [Dependency] private readonly BlindableSystem        _blindable  = default!;
    [Dependency] private readonly SharedEyeSystem        _eye        = default!;
    [Dependency] private readonly MetaDataSystem         _metaData   = default!;
    [Dependency] private readonly VisualBodySystem       _visualBody = default!;
    [Dependency] private readonly IPrototypeManager      _prototype  = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;

    private static readonly string[] HallucinationSounds =
    {
        "/Audio/Imperial/heretic/i_see_you1.ogg",
        "/Audio/Imperial/heretic/i_see_you2.ogg",
        "/Audio/Imperial/heretic/magic.ogg",
        "/Audio/Imperial/heretic/sound_ambience_misc_ambiatm1.ogg",
    };

    private const int   BlurAmount       = 5;
    private const float GhostSpawnChance = 0.35f;
    private const float GhostMaxDist     = 5f;
    private const float GhostLifeMin     = 3f;
    private const float GhostLifeMax     = 6f;

    private readonly Dictionary<EntityUid, TimeSpan>  _activeBlurs  = new();
    private readonly Dictionary<EntityUid, EntityUid> _activeGhosts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.NextHallucinationTime = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat(ent.Comp.MinInterval * 0.1f, ent.Comp.MinInterval * 0.5f));

        EnsureComp<HereticMoonIllusionViewerComponent>(args.Target);
        _eye.RefreshVisibilityMask(args.Target);
    }

    private void OnRemoved(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<HereticMoonIllusionViewerComponent>(args.Target);
        _eye.RefreshVisibilityMask(args.Target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Remove expired blurs
        var expiredBlurs = new List<EntityUid>();
        foreach (var (uid, restoreTime) in _activeBlurs)
        {
            if (now < restoreTime) continue;
            expiredBlurs.Add(uid);
            if (Exists(uid))
                _blindable.AdjustEyeDamage((uid, null), -BlurAmount);
        }
        foreach (var uid in expiredBlurs)
            _activeBlurs.Remove(uid);

        // Clear ghost slots when the entity has despawned (TimedDespawnComponent handles deletion)
        var expiredGhosts = new List<EntityUid>();
        foreach (var (ownerUid, ghostUid) in _activeGhosts)
        {
            if (!Exists(ghostUid))
                expiredGhosts.Add(ownerUid);
        }
        foreach (var uid in expiredGhosts)
            _activeGhosts.Remove(uid);

        // Trigger periodic hallucinations
        var query = EntityQueryEnumerator<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var hallucination, out var statusEffect))
        {
            if (now < hallucination.NextHallucinationTime) continue;
            if (statusEffect.AppliedTo is not { } target) continue;

            hallucination.NextHallucinationTime = now + TimeSpan.FromSeconds(
                _random.NextFloat(hallucination.MinInterval, hallucination.MaxInterval));

            TriggerHallucination(target);
        }
    }

    private void TriggerHallucination(EntityUid target)
    {
        if (!_activeBlurs.ContainsKey(target))
        {
            var duration = _random.NextFloat(3f, 5f);
            _blindable.AdjustEyeDamage((target, null), BlurAmount);
            _activeBlurs[target] = _timing.CurTime + TimeSpan.FromSeconds(duration);
        }

        var soundPath = _random.Pick(HallucinationSounds);
        if (TryComp<ActorComponent>(target, out var actor))
            _audio.PlayGlobal(new SoundPathSpecifier(soundPath), actor.PlayerSession);
        else
            _audio.PlayPvs(new SoundPathSpecifier(soundPath), target);

        if (!_activeGhosts.ContainsKey(target) && _random.Prob(GhostSpawnChance))
            SpawnGhostIllusion(target);
    }

    private void SpawnGhostIllusion(EntityUid target)
    {
        var angle  = _random.NextFloat(0f, MathF.PI * 2f);
        var dist   = _random.NextFloat(0.5f, GhostMaxDist);
        var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
        var coords = Transform(target).Coordinates.Offset(offset);

        if (!TryComp<HumanoidProfileComponent>(target, out var humanoid))
            return;
        if (!_prototype.Resolve(humanoid.Species, out var speciesProto))
            return;

        var ghost = Spawn(speciesProto.Prototype, coords);

        _visualBody.CopyAppearanceFrom(target, ghost);

        RemComp<DamageableComponent>(ghost);
        RemComp<MindContainerComponent>(ghost);
        RemComp<SSDIndicatorComponent>(ghost);
        RemComp<InputMoverComponent>(ghost);
        RemComp<MobMoverComponent>(ghost);
        _visibility.SetLayer(ghost, (ushort) VisibilityFlags.HereticIllusion);

        EnsureComp<TimedDespawnComponent>(ghost).Lifetime = _random.NextFloat(GhostLifeMin, GhostLifeMax);
        EnsureComp<HereticMoonIllusionComponent>(ghost);
        EnsureComp<HereticMoonGhostComponent>(ghost);
        EnsureComp<HereticMoonIllusionMovementComponent>(ghost);
        EnsureComp<CanMoveInAirComponent>(ghost);

        var targetMeta = MetaData(target);
        _metaData.SetEntityName(ghost, targetMeta.EntityName);
        _metaData.SetEntityDescription(ghost, targetMeta.EntityDescription);

        _activeGhosts[target] = ghost;
    }
}
