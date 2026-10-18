using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using System.Numerics;
using Content.Server.Body;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// action/cooldown/spell/aoe/moon_ringleader: рядом с каждой жертвой встаёт отражение еретика.
/// Сам каст не вредит — вред приходит, когда отражение бьют.
/// </summary>
public sealed class HereticMoonRingleaderSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem            _audio          = default!;
    [Dependency] private readonly EntityLookupSystem           _lookup         = default!;
    [Dependency] private readonly HereticMoonAmuletSystem      _moonAmulet     = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage    = default!;
    [Dependency] private readonly IRobustRandom                _random         = default!;
    [Dependency] private readonly MobStateSystem               _mobs           = default!;
    [Dependency] private readonly SharedStunSystem             _stun           = default!;
    [Dependency] private readonly MetaDataSystem               _metaData       = default!;
    [Dependency] private readonly VisualBodySystem             _visualBody     = default!;
    [Dependency] private readonly InventorySystem              _inventory      = default!;
    [Dependency] private readonly IPrototypeManager            _prototype      = default!;
    [Dependency] private readonly SharedPhysicsSystem          _physics        = default!;
    [Dependency] private readonly SharedTransformSystem        _xform          = default!;
    [Dependency] private readonly ImperialAntimagicSystem      _antimagic      = default!;

    /// <summary>aoe_radius заклинания.</summary>
    private const float AoeRadius = 5f;

    /// <summary>pick(RANGE_TURFS(2, victim)): клон встаёт в двух тайлах от жертвы.</summary>
    private const float CloneSpawnRadius = 2f;

    /// <summary>life = 30 SECONDS.</summary>
    private const float CloneLifetime = 30f;

    private const float CloneSpeed = 2f;

    private static readonly SoundPathSpecifier CastSound =
        new("/Audio/Imperial/heretic/sound_effects_moon_parade.ogg");

    private static readonly SoundPathSpecifier BurstSound = new("/Audio/Items/bikehorn.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticMoonRingleaderActionEvent>(OnRingleader);
        SubscribeLocalEvent<HereticMoonRingleaderCloneComponent, AttackedEvent>(OnCloneAttacked);
    }

    private void OnRingleader(EntityUid uid, HereticComponent comp, HereticMoonRingleaderActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        CastRingleader(uid);
    }

    /// <summary>cast(): вызывается и способностью, и смертью носителя Блистательного облачения.</summary>
    public void CastRingleader(EntityUid uid)
    {
        _audio.PlayPvs(CastSound, uid);

        var casterCoords = Transform(uid).Coordinates;

        var victims = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(casterCoords, AoeRadius, victims);

        foreach (var (victimUid, _) in victims)
        {
            if (victimUid == uid) continue;
            if (_mobs.IsDead(victimUid)) continue;
            if (HasComp<HereticComponent>(victimUid)) continue;
            if (HasComp<BorgChassisComponent>(victimUid)) continue;
            if (!HasComp<HumanoidProfileComponent>(victimUid)) continue;
            if (_antimagic.CanBlockMagic(victimUid)) continue;

            SpawnClone(uid, victimUid);
        }
    }

    /// <summary>on_attacked(): отражение лопается, выжигая рассудок всем рядом.</summary>
    private void OnCloneAttacked(Entity<HereticMoonRingleaderCloneComponent> ent, ref AttackedEvent args)
    {
        // Еретики не могут подрывать свои же отражения.
        if (HasComp<HereticComponent>(args.User))
            return;

        var comp = ent.Comp;
        _audio.PlayPvs(BurstSound, ent.Owner);

        var coords = Transform(ent.Owner).Coordinates;
        var targets = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(coords, comp.BurstRadius, targets);

        foreach (var (victim, _) in targets)
        {
            if (HasComp<HereticComponent>(victim)) continue;
            if (_antimagic.CanBlockMagic(victim)) continue;

            // Амулет проводится до эффектов, иначе обращение успеет сработать от первого клона.
            if (Exists(comp.Caster) && HasComp<HereticMoonAmuletEquippedComponent>(comp.Caster))
                _moonAmulet.ChannelAmulet(comp.Caster, victim);

            _stun.TryUpdateStunDuration(victim, comp.BurstStun);
            _stun.TryKnockdown(victim, comp.BurstStun, true);

            var brain = EnsureComp<HereticMoonBrainDamageComponent>(victim);
            brain.Sanity = Math.Max(0f, brain.Sanity - comp.BurstSanityDamage);
            _brainDamage.AddBrainDamage(victim, comp.BurstBrainDamage);
        }

        QueueDel(ent.Owner);
    }

    private void SpawnClone(EntityUid caster, EntityUid target)
    {
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var dist = _random.NextFloat(1f, CloneSpawnRadius);
        var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
        var spawnCoords = Transform(target).Coordinates.Offset(offset);

        if (!TryComp<HumanoidProfileComponent>(caster, out var humanoid))
            return;
        if (!_prototype.Resolve(humanoid.Species, out var speciesProto))
            return;

        var clone = Spawn(speciesProto.Prototype, spawnCoords);

        _visualBody.CopyAppearanceFrom(caster, clone);
        CopyEquipment(caster, clone);

        // Отражение нельзя ранить и нельзя занять разумом, но ударить — можно,
        // иначе его не подорвать. Поэтому видно его всем, слой иллюзий не ставим.
        RemComp<DamageableComponent>(clone);
        RemComp<MindContainerComponent>(clone);
        RemComp<SSDIndicatorComponent>(clone);
        RemComp<InputMoverComponent>(clone);
        RemComp<MobMoverComponent>(clone);

        EnsureComp<TimedDespawnComponent>(clone).Lifetime = CloneLifetime;
        EnsureComp<CanMoveInAirComponent>(clone);

        var cloneComp = EnsureComp<HereticMoonRingleaderCloneComponent>(clone);
        cloneComp.Caster = caster;
        cloneComp.Target = target;

        var casterMeta = MetaData(caster);
        _metaData.SetEntityName(clone, casterMeta.EntityName);
        _metaData.SetEntityDescription(clone, casterMeta.EntityDescription);
    }

    private void CopyEquipment(EntityUid source, EntityUid target)
    {
        var coords = Transform(target).Coordinates;
        var enumerator = _inventory.GetSlotEnumerator(source);
        while (enumerator.NextItem(out var item, out var slot))
        {
            var protoId = MetaData(item).EntityPrototype?.ID;
            if (protoId == null) continue;
            var copy = Spawn(protoId, coords);
            _inventory.TryEquip(target, copy, slot.Name, silent: true);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // full_setup(target_mob): отражение неспешно идёт за своей жертвой.
        var query = EntityQueryEnumerator<HereticMoonRingleaderCloneComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var physics))
        {
            if (!Exists(comp.Target))
                continue;

            var clonePos = _xform.GetWorldPosition(uid);
            var targetPos = _xform.GetWorldPosition(comp.Target);
            var dir = targetPos - clonePos;

            if (dir.LengthSquared() < 1f)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                continue;
            }

            _physics.SetLinearVelocity(uid, Vector2.Normalize(dir) * CloneSpeed, body: physics);
        }
    }
}
