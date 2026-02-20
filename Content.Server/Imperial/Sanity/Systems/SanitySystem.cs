using Content.Server.Imperial.Sanity.Components;
using Content.Server.Body.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Sanity.Systems;

public sealed class SanitySystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> _sanityAlert = "Sanity";
    private const string ScpBaseParent = "ImperialSCPBase";
    private const string ScpPresetParent = "ImperialSCPBasePreset";

    private static readonly HashSet<string> _friendlyScpIds =
    [
        "ImperialSCPNDA131",
        "ImperialSCPNDA131A",
        "ImperialSCPJelly",
    ];

    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SanityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SanityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SanityComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<SanityComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<SanityComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);
    }

    private void OnMapInit(Entity<SanityComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.Value = Math.Clamp(ent.Comp.StartSanity, ent.Comp.MinSanity, ent.Comp.MaxSanity);
        ent.Comp.NextUpdate = now + ent.Comp.UpdateInterval;
        ent.Comp.NextProximityCheck = now + ent.Comp.ProximityInterval;
        ent.Comp.LastDialogueTime = now;
        ent.Comp.NextSilencePenalty = now + ent.Comp.SilencePenaltyInterval;
        ent.Comp.NextCoffeeGain = now;
        ent.Comp.NextLowSound = now + ent.Comp.InitialLowSoundDelay;
        ent.Comp.NextHighRegenTick = now + ent.Comp.InitialHighRegenDelay;

        if (TryComp<HungerComponent>(ent, out var hungerComp))
            ent.Comp.LastHunger = _hunger.GetHunger(hungerComp);

        if (TryComp<ThirstComponent>(ent, out var thirstComp))
            ent.Comp.LastThirst = thirstComp.CurrentThirst;

        UpdateState(ent);
        UpdateAlert(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<SanityComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, _sanityAlert);
    }

    private void OnDamageChanged(Entity<SanityComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null)
            return;

        var total = 0f;
        foreach (var delta in args.DamageDelta.DamageDict.Values)
        {
            if (delta <= 0)
                continue;

            total += (float) delta;
        }

        if (total <= 0f)
            return;

        var loss = Math.Clamp(total * ent.Comp.DamageLossPerPoint, 0f, ent.Comp.DamageLossCap);
        ModifySanity(ent, -loss);
    }

    private void OnEntitySpoke(Entity<SanityComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Source != ent.Owner)
            return;

        ent.Comp.LastDialogueTime = _timing.CurTime;
        ModifySanity(ent, ent.Comp.DialogueGain);
    }

    private void OnRefreshMovement(Entity<SanityComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.State == SanityState.High)
        {
            args.ModifySpeed(ent.Comp.HighWalkMultiplier, ent.Comp.HighSprintMultiplier);
            return;
        }

        if (ent.Comp.State == SanityState.Low)
            args.ModifySpeed(ent.Comp.LowWalkMultiplier, ent.Comp.LowSprintMultiplier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SanityComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var sanity, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (now < sanity.NextUpdate)
                continue;

            sanity.NextUpdate = now + sanity.UpdateInterval;

            ProcessNutritionChanges((uid, sanity));
            ProcessCoffeeGain((uid, sanity), now);
            ProcessSilencePenalty((uid, sanity), now);

            if (now >= sanity.NextProximityCheck)
            {
                sanity.NextProximityCheck = now + sanity.ProximityInterval;
                ProcessProximity((uid, sanity));
            }

            UpdateState((uid, sanity));
            ApplyNeedsAndBloodEffects((uid, sanity));
            ProcessHighSanityRegen((uid, sanity), now);
            ProcessLowSanitySound((uid, sanity), now);
            UpdateAlert(uid, sanity);
        }
    }

    private void ProcessNutritionChanges(Entity<SanityComponent> ent)
    {
        if (TryComp<HungerComponent>(ent, out var hungerComp))
        {
            var current = _hunger.GetHunger(hungerComp);
            var delta = current - ent.Comp.LastHunger;
            if (delta >= 2f)
                ModifySanity(ent, ent.Comp.EatGain);

            ent.Comp.LastHunger = current;
        }

        if (TryComp<ThirstComponent>(ent, out var thirstComp))
        {
            var delta = thirstComp.CurrentThirst - ent.Comp.LastThirst;
            if (delta >= 2f)
                ModifySanity(ent, ent.Comp.DrinkGain);

            ent.Comp.LastThirst = thirstComp.CurrentThirst;
        }
    }

    private void ProcessCoffeeGain(Entity<SanityComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCoffeeGain)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        if (!_solutions.TryGetSolution(ent.Owner, bloodstream.ChemicalSolutionName, out _, out var solution))
            return;

        foreach (var reagent in solution.Contents)
        {
            if (!IsCoffeeLikeReagent(reagent.Reagent.Prototype))
                continue;

            ModifySanity(ent, ent.Comp.CoffeeGain);
            ent.Comp.NextCoffeeGain = now + ent.Comp.CoffeeGainCooldown;
            return;
        }
    }

    private void ProcessSilencePenalty(Entity<SanityComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextSilencePenalty)
            return;

        ent.Comp.NextSilencePenalty = now + ent.Comp.SilencePenaltyInterval;
        if (now - ent.Comp.LastDialogueTime < ent.Comp.SilenceTimeout)
            return;

        ModifySanity(ent, -ent.Comp.SilenceLoss);
    }

    private void ProcessProximity(Entity<SanityComponent> ent)
    {
        if (IsNearLivingHumanoid(ent.Owner, ent.Comp.NearHumanRadius))
            ModifySanity(ent, ent.Comp.NearHumanGain);

        ProcessScpProximity(ent);

        var sawCorpse = IsCorpseNearVisible(ent.Owner, ent.Comp.CorpseRadius);
        var sawScp = IsScpNearVisible(ent.Owner, ent.Comp.ScpProximityRadius);

        if (sawCorpse)
            ModifySanity(ent, -ent.Comp.CorpseLoss);

        if (sawScp)
            ModifySanity(ent, -ent.Comp.SeenScpLoss);

        if (IsNdaObjectNear(ent.Owner, ent.Comp.NdaRadius))
            ModifySanity(ent, ent.Comp.NearNdaGain);
    }

    private void ProcessScpProximity(Entity<SanityComponent> ent)
    {
        var hasFriendly = false;
        var hasHostile = false;

        foreach (var nearby in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.ScpProximityRadius, LookupFlags.Dynamic))
        {
            if (nearby == ent.Owner)
                continue;

            if (!TryComp<MobStateComponent>(nearby, out var state) || state.CurrentState == MobState.Dead)
                continue;

            if (!TryComp<MetaDataComponent>(nearby, out var meta))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (string.IsNullOrWhiteSpace(protoId))
                continue;

            if (_friendlyScpIds.Contains(protoId))
            {
                hasFriendly = true;
                continue;
            }

            if (PrototypeInherits(protoId, ScpPresetParent))
            {
                hasFriendly = true;
                continue;
            }

            if (PrototypeInherits(protoId, ScpBaseParent))
                hasHostile = true;
        }

        if (hasFriendly)
            ModifySanity(ent, ent.Comp.ScpFriendlyGain);

        if (hasHostile)
            ModifySanity(ent, -ent.Comp.ScpHostileLoss);
    }

    private void UpdateState(Entity<SanityComponent> ent)
    {
        var old = ent.Comp.State;
        ent.Comp.State = ent.Comp.Value > ent.Comp.HighThreshold
            ? SanityState.High
            : ent.Comp.Value < ent.Comp.LowThreshold
                ? SanityState.Low
                : SanityState.Normal;

        if (old != ent.Comp.State)
            _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void ApplyNeedsAndBloodEffects(Entity<SanityComponent> ent)
    {
        if (ent.Comp.State == SanityState.High)
        {
            if (TryComp<HungerComponent>(ent, out var hungerComp))
                _hunger.ModifyHunger(ent.Owner, ent.Comp.HighHungerPerTick, hungerComp);

            if (TryComp<ThirstComponent>(ent, out var thirstComp))
                _thirst.ModifyThirst(ent.Owner, thirstComp, ent.Comp.HighThirstPerTick);

            if (TryComp<BloodstreamComponent>(ent, out var bloodComp))
                _bloodstream.TryModifyBloodLevel((ent.Owner, bloodComp), FixedPoint2.New(ent.Comp.HighBloodPerTick));

            return;
        }

        if (ent.Comp.State == SanityState.Low)
        {
            if (TryComp<HungerComponent>(ent, out var hungerComp))
                _hunger.ModifyHunger(ent.Owner, ent.Comp.LowHungerPerTick, hungerComp);

            if (TryComp<ThirstComponent>(ent, out var thirstComp))
                _thirst.ModifyThirst(ent.Owner, thirstComp, ent.Comp.LowThirstPerTick);

            if (TryComp<BloodstreamComponent>(ent, out var bloodComp))
                _bloodstream.TryModifyBloodLevel((ent.Owner, bloodComp), FixedPoint2.New(ent.Comp.LowBloodPerTick));
        }
    }

    private static bool IsCoffeeLikeReagent(string reagentId)
    {
        return reagentId.Contains("Coffee", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Caffeine", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Espresso", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Latte", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Cappuccino", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Americano", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Mocha", StringComparison.OrdinalIgnoreCase)
               || reagentId.Contains("Macchiato", StringComparison.OrdinalIgnoreCase);
    }

    private void ProcessHighSanityRegen(Entity<SanityComponent> ent, TimeSpan now)
    {
        if (ent.Comp.State != SanityState.High)
            return;

        if (now < ent.Comp.NextHighRegenTick)
            return;

        ent.Comp.NextHighRegenTick = now + TimeSpan.FromSeconds(1);

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var healing = new DamageSpecifier();
        foreach (var kv in damageable.Damage.DamageDict)
        {
            if (kv.Value == FixedPoint2.Zero)
                continue;

            healing.DamageDict[kv.Key] = FixedPoint2.New(-ent.Comp.HighRegenPerType);
        }

        if (!healing.Empty)
            _damageable.TryChangeDamage(ent.Owner, healing, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void ProcessLowSanitySound(Entity<SanityComponent> ent, TimeSpan now)
    {
        if (ent.Comp.State != SanityState.Low)
            return;

        if (now < ent.Comp.NextLowSound || ent.Comp.LowSanitySounds.Count == 0)
            return;

        var sound = _random.Pick(ent.Comp.LowSanitySounds);
        _audio.PlayEntity(sound, Filter.Entities(ent.Owner), ent.Owner, true);

        var min = ent.Comp.LowSoundMinInterval.TotalSeconds;
        var max = ent.Comp.LowSoundMaxInterval.TotalSeconds;
        var next = _random.NextFloat((float) min, (float) max);
        ent.Comp.NextLowSound = now + TimeSpan.FromSeconds(next);
    }

    private bool IsNearLivingHumanoid(EntityUid uid, float radius)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(uid, radius, LookupFlags.Dynamic))
        {
            if (ent == uid)
                continue;

            if (!HasComp<HumanoidAppearanceComponent>(ent))
                continue;

            if (!TryComp<MobStateComponent>(ent, out var state) || state.CurrentState != MobState.Alive)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, ent, radius + 0.1f))
                continue;

            return true;
        }

        return false;
    }

    private bool IsCorpseNearVisible(EntityUid uid, float radius)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(uid, radius, LookupFlags.Dynamic))
        {
            if (!TryComp<MobStateComponent>(ent, out var state) || state.CurrentState != MobState.Dead)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, ent, radius + 0.1f))
                continue;

            return true;
        }

        return false;
    }

    private bool IsNdaObjectNear(EntityUid uid, float radius)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(uid, radius, LookupFlags.Dynamic))
        {
            if (ent == uid)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, ent, radius + 0.1f))
                continue;

            if (TryComp<MetaDataComponent>(ent, out var meta) &&
                meta.EntityPrototype?.ID.Contains("NDA", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (TryComp<NpcFactionMemberComponent>(ent, out var faction))
            {
                foreach (var id in faction.Factions)
                {
                    if (!id.Id.Contains("NDA", StringComparison.OrdinalIgnoreCase))
                        continue;

                    return true;
                }
            }
        }

        return false;
    }

    private bool IsScpNearVisible(EntityUid uid, float radius)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(uid, radius, LookupFlags.Dynamic))
        {
            if (ent == uid)
                continue;

            if (!TryComp<MobStateComponent>(ent, out var state) || state.CurrentState == MobState.Dead)
                continue;

            if (!TryComp<MetaDataComponent>(ent, out var meta))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (string.IsNullOrWhiteSpace(protoId))
                continue;

            // Skip friendly SCPs — they are handled by ProcessScpProximity
            if (_friendlyScpIds.Contains(protoId) || PrototypeInherits(protoId, ScpPresetParent))
                continue;

            if (!PrototypeInherits(protoId, ScpBaseParent))
                continue;

            if (!_interaction.InRangeUnobstructed(uid, ent, radius + 0.1f))
                continue;

            return true;
        }

        return false;
    }

    private bool PrototypeInherits(string prototypeId, string parentId, int depth = 0)
    {
        if (depth > 16)
            return false;

        if (prototypeId.Equals(parentId, StringComparison.Ordinal))
            return true;

        if (!_prototypes.TryIndex<EntityPrototype>(prototypeId, out var prototype) || prototype.Parents is null)
            return false;

        foreach (var parent in prototype.Parents)
        {
            if (parent.Equals(parentId, StringComparison.Ordinal))
                return true;

            if (PrototypeInherits(parent, parentId, depth + 1))
                return true;
        }

        return false;
    }

    private void ModifySanity(Entity<SanityComponent> ent, float delta)
    {
        if (MathF.Abs(delta) <= 0.0001f)
            return;

        var old = ent.Comp.Value;
        ent.Comp.Value = Math.Clamp(ent.Comp.Value + delta, ent.Comp.MinSanity, ent.Comp.MaxSanity);
        if (MathF.Abs(old - ent.Comp.Value) <= 0.0001f)
            return;

        if (ent.Comp.Value <= ent.Comp.LowThreshold && old > ent.Comp.LowThreshold)
            _popup.PopupEntity(Loc.GetString("sanity-low-warning"), ent, ent);

        if (ent.Comp.Value >= ent.Comp.HighThreshold && old < ent.Comp.HighThreshold)
            _popup.PopupEntity(Loc.GetString("sanity-high-feeling"), ent, ent);
    }

    private void UpdateAlert(EntityUid uid, SanityComponent comp)
    {
        var severity = (short) Math.Clamp((int) MathF.Floor(comp.Value / comp.MaxSanity * 10f), 0, 10);
        _alerts.ShowAlert(uid, _sanityAlert, severity);
    }
}
