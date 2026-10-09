using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Armor;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Поведение штаммов блоба (antagonists/blob/blobstrains/*.dm).
/// </summary>
public sealed class BlobStrainSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly BlobStructureSystem _structure = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;
    [Dependency] private readonly BlobInfectionSystem _infection = default!;

    private static readonly ProtoId<ReagentPrototype> FrostOil = "FrostOil";
    private static readonly ProtoId<ReagentPrototype> Ice = "Ice";
    private static readonly ProtoId<ReagentPrototype> CryogenicPoison = "BlobCryogenicPoisonChem";
    private static readonly ProtoId<ReagentPrototype> RegenerativeMateria = "BlobRegenerativeMateriaChem";
    private static readonly ProtoId<ReagentPrototype> SporeToxin = "BlobSporeToxin";
    private static readonly ProtoId<ReagentPrototype> SpaceLube = "SpaceLube";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly EntProtoId SmokePrototype = "Smoke";
    private static readonly EntProtoId SparksPrototype = "EffectSparks";
    private static readonly EntProtoId ExplosionEffect = "BlobExplosionFastEffect";
    private static readonly EntProtoId DrugStatus = "StatusEffectSeeingRainbow";
    private static readonly SoundSpecifier SporeBurstSound = new SoundPathSpecifier("/Audio/Imperial/blob/blob_spore_burst.ogg");
    private static readonly SoundSpecifier PopSound = new SoundPathSpecifier("/Audio/Imperial/blob/pop_expl.ogg");
    private static readonly SoundSpecifier ExplosionSound = new SoundPathSpecifier("/Audio/Imperial/blob/explosion2.ogg");
    private static readonly SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");

    public const string DebrisContainer = "blob_debris";

    /// <summary>BLOB_REAGENTATK_VOL.</summary>
    public const float AttackVolume = 25f;

    /// <summary>BLOBMOB_BLOBBERNAUT_REAGENTATK_VOL.</summary>
    public const float BlobbernautVolume = 20f;

    /// <summary>BLOBMOB_CLOUD_REAGENT_VOLUME.</summary>
    public const float CloudVolume = 40f;

    /// <summary>reactive_spines: retaliate_cooldown на оверманда.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _retaliateCooldown = new();

    public BlobStrainPrototype? GetStrain(EntityUid? overmind)
    {
        if (overmind == null || !TryComp<BlobOvermindComponent>(overmind, out var om))
            return null;
        return _overmind.GetStrain(om);
    }

    private BlobStrainKind? Kind(EntityUid? overmind) => GetStrain(overmind)?.Kind;

    #region Ядро

    /// <summary>core_process(): очки и регенерация ядра.</summary>
    public void CoreProcess(EntityUid overmind, BlobOvermindComponent om, Entity<BlobStructureComponent> core)
    {
        var strain = _overmind.GetStrain(om);
        _overmind.AddPoints(overmind, om, om.BasePointRate + (strain?.PointRateBonus ?? 0));
        _structure.Repair(core, om.BaseCoreRegen + (strain?.CoreRegenBonus ?? 0));
    }

    public bool IgnoresShock(EntityUid? overmind) => Kind(overmind) == BlobStrainKind.EnergizedJelly;

    #endregion

    #region Атака

    /// <summary>attack_living(): удар блоба по живому существу.</summary>
    public void AttackLiving(EntityUid? overmind, EntityUid target, List<EntityUid>? nearbyBlobs)
    {
        if (GetStrain(overmind) is not { } strain)
            return;

        if (strain.Kind == BlobStrainKind.DebrisDevourer)
        {
            SendStrainMessage(strain, target);
            foreach (var blob in nearbyBlobs ?? new List<EntityUid>())
                DebrisAttack(overmind, target, blob);
            return;
        }

        ExposeMob(target, strain, AttackVolume, GetBioProtection(target), overmind);
        SendStrainMessage(strain, target);
    }

    /// <summary>blobbernaut_attack(): блоббернаут впрыскивает реагент штамма.</summary>
    public void BlobbernautAttack(EntityUid blobbernaut, EntityUid victim, BlobStrainPrototype strain, EntityUid? overmind)
    {
        if (strain.Kind == BlobStrainKind.DebrisDevourer)
        {
            DebrisAttack(overmind, victim, blobbernaut);
            return;
        }

        if (!HasComp<MobStateComponent>(victim))
            return;

        ExposeMob(victim, strain, BlobbernautVolume, GetBioProtection(victim), overmind, primary: false);
    }

    private void SendStrainMessage(BlobStrainPrototype strain, EntityUid target)
    {
        var message = Loc.GetString(strain.Message);
        if (strain.MessageLiving is { } living && !HasComp<BorgChassisComponent>(target))
            message += Loc.GetString(living);
        _popup.PopupEntity(message + "!", target, target, PopupType.LargeCaution);
    }

    /// <summary>getarmor(null, BIO): защита по сопротивлению едким веществам.</summary>
    public float GetBioProtection(EntityUid target)
    {
        var ev = new CoefficientQueryEvent(SlotFlags.WITHOUT_POCKET);
        RaiseLocalEvent(target, ev);
        var coefficient = ev.DamageModifiers.Coefficients.GetValueOrDefault("Caustic", 1f);
        return Math.Clamp(1f - coefficient, 0f, 1f);
    }

    /// <summary>return_mob_expose_reac_volume().</summary>
    private float ReactionVolume(EntityUid target, BlobStrainPrototype strain, float volume, float protection)
    {
        if (_mobState.IsDead(target) || _overmind.IsBlobAlly(target))
            return 0f;
        if (strain.Kind == BlobStrainKind.ReactiveSpines)
            return volume;
        return MathF.Round(volume * Math.Min(1.5f - protection, 1f), 1);
    }

    /// <summary>
    /// expose_mob() реагента штамма. primary — основная атака (25 ед.), для взрывной решётки даёт AoE.
    /// </summary>
    public void ExposeMob(EntityUid target, BlobStrainPrototype strain, float volume, float protection, EntityUid? overmind, bool primary = true)
    {
        if (TerminatingOrDeleted(target))
            return;

        var v = ReactionVolume(target, strain, volume, protection);
        switch (strain.Kind)
        {
            case BlobStrainKind.BlazingOil:
                _flammable.AdjustFireStacks(target, MathF.Round(v / 10f), ignite: true);
                Damage(target, Heat, 0.8f * v);
                if (HasComp<HumanoidProfileComponent>(target) && v > 0)
                    _chat.TryEmoteWithChat(target, "Scream");
                break;

            case BlobStrainKind.CryogenicPoison:
                if (v > 0)
                {
                    var solution = new Solution();
                    solution.AddReagent(FrostOil, 0.3f * v);
                    solution.AddReagent(Ice, 0.3f * v);
                    solution.AddReagent(CryogenicPoison, 0.3f * v);
                    _bloodstream.TryAddToBloodstream(target, solution);
                }
                Damage(target, Blunt, 0.2f * v);
                break;

            case BlobStrainKind.DistributedNeurons:
                Damage(target, Poison, 0.6f * v);
                if (HasComp<HumanoidProfileComponent>(target))
                {
                    if (_mobState.IsCritical(target))
                        _mobState.ChangeMobState(target, MobState.Dead);

                    // Вместо зомби-трупа — заражение (старая система зомби Imperial).
                    if (_mobState.IsDead(target) && overmind is { } om && TryComp<BlobOvermindComponent>(om, out var omComp) &&
                        omComp.Points >= 5 && _overmind.CanBuy(om, omComp, 5))
                    {
                        _infection.Infect(target, om);
                        _overmind.SendMessage(om, Loc.GetString("blob-zombification-spent", ("target", target)));
                    }
                }
                break;

            case BlobStrainKind.ElectromagneticWeb:
                if (_random.Prob(Math.Clamp(v * 2 / 100f, 0f, 1f)))
                    _emp.EmpPulse(_transform.GetMapCoordinates(target), 0.5f, 5000f, TimeSpan.FromSeconds(5));
                Damage(target, Heat, v);
                break;

            case BlobStrainKind.EnergizedJelly:
                // losebreath: пропущенные вдохи — удушье.
                Damage(target, Asphyxiation, MathF.Round(0.2f * v) + 0.6f * v);
                _stamina.TakeStaminaDamage(target, v * 1.2f);
                break;

            case BlobStrainKind.ExplosiveLattice:
                if (v > 10)
                {
                    Spawn(ExplosionEffect, Transform(target).Coordinates);
                    // Эпицентр 0.7*v, соседи 0.5*v.
                    Damage(target, Blunt, v * 0.35f);
                    Damage(target, Heat, v * 0.35f);
                    foreach (var nearby in MobsInRange(Transform(target).Coordinates, 1))
                    {
                        if (nearby == target || _overmind.IsBlobAlly(nearby))
                            continue;
                        Damage(nearby, Blunt, v * 0.25f);
                        Damage(nearby, Heat, v * 0.25f);
                    }
                }
                else
                {
                    Damage(target, Blunt, 0.6f * v);
                }
                break;

            case BlobStrainKind.NetworkedFibers:
                Damage(target, Blunt, 0.6f * v);
                Damage(target, Heat, 0.6f * v);
                break;

            case BlobStrainKind.PressurizedSlime:
                if (_random.Prob(Math.Clamp(v / 100f, 0f, 1f)))
                    Lube(Transform(target).Coordinates);
                Damage(target, Blunt, 0.4f * v);
                _stamina.TakeStaminaDamage(target, v);
                Damage(target, Asphyxiation, 0.4f * v);
                break;

            case BlobStrainKind.ReactiveSpines:
                Damage(target, Blunt, v);
                break;

            case BlobStrainKind.RegenerativeMateria:
                if (v > 0)
                {
                    _status.TryAddStatusEffectDuration(target, DrugStatus, TimeSpan.FromSeconds(v * 2));
                    var solution = new Solution();
                    solution.AddReagent(RegenerativeMateria, 0.2f * v);
                    solution.AddReagent(SporeToxin, 0.2f * v);
                    _bloodstream.TryAddToBloodstream(target, solution);
                }
                Damage(target, Poison, 0.7f * v);
                break;

            case BlobStrainKind.ReplicatingFoam:
            case BlobStrainKind.ShiftingFragments:
                Damage(target, Blunt, 0.7f * v);
                break;

            case BlobStrainKind.SynchronousMesh:
                Damage(target, Blunt, 0.2f * v);
                if (v > 0 && _structure.TryGetTile(target, out var grid, out var tile))
                {
                    foreach (var blob in _structure.BlobsInRange(grid, tile, 1))
                    {
                        _structure.AttackAnimation(blob, Transform(target).Coordinates);
                        Damage(target, Blunt, 0.3f * v);
                    }
                }
                break;
        }
    }

    private void Damage(EntityUid target, ProtoId<DamageTypePrototype> type, float amount)
    {
        _structure.Damage(target, type, amount, true);
    }

    private IEnumerable<EntityUid> MobsInRange(EntityCoordinates coords, int range)
    {
        if (coords.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            yield break;
        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var center = _map.TileIndicesFor(gridUid, gridComp, coords);
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                foreach (var mob in _structure.EntitiesOnTile(grid, center + new Vector2i(x, y)))
                {
                    if (HasComp<MobStateComponent>(mob))
                        yield return mob;
                }
            }
        }
    }

    #endregion

    #region Реакции структур

    /// <summary>damage_reaction().</summary>
    public float DamageReaction(Entity<BlobStructureComponent> blob, float damage, bool brute, BlobDamageFlag flag)
    {
        if (GetStrain(blob.Comp.Overmind) is not { } strain)
            return damage;

        var direct = flag is BlobDamageFlag.Melee or BlobDamageFlag.Bullet or BlobDamageFlag.Laser;
        var survives = blob.Comp.Integrity - damage > 0;

        switch (strain.Kind)
        {
            case BlobStrainKind.BlazingOil:
                if (!brute && flag != BlobDamageFlag.Energy && _structure.TryGetTile(blob, out var grid, out var tile))
                {
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            var other = _structure.GetBlobAt(grid, tile + new Vector2i(x, y));
                            if (other != null && Kind(Comp<BlobStructureComponent>(other.Value).Overmind) == strain.Kind)
                                continue;
                            if (_random.Prob(0.8f))
                                _atmos.HotspotExpose(grid.Owner, tile + new Vector2i(x, y), 700f, 50f, blob, true);
                        }
                    }
                }
                return flag == BlobDamageFlag.Fire ? 0 : damage;

            case BlobStrainKind.DebrisDevourer:
                return MathF.Round(Math.Max(damage - Math.Min(DebrisDensity(blob.Comp.Overmind), 10), 0));

            case BlobStrainKind.DistributedNeurons:
                if (direct && damage <= 20 && !survives && _random.Prob(0.15f))
                {
                    _popup.PopupEntity(Loc.GetString("blob-spore-floats-free"), blob, PopupType.MediumCaution);
                    _blobMob.CreateSpore(blob.Comp.Overmind, Transform(blob).Coordinates, BlobMobType.WeakSpore);
                }
                return damage;

            case BlobStrainKind.ElectromagneticWeb:
                return brute ? damage / blob.Comp.BruteResist : damage * 1.25f;

            case BlobStrainKind.EnergizedJelly:
                if (direct && !survives && _random.Prob(0.1f))
                    Sparks(Transform(blob).Coordinates);
                return damage;

            case BlobStrainKind.ExplosiveLattice:
                if (flag == BlobDamageFlag.Bomb)
                    return 0;
                return direct ? damage : damage * 1.5f;

            case BlobStrainKind.PressurizedSlime:
                if (direct || brute)
                    ExtinguishArea(blob, damage);
                return brute ? damage * 0.5f : damage;

            case BlobStrainKind.ReactiveSpines:
                if (damage > 0 && survives && blob.Comp.Overmind is { } overmind &&
                    _timing.CurTime >= _retaliateCooldown.GetValueOrDefault(overmind))
                {
                    _retaliateCooldown[overmind] = _timing.CurTime + TimeSpan.FromSeconds(2.5);
                    _popup.PopupEntity(Loc.GetString("blob-retaliates"), blob, PopupType.MediumCaution);
                    Retaliate(blob, overmind);
                }
                return damage;

            case BlobStrainKind.ReplicatingFoam:
                if (brute)
                    return damage * 2;
                if (damage > 0 && survives && _random.Prob(0.6f))
                {
                    var newBlob = _structure.Expand(blob, null, null, false);
                    if (newBlob != null && TryComp<BlobStructureComponent>(newBlob, out var newComp))
                    {
                        newComp.Integrity = blob.Comp.Integrity - damage;
                        _structure.UpdateState(newBlob.Value, newComp);
                    }
                }
                return damage;

            case BlobStrainKind.ShiftingFragments:
                if (direct && damage > 0 && survives && _random.Prob(Math.Clamp((60 - damage) / 100f, 0f, 1f)) &&
                    _structure.TryGetTile(blob, out var sGrid, out var sTile))
                {
                    var candidates = new List<EntityUid>();
                    foreach (var other in _structure.BlobsInRange(sGrid, sTile, 1, includeCenter: false))
                    {
                        var otherComp = Comp<BlobStructureComponent>(other);
                        if (Kind(otherComp.Overmind) != strain.Kind)
                            continue;
                        if (otherComp.Type == BlobStructureType.Normal ||
                            otherComp.Type is BlobStructureType.Strong or BlobStructureType.Reflective && _random.Prob(0.25f))
                            candidates.Add(other);
                    }

                    if (candidates.Count > 0)
                        _structure.SwapBlobs(_random.Pick(candidates), blob);
                }
                return damage;

            case BlobStrainKind.SynchronousMesh:
                if (direct && _structure.TryGetTile(blob, out var mGrid, out var mTile))
                {
                    var share = _structure.BlobsInRange(mGrid, mTile, 1, includeCenter: false)
                        .Where(o =>
                        {
                            var c = Comp<BlobStructureComponent>(o);
                            return !c.IgnoreSyncMesh && Kind(c.Overmind) == strain.Kind;
                        })
                        .ToList();
                    var split = 1 + share.Count;
                    foreach (var other in share)
                        _structure.TakeDamage((other, Comp<BlobStructureComponent>(other)), damage / split, brute, BlobDamageFlag.None, false);
                    return damage / split;
                }
                return damage * 1.25f;
        }

        return damage;
    }

    /// <summary>reactive_spines: удар по всему вокруг.</summary>
    private void Retaliate(Entity<BlobStructureComponent> blob, EntityUid overmind)
    {
        if (!_structure.TryGetTile(blob, out var grid, out var tile))
            return;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var coords = _structure.TileCenter(grid, tile + new Vector2i(x, y));
                foreach (var thing in _structure.EntitiesOnTile(grid, tile + new Vector2i(x, y)))
                {
                    if (TerminatingOrDeleted(thing) || HasComp<BlobStructureComponent>(thing))
                        continue;

                    if (HasComp<MobStateComponent>(thing) && !_overmind.IsBlobAlly(thing))
                    {
                        _structure.AttackAnimation(blob, coords, overmind);
                        AttackLiving(overmind, thing, null);
                    }
                    else if (_structure.BlobAct(thing, blob))
                    {
                        _structure.AttackAnimation(blob, coords, overmind);
                    }
                }
            }
        }
    }

    /// <summary>death_reaction().</summary>
    public void DeathReaction(Entity<BlobStructureComponent> blob, BlobDamageFlag flag)
    {
        var direct = flag is BlobDamageFlag.Melee or BlobDamageFlag.Bullet or BlobDamageFlag.Laser;
        switch (Kind(blob.Comp.Overmind))
        {
            case BlobStrainKind.ElectromagneticWeb when direct:
                _emp.EmpPulse(_transform.GetMapCoordinates(blob), 3f, 10000f, TimeSpan.FromSeconds(10));
                break;
            case BlobStrainKind.PressurizedSlime when direct:
                _popup.PopupEntity(Loc.GetString("blob-ruptures"), blob, PopupType.MediumCaution);
                ExtinguishArea(blob, 50);
                break;
        }
    }

    /// <summary>expand_reaction().</summary>
    public void ExpandReaction(Entity<BlobStructureComponent> source, Entity<BlobStructureComponent> newBlob, Entity<MapGridComponent> grid, Vector2i tile, EntityUid? controller)
    {
        if (GetStrain(newBlob.Comp.Overmind) is not { } strain)
            return;

        switch (strain.Kind)
        {
            case BlobStrainKind.DebrisDevourer:
                if (TryComp<BlobOvermindComponent>(newBlob.Comp.Overmind, out var om) && om.Core is { } core)
                {
                    var container = _container.EnsureContainer<Container>(core, DebrisContainer);
                    foreach (var item in _structure.EntitiesOnTile(grid, tile))
                    {
                        if (HasComp<ItemComponent>(item) && !TerminatingOrDeleted(item))
                            _container.Insert(item, container);
                    }
                }
                break;

            case BlobStrainKind.NetworkedFibers:
                NetworkedFibersExpand(newBlob, grid, tile, controller);
                break;

            case BlobStrainKind.ReplicatingFoam:
                if (_random.Prob(0.3f))
                    _structure.Expand(newBlob, null, null, false);
                break;

            case BlobStrainKind.ShiftingFragments:
                if (source.Comp.Type is BlobStructureType.Normal or BlobStructureType.Strong or BlobStructureType.Reflective)
                    _structure.SwapBlobs(newBlob, source);
                break;
        }
    }

    /// <summary>networked_fibers: растёт только вручную, двигая ядро или узел.</summary>
    private void NetworkedFibersExpand(Entity<BlobStructureComponent> newBlob, Entity<MapGridComponent> grid, Vector2i tile, EntityUid? controller)
    {
        if (controller == null && newBlob.Comp.Overmind is { } owner && TryComp<BlobOvermindComponent>(owner, out var ownerComp))
        {
            _overmind.AddPoints(owner, ownerComp, 1);
            QueueDel(newBlob);
            return;
        }

        if (controller == null || !TryComp<BlobOvermindComponent>(controller, out var om))
            return;

        foreach (var expander in _structure.BlobsInRange(grid, tile, 1, includeCenter: false))
        {
            var comp = Comp<BlobStructureComponent>(expander);
            if (comp.Overmind != controller || comp.Type is not (BlobStructureType.Core or BlobStructureType.Node))
                continue;
            _structure.SwapBlobs(newBlob, expander);
            return;
        }

        _overmind.AddPoints(controller.Value, om, 4);
        QueueDel(newBlob);
    }

    /// <summary>emp_reaction().</summary>
    public void EmpReaction(Entity<BlobStructureComponent> blob)
    {
        if (Kind(blob.Comp.Overmind) != BlobStrainKind.EnergizedJelly)
            return;

        // severity EMP_HEAVY = 1.
        var damage = _random.Next(30, 51) - _random.Next(10, 16);
        _structure.TakeDamage(blob, damage, false, BlobDamageFlag.Energy);
    }

    /// <summary>extinguish_reaction().</summary>
    public void ExtinguishReaction(Entity<BlobStructureComponent> blob)
    {
        if (Kind(blob.Comp.Overmind) == BlobStrainKind.BlazingOil)
            _structure.TakeDamage(blob, 4.5f, false, BlobDamageFlag.Energy);
    }

    /// <summary>pressurized_slime extinguisharea(): смазка и тушение вокруг.</summary>
    private void ExtinguishArea(Entity<BlobStructureComponent> blob, float probability)
    {
        if (!_structure.TryGetTile(blob, out var grid, out var tile))
            return;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!_random.Prob(Math.Clamp(probability / 100f, 0f, 1f)))
                    continue;
                var target = tile + new Vector2i(x, y);
                Lube(_structure.TileCenter(grid, target));
                foreach (var thing in _structure.EntitiesOnTile(grid, target))
                {
                    if (TryComp<FlammableComponent>(thing, out var flammable))
                        _flammable.Extinguish(thing, flammable);
                }
            }
        }
    }

    /// <summary>MakeSlippery(TURF_WET_LUBE).</summary>
    private void Lube(EntityCoordinates coords)
    {
        var solution = new Solution();
        solution.AddReagent(SpaceLube, 5);
        _puddle.TrySpillAt(coords, solution, out _, false);
    }

    private void Sparks(EntityCoordinates coords)
    {
        Spawn(SparksPrototype, coords);
        _audio.PlayPvs(SparkSound, coords);
    }

    #endregion

    #region Пожиратель обломков

    /// <summary>DEBRIS_DENSITY: предметов в ядре на четверть легитимных блобов.</summary>
    private float DebrisDensity(EntityUid? overmind)
    {
        if (overmind == null || !TryComp<BlobOvermindComponent>(overmind, out var om) || om.Core is not { } core ||
            !_container.TryGetContainer(core, DebrisContainer, out var container))
            return 0;
        var legit = Math.Max(om.BlobsLegit.Count, 1);
        return container.ContainedEntities.Count / (legit * 0.25f);
    }

    /// <summary>debris_attack(): метнуть поглощённый предмет в цель.</summary>
    private void DebrisAttack(EntityUid? overmind, EntityUid target, EntityUid source)
    {
        var chance = overmind != null ? 40 * DebrisDensity(overmind) : 80;
        if (!_random.Prob(Math.Clamp(chance / 100f, 0f, 1f)))
            return;

        var items = DebrisSource(overmind, source);
        if (items.Count == 0)
            return;

        var item = _random.Pick(items);
        if (TerminatingOrDeleted(item))
            return;

        _container.TryRemoveFromContainer(item, true);
        _transform.SetCoordinates(item, Transform(source).Coordinates);
        _throwing.TryThrow(item, Transform(target).Coordinates, 5f);
    }

    private List<EntityUid> DebrisSource(EntityUid? overmind, EntityUid fallback)
    {
        if (overmind != null && TryComp<BlobOvermindComponent>(overmind, out var om) && om.Core is { } core &&
            _container.TryGetContainer(core, DebrisContainer, out var coreContainer))
            return coreContainer.ContainedEntities.ToList();

        return _container.TryGetContainer(fallback, DebrisContainer, out var own) ? own.ContainedEntities.ToList() : new List<EntityUid>();
    }

    #endregion

    #region Облако споры

    /// <summary>on_sporedeath(): облако, взрыв или шрапнель из мусора.</summary>
    public void OnSporeDeath(EntityUid mob, BlobStrainPrototype? strain, EntityUid? overmind, int cloudSize)
    {
        var coords = Transform(mob).Coordinates;
        if (strain == null)
        {
            SporeCloud(coords, "BlobSporeToxin", cloudSize);
            _audio.PlayPvs(SporeBurstSound, coords);
            return;
        }

        switch (strain.Kind)
        {
            case BlobStrainKind.DebrisDevourer:
            {
                var items = DebrisSource(overmind, mob);
                var count = overmind != null ? 3 : items.Count;
                for (var i = 0; i < count && items.Count > 0; i++)
                {
                    var item = _random.PickAndTake(items);
                    if (TerminatingOrDeleted(item))
                        continue;
                    _container.TryRemoveFromContainer(item, true);
                    _transform.SetCoordinates(item, coords);
                    var dir = new Angle(_random.NextFloat() * MathF.Tau).ToVec() * 6;
                    _throwing.TryThrow(item, coords.Offset(dir), 5f);
                }

                _audio.PlayPvs(PopSound, coords, AudioParams.Default.WithVariation(0.125f));
                break;
            }

            case BlobStrainKind.ExplosiveLattice:
                Spawn(ExplosionEffect, coords);
                foreach (var actor in MobsInRange(coords, cloudSize))
                {
                    if (_overmind.IsBlobAlly(actor))
                        continue;
                    var dist = Math.Max(1f, (_transform.GetMapCoordinates(actor).Position - _transform.ToMapCoordinates(coords).Position).Length());
                    var total = (10 + 10 * cloudSize) / MathF.Round(dist);
                    Damage(actor, Blunt, total / 2);
                    Damage(actor, Heat, total / 2);
                }

                _audio.PlayPvs(ExplosionSound, coords, AudioParams.Default.WithVolume(BlobStructureSystem.Db(20 + 20 * cloudSize)).WithVariation(0.125f));
                break;

            default:
                SporeCloud(coords, strain.Reagent, cloudSize);
                _audio.PlayPvs(SporeBurstSound, coords, AudioParams.Default.WithVariation(0.125f));
                break;
        }
    }

    /// <summary>do_chem_smoke(range, 40 ед. реагента).</summary>
    private void SporeCloud(EntityCoordinates coords, string reagent, int range)
    {
        var smoke = Spawn(SmokePrototype, coords);
        var solution = new Solution();
        solution.AddReagent(reagent, CloudVolume);
        var spread = range <= 0 ? 1 : (2 * range + 1) * (2 * range + 1);
        _smoke.StartSmoke(smoke, solution, 10f, spread);
    }

    #endregion
}

/// <summary>Реагент штамма при контакте (облако спор) действует как атака блоба.</summary>
public sealed class BlobStrainExposureEffectSystem : EntityEffectSystem<MobStateComponent, BlobStrainExposure>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BlobStrainSystem _strain = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<BlobStrainExposure> args)
    {
        if (!_proto.TryIndex(args.Effect.Strain, out var strain))
            return;

        _strain.ExposeMob(entity, strain, args.Scale, _strain.GetBioProtection(entity), null, primary: false);
    }
}
