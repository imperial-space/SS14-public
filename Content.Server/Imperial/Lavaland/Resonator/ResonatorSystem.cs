using Content.Server.Destructible;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland.Resonator;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Resonator;

public sealed class ResonatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string FieldPrototype = "ResonanceField";
    private const string FieldMatrixPrototype = "ResonanceFieldMatrix";
    private const string CrushPrototype = "ResonanceCrush";
    private const string FireSound = "/Audio/Imperial/Lavaland/sound_items_weapons_resonator_fire.ogg";
    private const string BlastSound = "/Audio/Imperial/Lavaland/sound_items_weapons_resonator_blast.ogg";
    private const float BurstDamage = 20f;
    private const float SlowWalk = 0.5f;
    private const float SlowSprint = 0.5f;
    private static readonly TimeSpan SlowDuration = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResonatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ResonatorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ResonanceFieldComponent, ComponentShutdown>(OnFieldShutdown);
    }

    private void OnUseInHand(EntityUid uid, ResonatorComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (comp.CanMatrix)
        {
            comp.Mode = comp.Mode switch
            {
                ResonatorMode.Auto => ResonatorMode.Manual,
                ResonatorMode.Manual => ResonatorMode.Matrix,
                _ => ResonatorMode.Auto,
            };
        }
        else
        {
            comp.Mode = comp.Mode == ResonatorMode.Auto ? ResonatorMode.Manual : ResonatorMode.Auto;
        }

        var modeKey = comp.Mode switch
        {
            ResonatorMode.Manual => "resonator-mode-manual",
            ResonatorMode.Matrix => "resonator-mode-matrix",
            _ => "resonator-mode-auto",
        };
        _popup.PopupEntity(Loc.GetString(modeKey), args.User, args.User, PopupType.Small);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, ResonatorComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var targetCoords = args.ClickLocation;
        var mapCoords = _xform.ToMapCoordinates(targetCoords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        // Check if there's already a field at target location → burst it
        var fields = _lookup.GetEntitiesInRange<ResonanceFieldComponent>(mapCoords, 0.6f);
        foreach (var (fieldUid, fieldComp) in fields)
        {
            fieldComp.DamageMultiplier = comp.QuickBurstMod;
            BurstField(fieldUid, fieldComp);
            args.Handled = true;
            return;
        }

        // Spawn new field if under limit
        if (comp.Fields.Count >= comp.FieldLimit)
        {
            args.Handled = true;
            return;
        }

        var proto = comp.Mode == ResonatorMode.Matrix ? FieldMatrixPrototype : FieldPrototype;
        var fieldEnt = Spawn(proto, targetCoords);
        var newField = EnsureComp<ResonanceFieldComponent>(fieldEnt);
        newField.Creator = args.User;
        newField.ParentResonator = uid;
        newField.AddingFailure = comp.AddingFailure;
        newField.Mode = comp.Mode;
        newField.Duration = comp.Mode == ResonatorMode.Auto ? 2f : 60f;
        newField.Timer = newField.Duration;

        comp.Fields.Add(fieldEnt);

        _audio.PlayPvs(FireSound, fieldEnt);
        args.Handled = true;
    }

    private void OnFieldShutdown(EntityUid uid, ResonanceFieldComponent comp, ComponentShutdown args)
    {
        if (comp.ParentResonator is { } resonatorUid && TryComp<ResonatorComponent>(resonatorUid, out var resonator))
            resonator.Fields.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ResonanceFieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Rupturing)
                continue;

            if (comp.Mode == ResonatorMode.Matrix)
            {
                // Matrix mode: trigger on entity entry — handled via trigger check here
                var mapCoords = _xform.ToMapCoordinates(Transform(uid).Coordinates);
                var mobs = _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords, 0.4f);
                bool hasMob = false;
                foreach (var (mobUid, _) in mobs)
                {
                    if (mobUid != comp.Creator)
                    {
                        hasMob = true;
                        break;
                    }
                }
                if (hasMob)
                {
                    BurstField(uid, comp);
                    continue;
                }
            }

            comp.Timer -= frameTime;
            if (comp.Timer <= 0f)
                BurstField(uid, comp);
        }
    }

    /// <summary>
    /// modkit/resonator_blasts: подрывает поле на клетке с множителем урона или ставит новое ручное поле.
    /// </summary>
    public void KineticResonance(EntityCoordinates coords, EntityUid? creator, float multiplier)
    {
        var mapCoords = _xform.ToMapCoordinates(coords);
        foreach (var (fieldUid, field) in _lookup.GetEntitiesInRange<ResonanceFieldComponent>(mapCoords, 0.4f))
        {
            if (field.Rupturing)
                continue;
            field.DamageMultiplier = multiplier;
            BurstField(fieldUid, field);
            return;
        }

        var fieldEnt = Spawn(FieldPrototype, coords);
        var newField = EnsureComp<ResonanceFieldComponent>(fieldEnt);
        newField.Creator = creator;
        newField.Mode = ResonatorMode.Manual;
        newField.Duration = 10f;
        newField.Timer = newField.Duration;
        _audio.PlayPvs(FireSound, fieldEnt);
    }

    private void BurstField(EntityUid uid, ResonanceFieldComponent comp)
    {
        if (comp.Rupturing)
            return;
        comp.Rupturing = true;

        var coords = Transform(uid).Coordinates;
        var mapCoords = _xform.ToMapCoordinates(coords);

        // Visual crush effect
        Spawn(CrushPrototype, coords);

        // Destroy any ore vein entity on this tile
        var oreVeins = _lookup.GetEntitiesInRange<OreVeinComponent>(mapCoords, 0.6f);
        foreach (var (oreUid, _) in oreVeins)
            _destructible.DestroyEntity(oreUid);

        // Calculate damage (×3 on lavaland low pressure)
        var damage = BurstDamage * comp.DamageMultiplier;
        if (IsLavaland(mapCoords))
            damage *= 3f;

        // Damage mobs on the tile
        var dmgSpec = new DamageSpecifier();
        dmgSpec.DamageDict["Blunt"] = FixedPoint2.New(damage);

        var mobs = _lookup.GetEntitiesInRange<DamageableComponent>(mapCoords, 0.6f);
        foreach (var (mobUid, _) in mobs)
        {
            if (!HasComp<MobStateComponent>(mobUid))
                continue;

            _damageable.TryChangeDamage(mobUid, dmgSpec, ignoreResistances: false, origin: comp.Creator);
            _movementMod.TryAddMovementSpeedModDuration(mobUid, MovementModStatusSystem.TaserSlowdown, SlowDuration, SlowWalk, SlowSprint);
        }

        _audio.PlayPvs(BlastSound, uid);

        // Chain-burst adjacent fields
        var adjacentFields = _lookup.GetEntitiesInRange<ResonanceFieldComponent>(mapCoords, 1.6f);
        foreach (var (adjUid, adjComp) in adjacentFields)
        {
            if (adjUid == uid || adjComp.Rupturing)
                continue;
            BurstField(adjUid, adjComp);
        }

        // Spread to adjacent ore veins
        if (comp.ParentResonator != null && TryComp<ResonatorComponent>(comp.ParentResonator.Value, out var resonator))
        {
            if (!_random.Prob(comp.FailureProb / 100f))
            {
                var nearbyOres = _lookup.GetEntitiesInRange<OreVeinComponent>(mapCoords, 1.6f);
                foreach (var (nearOreUid, _) in nearbyOres)
                {
                    var nearCoords = Transform(nearOreUid).Coordinates;
                    var nearMap = _xform.ToMapCoordinates(nearCoords);

                    // Skip if already has a field there
                    bool hasField = false;
                    var check = _lookup.GetEntitiesInRange<ResonanceFieldComponent>(nearMap, 0.6f);
                    foreach (var _ in check) { hasField = true; break; }
                    if (hasField)
                        continue;

                    var spreadProto = resonator.Mode == ResonatorMode.Matrix ? FieldMatrixPrototype : FieldPrototype;
                    var newFieldEnt = Spawn(spreadProto, nearCoords);
                    var newField = EnsureComp<ResonanceFieldComponent>(newFieldEnt);
                    newField.Creator = comp.Creator;
                    newField.ParentResonator = comp.ParentResonator;
                    newField.AddingFailure = 50f;
                    newField.Mode = resonator.Mode;
                    newField.Duration = resonator.Mode == ResonatorMode.Auto ? 2f : 60f;
                    newField.Timer = newField.Duration;
                    newField.FailureProb = comp.FailureProb + comp.AddingFailure;

                    resonator.Fields.Add(newFieldEnt);
                    _audio.PlayPvs(FireSound, newFieldEnt);
                }
            }
        }

        QueueDel(uid);
    }

    private bool IsLavaland(MapCoordinates coords)
    {
        var mapEnt = _mapSystem.GetMapOrInvalid(coords.MapId);
        return HasComp<LavalandMapComponent>(mapEnt);
    }
}
