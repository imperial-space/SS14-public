using System.Numerics;
using Content.Server.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Eye;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.MoonParade;
using Content.Shared.Inventory;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonRingleaderSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem            _audio          = default!;
    [Dependency] private readonly EntityLookupSystem           _lookup         = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage    = default!;
    [Dependency] private readonly HereticMoonAmuletSystem      _moonAmulet     = default!;
    [Dependency] private readonly HereticStatusEffectsSystem   _hereticEffects = default!;
    [Dependency] private readonly IRobustRandom                _random         = default!;
    [Dependency] private readonly MobStateSystem               _mobs           = default!;
    [Dependency] private readonly SharedStunSystem             _stun           = default!;
    [Dependency] private readonly MetaDataSystem               _metaData       = default!;
    [Dependency] private readonly VisualBodySystem             _visualBody     = default!;
    [Dependency] private readonly InventorySystem              _inventory      = default!;
    [Dependency] private readonly IPrototypeManager            _prototype      = default!;
    [Dependency] private readonly SharedVisibilitySystem       _visibility     = default!;

    private const float AoeRadius     = 5f;
    private const float CloneSpawnRad = 5f;

    private const float CastBrainDmg    = 30f;
    private const float CastSanityLoss  = 30f;
    private const float LowSanityMult   = 1.5f;
    private const float LowSanityThresh = 20f;

    private static readonly SoundPathSpecifier CastSound = new("/Audio/Imperial/heretic/sound_effects_moon_parade.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticMoonRingleaderActionEvent>(OnRingleader);
    }

    private void OnRingleader(EntityUid uid, HereticComponent comp, HereticMoonRingleaderActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        _audio.PlayPvs(CastSound, uid);

        var casterCoords    = Transform(uid).Coordinates;
        var casterHasAmulet = HasComp<HereticMoonAmuletEquippedComponent>(uid);

        var victims = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(casterCoords, AoeRadius, victims);

        foreach (var (victimUid, _) in victims)
        {
            if (victimUid == uid) continue;
            if (!_mobs.IsAlive(victimUid)) continue;
            if (HasComp<HereticComponent>(victimUid)) continue;

            var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(victimUid);
            var lowSanity = brainComp.Sanity < LowSanityThresh;
            var mult      = lowSanity ? LowSanityMult : 1f;

            ApplyChannelAmulet(uid, victimUid, brainComp);

            if (casterHasAmulet || comp.PassiveLevel >= 3)
                brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);

            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - CastSanityLoss * mult);
            _brainDamage.AddBrainDamage(victimUid, CastBrainDmg * mult);

            var hallDuration = lowSanity ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(30);
            _hereticEffects.ApplyHallucination(victimUid, hallDuration);

            var cloneCount = _random.Next(2, 5);
            for (var i = 0; i < cloneCount; i++)
                SpawnClone(uid, victimUid);
        }
    }

    private void SpawnClone(EntityUid caster, EntityUid target)
    {
        var angle       = _random.NextFloat(0f, MathF.PI * 2f);
        var dist        = _random.NextFloat(0.5f, CloneSpawnRad);
        var offset      = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
        var spawnCoords = Transform(target).Coordinates.Offset(offset);

        if (!TryComp<HumanoidProfileComponent>(caster, out var humanoid))
            return;
        if (!_prototype.Resolve(humanoid.Species, out var speciesProto))
            return;

        var clone = Spawn(speciesProto.Prototype, spawnCoords);

        _visualBody.CopyAppearanceFrom(caster, clone);
        CopyEquipment(caster, clone);

        RemComp<DamageableComponent>(clone);
        RemComp<MindContainerComponent>(clone);
        RemComp<SSDIndicatorComponent>(clone);
        RemComp<InputMoverComponent>(clone);
        RemComp<MobMoverComponent>(clone);
        _visibility.SetLayer(clone, (ushort) VisibilityFlags.HereticIllusion);

        EnsureComp<TimedDespawnComponent>(clone).Lifetime = 30f;
        EnsureComp<HereticMoonIllusionComponent>(clone);
        EnsureComp<HereticMoonIllusionMovementComponent>(clone);
        EnsureComp<CanMoveInAirComponent>(clone);

        var casterMeta = MetaData(caster);
        _metaData.SetEntityName(clone, casterMeta.EntityName);
        _metaData.SetEntityDescription(clone, casterMeta.EntityDescription);
    }

    private void CopyEquipment(EntityUid source, EntityUid target)
    {
        var coords     = Transform(target).Coordinates;
        var enumerator = _inventory.GetSlotEnumerator(source);
        while (enumerator.NextItem(out var item, out var slot))
        {
            var protoId = MetaData(item).EntityPrototype?.ID;
            if (protoId == null) continue;
            var copy = Spawn(protoId, coords);
            _inventory.TryEquip(target, copy, slot.Name, silent: true);
        }
    }

    private void ApplyChannelAmulet(EntityUid caster, EntityUid target, HereticMoonBrainDamageComponent brainComp)
    {
        if (brainComp.Sanity >= 10f)
        {
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(120), true);
            return;
        }

        _moonAmulet.TryMoonConvert(target, brainComp.Sanity);
    }
}
