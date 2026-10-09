using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Imperial.Blob.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Administration.Systems;
using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Rotation;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Миньоны блоба (basic/blob_minions/*.dm, datum/component/blob_minion).
/// </summary>
public sealed class BlobMobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly BlobStructureSystem _structure = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;
    [Dependency] private readonly BlobStrainSystem _strain = default!;
    [Dependency] private readonly BlobInfectionSystem _infection = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly Content.Server.NPC.Systems.NPCSystem _npc = default!;

    private static readonly EntProtoId SporePrototype = "MobBlobSpore";
    private static readonly EntProtoId WeakSporePrototype = "MobBlobSporeWeak";
    private static readonly EntProtoId IndependentSporePrototype = "MobBlobSporeIndependent";
    private static readonly EntProtoId HealEffect = "BlobHealEffect";
    private static readonly EntProtoId ProduceEffect = "BlobbernautProduceEffect";
    private static readonly EntProtoId DeathEffect = "BlobbernautDeathEffect";
    private static readonly EntProtoId VeinsFlashEffect = "BlobbernautVeinsFlashEffect";
    private static readonly SoundSpecifier EatSound = new SoundPathSpecifier("/Audio/Imperial/blob/eatfood.ogg");
    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<AlertPrototype> NoFactoryAlert = "BlobbernautNoFactory";
    private static readonly SoundSpecifier BlobbernautDeathSound = new SoundPathSpecifier("/Audio/Imperial/blob/blobbernaut_death.ogg");
    private static readonly SoundSpecifier SearSound = new SoundPathSpecifier("/Audio/Imperial/blob/sear.ogg");
    private static readonly SoundSpecifier BlobAttackSound = new SoundPathSpecifier("/Audio/Imperial/blob/blobattack.ogg");
    private static readonly SoundSpecifier AttackBlobSound = new SoundPathSpecifier("/Audio/Imperial/blob/attackblob.ogg");

    /// <summary>BLOBMOB_HEALING_MULTIPLIER.</summary>
    private const float HealingMultiplier = 0.0125f;

    /// <summary>BLOBMOB_BLOBBERNAUT_HEALING_CORE / NODE / HEALTH_DECAY (за секунду, Life — 2 с).</summary>
    private const float HealingCore = 0.05f;
    private const float HealingNode = 0.025f;
    private const float HealthDecay = 0.0125f;
    private static readonly TimeSpan LifeInterval = TimeSpan.FromSeconds(2);

    private readonly List<(EntityUid, BlobMobComponent)> _mobBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobMobComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlobMobComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobMobComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BlobMobComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<BlobMobComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BlobMobComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<BlobMobComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<BlobMobComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BlobMobComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
    }

    private void OnMapInit(Entity<BlobMobComponent> ent, ref MapInitEvent args)
    {
        _faction.AddFaction(ent.Owner, BlobOvermindSystem.BlobFaction);
        ent.Comp.NextLife = _timing.CurTime + LifeInterval;
        UpdateVisuals(ent);
    }

    #region Связь с овермандом

    /// <summary>create_spore().</summary>
    public EntityUid CreateSpore(EntityUid? overmind, EntityCoordinates coords, BlobMobType type)
    {
        var proto = type switch
        {
            BlobMobType.WeakSpore => WeakSporePrototype,
            BlobMobType.IndependentSpore => IndependentSporePrototype,
            _ => SporePrototype,
        };

        var spore = Spawn(proto, coords);
        if (overmind != null)
            RegisterOvermind(spore, overmind.Value);
        return spore;
    }

    /// <summary>register_overlord().</summary>
    public void RegisterOvermind(EntityUid mob, EntityUid overmind)
    {
        if (!TryComp<BlobMobComponent>(mob, out var comp) || !TryComp<BlobOvermindComponent>(overmind, out var om))
            return;

        comp.Overmind = overmind;
        comp.Strain = om.Strain;
        om.Mobs.Add(mob);
        comp.OvermindCoreHealth = om.CoreHealth;
        Dirty(mob, comp);
        OnStrainChanged(mob);
    }

    /// <summary>overmind_deleted().</summary>
    public void OnOvermindDeleted(EntityUid mob, EntityUid overmind)
    {
        if (!TryComp<BlobMobComponent>(mob, out var comp) || comp.Overmind != overmind)
            return;
        comp.Overmind = null;
        comp.Strain = null;
        comp.OvermindCoreHealth = -1;
        Dirty(mob, comp);
        OnStrainChanged(mob);
    }

    /// <summary>strain_properties_changed() / on_strain_updated().</summary>
    public void OnStrainChanged(EntityUid mob)
    {
        if (!TryComp<BlobMobComponent>(mob, out var comp))
            return;

        if (comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
            comp.Strain = om.Strain;

        // spore/minion: при distributed neurons спорами фабрики управляют призраки.
        if (comp.Type == BlobMobType.Spore && comp.FactoryBound)
        {
            var neurons = GetStrain(comp)?.Kind == BlobStrainKind.DistributedNeurons;
            if (neurons && !HasComp<ActorComponent>(mob))
            {
                var role = EnsureComp<GhostRoleComponent>(mob);
                role.RoleName = Loc.GetString("blob-ghost-role-spore-name");
                role.RoleDescription = Loc.GetString("blob-ghost-role-spore-desc");
                role.RoleRules = Loc.GetString("ghost-role-information-antagonist-rules");
                EnsureComp<GhostTakeoverAvailableComponent>(mob);
            }
            else if (!neurons)
            {
                RemComp<GhostTakeoverAvailableComponent>(mob);
                RemComp<GhostRoleComponent>(mob);
            }
        }

        if (GetStrain(comp) is { } strain && TryComp<ActorComponent>(mob, out _))
        {
            var desc = strain.ShortDesc is { } shortDesc ? Loc.GetString(shortDesc) : Loc.GetString(strain.Description);
            _overmind.SendMessage(mob, Loc.GetString("blob-minion-strain-now", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name))));
            _overmind.SendMessage(mob, Loc.GetString("blob-strain-desc", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name)), ("desc", desc)));
        }

        UpdateVisuals((mob, comp));
    }

    private BlobStrainPrototype? GetStrain(BlobMobComponent comp) =>
        comp.Strain is { } id && _proto.TryIndex(id, out var strain) ? strain : null;

    private void UpdateVisuals(Entity<BlobMobComponent> ent)
    {
        var strain = GetStrain(ent.Comp);
        var alive = !_mobState.IsDead(ent);
        _appearance.SetData(ent, BlobVisuals.Color, strain?.Color ?? Color.White);
        _appearance.SetData(ent, BlobVisuals.Alive, alive);

        if (ent.Comp.Type == BlobMobType.Blobbernaut)
        {
            // Без штамма вены «траурно-аметистовые», глаза белые.
            _appearance.SetData(ent, BlobVisuals.SecondaryColor, strain?.ComplementaryColor ?? Color.FromHex("#7d6eb4"));
            _appearance.SetData(ent, BlobVisuals.EyesColor, strain?.ComplementaryColor ?? Color.White);
        }
    }

    private void OnMindAdded(Entity<BlobMobComponent> ent, ref MindAddedMessage args)
    {
        // Становится миньоном блоба.
        if (ent.Comp.Overmind == null)
            return;
        _overmind.SendMessage(ent, Loc.GetString("blob-minion-objective"));
    }

    #endregion

    #region Фабрика

    /// <summary>on_factory_destroyed().</summary>
    public void OnFactoryDestroyed(EntityUid mob)
    {
        if (TerminatingOrDeleted(mob) || !TryComp<BlobMobComponent>(mob, out var comp))
            return;

        comp.Factory = null;
        _popup.PopupEntity(Loc.GetString("blob-factory-destroyed-dying"), mob, mob, PopupType.LargeCaution);
        if (comp.Type == BlobMobType.Blobbernaut)
        {
            comp.Orphaned = true;
            _alerts.ShowAlert(mob, NoFactoryAlert);
            return;
        }

        Kill(mob);
    }

    /// <summary>pick_blobbernaut_candidate(): 20 секунд опроса призраков.</summary>
    public void StartBlobbernautPoll(EntityUid overmind, Entity<BlobStructureComponent> factory)
    {
        if (!TryComp<BlobOvermindComponent>(overmind, out var om))
            return;

        var spawner = Spawn(om.BlobbernautSpawnerPrototype, Transform(factory).Coordinates);
        var comp = EnsureComp<BlobbernautSpawnerComponent>(spawner);
        comp.Overmind = overmind;
        comp.Factory = factory;
        comp.Expire = _timing.CurTime + TimeSpan.FromSeconds(20);
    }

    private void OnSpawnerUsed(Entity<BlobMobComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<BlobbernautSpawnerComponent>(args.Spawner, out var spawner))
            return;

        if (spawner.Overmind is { } overmind && !TerminatingOrDeleted(overmind))
            RegisterOvermind(ent, overmind);

        if (spawner.Factory is { } factory && TryComp<BlobStructureComponent>(factory, out var factoryComp))
        {
            ent.Comp.Factory = factory;
            _structure.AssignBlobbernaut((factory, factoryComp), ent);
        }

        // assign_key(): начинает раненым, чтобы не убегал от блоба.
        if (_thresholds.TryGetDeadThreshold(ent, out var max) && max != null)
            _structure.Damage(ent, "Blunt", (float) max.Value / 2, true);

        // flick("blobbernaut_produce").
        _structure.SpawnTinted(ProduceEffect, Transform(ent).Coordinates, GetStrain(ent.Comp)?.Color ?? Color.White);
        spawner.Overmind = null;
        spawner.Factory = null;
        QueueDel(args.Spawner);

        Timer.Spawn(TimeSpan.FromSeconds(0.5), () => GreetBlobbernaut(ent));
    }

    private void GreetBlobbernaut(EntityUid naut)
    {
        if (TerminatingOrDeleted(naut) || !TryComp<BlobMobComponent>(naut, out var comp))
            return;

        _audio.PlayEntity(BlobAttackSound, naut, naut);
        _audio.PlayEntity(AttackBlobSound, naut, naut);
        _overmind.SendMessage(naut, Loc.GetString("blob-blobbernaut-greet-1"));
        _overmind.SendMessage(naut, Loc.GetString("blob-blobbernaut-greet-2"));
        if (GetStrain(comp) is { } strain)
        {
            var desc = strain.ShortDesc is { } shortDesc ? Loc.GetString(shortDesc) : Loc.GetString(strain.Description);
            _overmind.SendMessage(naut, Loc.GetString("blob-blobbernaut-greet-strain", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name))));
            _overmind.SendMessage(naut, Loc.GetString("blob-strain-desc", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name)), ("desc", desc)));
        }
    }

    #endregion

    #region Жизнь

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var spawners = EntityQueryEnumerator<BlobbernautSpawnerComponent>();
        while (spawners.MoveNext(out var uid, out var spawner))
        {
            if (now < spawner.Expire)
                continue;

            // on_poll_concluded(null): очки возвращаются.
            if (spawner.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
            {
                _overmind.SendMessage(overmind, Loc.GetString("blob-blobbernaut-no-ghost"));
                _overmind.AddPoints(overmind, om, om.BlobbernautCost);
            }

            if (spawner.Factory is { } factory && TryComp<BlobStructureComponent>(factory, out var factoryComp))
                _structure.AssignBlobbernaut((factory, factoryComp), null);
            QueueDel(uid);
        }

        // Снимок: заражение и смерть создают и удаляют миньонов прямо во время обхода.
        _mobBuffer.Clear();
        var mobs = EntityQueryEnumerator<BlobMobComponent>();
        while (mobs.MoveNext(out var mobUid, out var mobComp))
            _mobBuffer.Add((mobUid, mobComp));

        foreach (var (uid, comp) in _mobBuffer)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (now < comp.NextLife || _mobState.IsDead(uid))
                continue;
            comp.NextLife = now + LifeInterval;

            switch (comp.Type)
            {
                case BlobMobType.Blobbernaut:
                    if (comp.FactoryBound)
                        BlobbernautLife((uid, comp));
                    break;
            }
        }
    }

    /// <summary>blobbernaut/minion/Life(): лечится у ядра и узлов, умирает вдали от блоба или без фабрики.</summary>
    private void BlobbernautLife(Entity<BlobMobComponent> ent)
    {
        if (!_thresholds.TryGetDeadThreshold(ent, out var maxHealth) || maxHealth == null)
            return;
        var max = (float) maxHealth.Value;
        var seconds = (float) LifeInterval.TotalSeconds;

        var damageSources = 0;
        var nearCore = false;
        var nearNode = false;
        var nearBlob = false;
        if (_structure.TryGetTile(ent, out var grid, out var tile))
        {
            foreach (var blob in _structure.BlobsInRange(grid, tile, 2))
            {
                nearBlob = true;
                var type = Comp<BlobStructureComponent>(blob).Type;
                nearCore |= type == BlobStructureType.Core;
                nearNode |= type == BlobStructureType.Node;
            }
        }

        if (!nearBlob)
            damageSources++;

        if (ent.Comp.Orphaned)
        {
            damageSources++;
        }
        else
        {
            var color = GetStrain(ent.Comp)?.Color ?? Color.Black;
            if (nearCore)
            {
                HealBrute(ent, max * HealingCore * seconds);
                SpawnHeal(ent, color);
            }

            if (nearNode)
            {
                HealBrute(ent, max * HealingNode * seconds);
                SpawnHeal(ent, color);
            }
        }

        if (damageSources == 0)
            return;

        _structure.Damage(ent, Poison, max * HealthDecay * damageSources * seconds, true);

        // Вены мигают цветом, сдвинутым по оттенку на 180°.
        var veins = GetStrain(ent.Comp)?.ComplementaryColor ?? Color.FromHex("#7d6eb4");
        var hsv = Color.ToHsv(veins);
        hsv.X = (hsv.X + 0.5f) % 1f;
        var flash = Spawn(VeinsFlashEffect, new EntityCoordinates(ent, 0, 0));
        _appearance.SetData(flash, BlobVisuals.Color, Color.FromHsv(hsv));
        if (_random.Prob(0.2f))
            _audio.PlayPvs(SearSound, ent, AudioParams.Default.WithVolume(BlobStructureSystem.Db(5)).WithVariation(0.125f));
    }

    /// <summary>heal_overall_damage(brute).</summary>
    private void HealBrute(EntityUid uid, float amount)
    {
        if (amount <= 0)
            return;
        _damageable.HealDistributed(uid, -FixedPoint2.New(amount), Brute);
    }

    private void SpawnHeal(EntityUid uid, Color color)
    {
        var offset = new System.Numerics.Vector2(_random.NextFloat(-0.375f, 0.375f), _random.NextFloat(-0.28f, 0f));
        _structure.SpawnTinted(HealEffect, Transform(uid).Coordinates.Offset(offset), color);
    }

    /// <summary>on_blob_touched(): касание блоба лечит миньона.</summary>
    public bool HealFromBlob(EntityUid mob)
    {
        if (_mobState.IsDead(mob) || !TryComp<DamageableComponent>(mob, out var damageable) ||
            _damageable.GetTotalDamage((mob, damageable)) <= 0 ||
            !_thresholds.TryGetDeadThreshold(mob, out var max) || max == null)
            return false;

        var color = TryComp<BlobMobComponent>(mob, out var comp) && comp.Overmind is { } overmind &&
                    TryComp<BlobOvermindComponent>(overmind, out var om) && _overmind.GetStrain(om) is { } strain
            ? strain.ComplementaryColor
            : Color.Black;
        SpawnHeal(mob, color);
        SpawnHeal(mob, color);
        HealBrute(mob, (float) max.Value * HealingMultiplier);
        return false;
    }

    public void FullyHeal(EntityUid mob)
    {
        _rejuvenate.PerformRejuvenate(mob);
    }

    private void Kill(EntityUid mob)
    {
        if (!_mobState.IsDead(mob))
            _mobState.ChangeMobState(mob, MobState.Dead);
    }

    #endregion

    #region Бой

    /// <summary>melee_damage_lower/upper и obj_damage.</summary>
    private void OnGetMeleeDamage(Entity<BlobMobComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon != ent.Owner)
            return;

        var hasStrain = GetStrain(ent.Comp) != null;
        var (lower, upper, obj) = ent.Comp.Type switch
        {
            BlobMobType.Spore or BlobMobType.IndependentSpore => (4, 8, 10),
            BlobMobType.WeakSpore => (2, 4, 0),
            BlobMobType.Blobbernaut => hasStrain ? (4, 4, 60) : (20, 20, 60),
            _ => (4, 8, 10),
        };

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = _random.Next(lower, upper + 1);
        if (obj > 0)
            damage.DamageDict["Structural"] = obj;
        args.Damage = damage;
    }

    private void OnMeleeHit(Entity<BlobMobComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner)
            return;

        foreach (var victim in args.HitEntities)
        {
            if (TryEatDebris(ent, victim))
                return;

            // Спора под управлением игрока забирается на голову жертвы в крите ударом.
            if (TryComp<BlobSporeLatchComponent>(ent, out var latch) && _infection.TryStartLatch((ent, latch), victim))
                return;

            switch (ent.Comp.Type)
            {
                case BlobMobType.Blobbernaut:
                    if (GetStrain(ent.Comp) is { } strain)
                        _strain.BlobbernautAttack(ent, victim, strain, ent.Comp.Overmind);
                    break;
            }
        }
    }

    /// <summary>Блоббернаут: brute 0.5 и порог урона 10 (damage_threshold).</summary>
    private void OnDamageModify(Entity<BlobMobComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Type != BlobMobType.Blobbernaut || args.Damage.GetTotal() <= 0)
            return;

        if (args.Damage.GetTotal() < 10)
            args.Damage = new DamageSpecifier();
    }

    #endregion

    /// <summary>debris_devourer on_blobmob_atom_interacted(): миньон глотает предметы.</summary>
    private void OnBeforeInteractHand(Entity<BlobMobComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (!args.Handled && TryEatDebris(ent, args.Target))
            args.Handled = true;
    }

    private bool TryEatDebris(Entity<BlobMobComponent> ent, EntityUid target)
    {
        if (GetStrain(ent.Comp)?.Kind != BlobStrainKind.DebrisDevourer || !HasComp<ItemComponent>(target) ||
            _container.IsEntityInContainer(target) ||
            !_transform.InRange(Transform(ent).Coordinates, Transform(target).Coordinates, 1.5f))
            return false;

        // mob_size * 5: блоббернаут крупный (15), остальные — 10.
        var own = _container.EnsureContainer<Container>(ent, BlobStrainSystem.DebrisContainer);
        var limit = ent.Comp.Type == BlobMobType.Blobbernaut ? 15 : 10;
        if (own.ContainedEntities.Count >= limit)
        {
            _popup.PopupEntity(Loc.GetString("blob-minion-too-full"), ent, ent);
            return true;
        }

        _audio.PlayPvs(EatSound, ent, AudioParams.Default.WithVolume(BlobStructureSystem.Db(60)).WithVariation(0.125f));
        var container = own;
        if (ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om) && om.Core is { } core)
            container = _container.EnsureContainer<Container>(core, BlobStrainSystem.DebrisContainer);
        _container.Insert(target, container);
        return true;
    }

    #region Смерть

    private void OnMobStateChanged(Entity<BlobMobComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateVisuals(ent);
        if (args.NewMobState != MobState.Dead)
            return;

        // on_death(): облако или реакция штамма.
        if (ent.Comp.DeathCloudSize >= 0)
            _strain.OnSporeDeath(ent, GetStrain(ent.Comp), ent.Comp.Overmind, ent.Comp.DeathCloudSize);

        if (ent.Comp.Factory is { } factory)
        {
            _structure.OnFactoryMobDied(factory, ent);
            if (ent.Comp.Type == BlobMobType.Blobbernaut)
                _structure.OnBlobbernautDied(factory, ent);
        }

        switch (ent.Comp.Type)
        {
            case BlobMobType.Blobbernaut:
                _structure.SpawnTinted(DeathEffect, Transform(ent).Coordinates, GetStrain(ent.Comp)?.Color ?? Color.White);
                _audio.PlayPvs(BlobbernautDeathSound, ent, AudioParams.Default.WithVariation(0.125f));
                break;
            default:
                // DEL_ON_DEATH: «explodes into a cloud of gas!»
                _popup.PopupEntity(Loc.GetString("blob-spore-explodes", ("spore", ent.Owner)), ent, PopupType.Medium);
                QueueDel(ent);
                break;
        }
    }

    private void OnShutdown(Entity<BlobMobComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Factory is { } factory)
        {
            _structure.OnFactoryMobRemoved(factory, ent);
            if (ent.Comp.Type == BlobMobType.Blobbernaut)
                _structure.OnBlobbernautDied(factory, ent);
        }

        if (ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
            om.Mobs.Remove(ent);

    }

    #endregion
}
