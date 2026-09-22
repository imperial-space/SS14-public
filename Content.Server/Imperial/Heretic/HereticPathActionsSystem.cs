using System.Numerics;
using System.Threading;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Content.Server.Popups;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Inventory;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Robust.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Server.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Hands.EntitySystems;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.CombatMode;
using Robust.Shared.Timing;
using Content.Shared.Mindshield.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Stacks;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Maps;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Body.Events;
using Content.Shared.Eye;
using Content.Server.Beam;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Alert;
using Content.Server.Body.Components;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Slippery;
using Content.Shared.Electrocution;
using Content.Shared.Bed.Sleep;
using Content.Server.Body;
using Content.Server.Polymorph.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Polymorph;
using Robust.Shared.Spawners;
using Robust.Shared.Map.Components;
using Robust.Shared.Collections;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Handles all path-specific heretic actions.
/// </summary>
public sealed class HereticPathActionsSystem : EntitySystem
{
    private static readonly EntProtoId DisableCloakProto = "ActionHereticDisableCloak";
    private static readonly EntProtoId ShedHumanFormProto = "ActionHereticShedHumanForm";
    private static readonly EntProtoId LockShapeshiftProto = "ActionHereticLockShapeshift";

    [Dependency] private readonly SharedCuffableSystem  _cuffs   = default!;
    [Dependency] private readonly DamageableSystem      _damage  = default!;
    [Dependency] private readonly DoorSystem            _door    = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;
    [Dependency] private readonly MobStateSystem        _mobs    = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;
    [Dependency] private readonly SharedStealthSystem   _stealth = default!;
    [Dependency] private readonly SharedStunSystem      _stun    = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly ThrowingSystem        _throw   = default!;
    [Dependency] private readonly HereticSystem         _heretic = default!;
    [Dependency] private readonly MindSystem            _mind    = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly SharedHandsSystem     _hands   = default!;
    [Dependency] private readonly InventorySystem        _inventory = default!;
    [Dependency] private readonly SharedContainerSystem  _container = default!;
    [Dependency] private readonly StatusEffectsSystem    _statusEffects = default!;
    [Dependency] private readonly TagSystem              _tag          = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly FlammableSystem        _flammable    = default!;
    [Dependency] private readonly VomitSystem            _vomit        = default!;
    [Dependency] private readonly SharedActionsSystem    _actions      = default!;
    [Dependency] private readonly IGameTiming            _timing       = default!;
    [Dependency] private readonly StaminaSystem          _stamina      = default!;
    [Dependency] private readonly SharedEyeSystem        _eye          = default!;
    [Dependency] private readonly HereticMoonAmuletSystem      _moonAmulet      = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly MovementModStatusSystem     _movementMod   = default!;
    [Dependency] private readonly SharedPhysicsSystem         _physics       = default!;
    [Dependency] private readonly GodmodeSystem               _godmode       = default!;
    [Dependency] private readonly SharedAccessSystem          _access        = default!;
    [Dependency] private readonly SharedBloodstreamSystem     _bloodstream   = default!;
    [Dependency] private readonly ChatSystem                  _chat          = default!;
    [Dependency] private readonly MetaDataSystem              _metaData      = default!;
    [Dependency] private readonly MobThresholdSystem          _mobThreshold  = default!;
    [Dependency] private readonly TurfSystem                  _turf          = default!;
    [Dependency] private readonly VisibilitySystem            _visibility    = default!;
    [Dependency] private readonly BeamSystem                 _beam          = default!;
    [Dependency] private readonly HereticAshSpiritSystem     _ashSpiritSystem = default!;
    [Dependency] private readonly AlertsSystem                _alert           = default!;
    [Dependency] private readonly GunSystem                  _gun             = default!;
    [Dependency] private readonly DecalSystem                _decals          = default!;
    [Dependency] private readonly HereticArenaSystem        _arena           = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem   _melee           = default!;
    [Dependency] private readonly SharedCombatModeSystem    _combatMode      = default!;
    [Dependency] private readonly DoAfterSystem              _doAfter         = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage   = default!;
    [Dependency] private readonly PolymorphSystem               _polymorph     = default!;
    [Dependency] private readonly HereticVoidPrisonSystem      _voidPrison    = default!;
    [Dependency] private readonly UserInterfaceSystem           _ui            = default!;
    [Dependency] private readonly VisualBodySystem              _visualBody    = default!;
    [Dependency] private readonly IPrototypeManager             _prototype     = default!;
    [Dependency] private readonly SharedMapSystem               _map           = default!;
    [Dependency] private readonly SharedInteractionSystem       _interaction   = default!;

    private static readonly ProtoId<TagPrototype> HereticBladeTag = "HereticBlade";
    private static readonly ProtoId<TagPrototype> KnifeTag = "Knife";

    private static readonly EntProtoId FireRingOathActionId        = "ActionHereticFireRingOath";
    private static readonly EntProtoId FireCascadeActionId          = "ActionHereticFireCascade";
    private static readonly EntProtoId GreatFireCascadeActionId     = "ActionHereticGreatFireCascade";
    private static readonly EntProtoId AshSpiritFlameOathActionId   = "ActionHereticAshSpiritFlameOath";

    private readonly Dictionary<EntityUid, TimeSpan>              _danceOfBrandCooldowns  = new();
    private static readonly TimeSpan DanceOfBrandCooldown = TimeSpan.FromSeconds(20);

    // CTS for timed stealth effects (refreshed on re-use)
    private readonly Dictionary<EntityUid, CancellationTokenSource> _fireRingCancels = new();
    private readonly Dictionary<EntityUid, AshShiftState> _ashShiftStates = new();

    private sealed class AshShiftState
    {
        public EntityUid OrbUid;
        public List<EntityCoordinates> ExitPoints = new();
    }

    public override void Initialize()
    {
        base.Initialize();

        // ── Soul Bottle (Lock path) ───────────────────────────────────────────
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobDied);

        // ── Flesh familiars ───────────────────────────────────────────────────
        SubscribeLocalEvent<HereticVoicelessDeadComponent, MobStateChangedEvent>(OnVoicelessDeadDied);

        // ── General ───────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticCloakOfShadowActionEvent>(OnCloakOfShadow);
        SubscribeLocalEvent<HereticComponent, HereticDisableCloakActionEvent>(OnDisableCloak);
        SubscribeLocalEvent<HereticComponent, HereticHeartbeatMansusActionEvent>(OnHeartbeatMansus);

        // ── Ash Orb ───────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticAshOrbComponent, MoveEvent>(OnAshOrbMove);
        SubscribeLocalEvent<HereticAshOrbComponent, GetVisMaskEvent>(OnAshOrbGetVisMask);
        SubscribeLocalEvent<HereticAshOrbComponent, EntityTerminatingEvent>(OnAshOrbTerminating);
        SubscribeLocalEvent<HereticComponent, BeforeDamageChangedEvent>(OnHereticAshShiftDamage);

        // ── Ash ───────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticAshenPassageActionEvent>(OnAshenPassage);
        SubscribeLocalEvent<HereticComponent, HereticVolcanoBlastActionEvent>(OnVolcanoBlast);
        SubscribeLocalEvent<HereticComponent, HereticAshlordsRebirthActionEvent>(OnAshlordsRebirth);
        SubscribeLocalEvent<HereticAshBladeComponent, MeleeHitEvent>(OnAshBladeMeleeHit);

        // ── Rust ──────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticRustBladeComponent, MeleeHitEvent>(OnRustBladeMeleeHit);

        // ── Cosmos ────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticCosmosBladeComponent, MeleeHitEvent>(OnCosmosBladeMeleeHit);

        // ── Void ──────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticVoidPullActionEvent>(OnVoidPull);
        SubscribeLocalEvent<HereticComponent, HereticVoidConduitActionEvent>(OnVoidConduit);
        SubscribeLocalEvent<HereticVoidBladeComponent, MeleeHitEvent>(OnVoidBladeMeleeHit);

        // ── Blade ─────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, DamageChangedEvent>(OnHereticDamaged);
        SubscribeLocalEvent<HereticComponent, DamageModifyEvent>(OnHereticDamageModify);
        SubscribeLocalEvent<HereticComponent, HereticRealignmentActionEvent>(OnRealignment);
        SubscribeLocalEvent<HereticComponent, HereticSanguineSurgeActionEvent>(OnSanguineSurge);
        SubscribeLocalEvent<HereticComponent, HereticCleaveActionEvent>(OnCleave);
        SubscribeLocalEvent<HereticComponent, HereticSummonBladesActionEvent>(OnSummonBlades);
        SubscribeLocalEvent<HereticComponent, HereticFuriousSteelActionEvent>(OnFuriousSteel);
        SubscribeLocalEvent<HereticComponent, HereticWolvesAmongSheepActionEvent>(OnWolvesAmongSheep);
        SubscribeLocalEvent<HereticBladeWeaponComponent, MeleeHitEvent>(OnBladeBladeMeleeHit);
        SubscribeLocalEvent<HereticBladeWeaponComponent, UseInHandEvent>(OnBladeUseInHand);

        // ── Rust ──────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticCorrodeActionEvent>(OnCorrode);
        SubscribeLocalEvent<HereticComponent, HereticRustCoatActionEvent>(OnRustCoat);
        SubscribeLocalEvent<HereticComponent, HereticRustWaveActionEvent>(OnRustWave);
        SubscribeLocalEvent<HereticComponent, HereticEntropicPlagueActionEvent>(OnEntropicPlague);
        SubscribeLocalEvent<HereticComponent, HereticRustingCrownActionEvent>(OnRustingCrown);

        // ── Ash (expanded) ────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticAshlordRiteActionEvent>(OnAshlordRite);
        SubscribeLocalEvent<HereticComponent, HereticFireRingOathActionEvent>(OnFireRingOath);
        SubscribeLocalEvent<HereticComponent, HereticFireCascadeActionEvent>(OnFireCascade);

        // ── Moon ─────────────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticMoonGateActionEvent>(OnMoonGate);

        // ── Void (expanded) ───────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticVoidPhaseActionEvent>(OnVoidPhase);
        SubscribeLocalEvent<HereticComponent, HereticVoidPrisonActionEvent>(OnVoidPrison);
        SubscribeLocalEvent<HereticComponent, HereticWaveOfDesperationActionEvent>(OnWaveOfDesperation);
        SubscribeLocalEvent<HereticComponent, HereticMaidInMirrorActionEvent>(OnMaidInMirror);
        SubscribeLocalEvent<HereticComponent, HereticVoidSeekingBladeActionEvent>(OnVoidSeekingBlade);

        // ── Blade (expanded) ──────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticRawRitualActionEvent>(OnRawRitual);
        SubscribeLocalEvent<HereticComponent, HereticStanceOfTornChampionActionEvent>(OnStanceOfTornChampion);
        SubscribeLocalEvent<TornChampionStanceComponent, BleedModifierEvent>(OnTornChampionBleed);
        SubscribeLocalEvent<TornChampionStanceComponent, DamageModifyEvent>(OnTornChampionDamageModify);
        SubscribeLocalEvent<TornChampionStanceComponent, KnockDownAttemptEvent>(OnTornChampionKnockdownAttempt);
        SubscribeLocalEvent<HereticComponent, HereticLionhunterRifleActionEvent>(OnLionhunterRifle);

        // ── Rust (expanded) ───────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticAggressiveSpreadActionEvent>(OnAggressiveSpread);
        SubscribeLocalEvent<HereticComponent, HereticRustWalkerAggressiveSpreadActionEvent>(OnHereticRustWalkerAggressiveSpread);
        SubscribeLocalEvent<HereticComponent, HereticRustConstructionActionEvent>(OnRustConstruction);
        SubscribeLocalEvent<HereticComponent, HereticEntropicPlumeActionEvent>(OnEntropicPlume);

        // ── Lock (expanded) ───────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticLockShapeshiftActionEvent>(OnLockShapeshift);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticLockShapeshiftSelectMessage>(OnLockShapeshiftSelect);

        // ── Ascension abilities ───────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticAshSpiritFlameOathActionEvent>(OnHereticFlameOath);
        SubscribeLocalEvent<HereticComponent, HereticGreatFireCascadeActionEvent>(OnGreatFireCascade);
        SubscribeLocalEvent<HereticComponent, HereticAscensionRustActionEvent>(OnAscensionRust);
        SubscribeLocalEvent<HereticComponent, HereticAscensionCosmosActionEvent>(OnAscensionCosmos);

        // Maelstrom of Silver: stun/knockdown immunity
        SubscribeLocalEvent<HereticMaelstromOfSilverComponent, StunnedEvent>(OnMaelstromStunned);
        SubscribeLocalEvent<HereticMaelstromOfSilverComponent, KnockDownAttemptEvent>(OnMaelstromKnockdownAttempt);

        // Rustbringer's Oath: tripled healing + resistance while on rust tiles
        SubscribeLocalEvent<HereticRustAscendedComponent, DamageModifyEvent>(OnRustAscendedDamageModify);
        SubscribeLocalEvent<HereticRustAscendedComponent, StunnedEvent>(OnRustAscendedStunned);
        SubscribeLocalEvent<HereticRustAscendedComponent, KnockDownAttemptEvent>(OnRustAscendedKnockdownAttempt);
        SubscribeLocalEvent<HereticRustAscendedComponent, SlipAttemptEvent>(OnRustAscendedSlipAttempt);
        SubscribeLocalEvent<HereticRustAscendedComponent, ElectrocutionAttemptEvent>(OnRustAscendedElectrocution);
        SubscribeLocalEvent<HereticRustAscendedComponent, TryingToSleepEvent>(OnRustAscendedSleepAttempt);

        // ── Special abilities ─────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticRelentlessHeartbeatActionEvent>(OnRelentlessHeartbeat);
        SubscribeLocalEvent<HereticComponent, HereticSummonFamiliarActionEvent>(OnSummonFamiliar);



        // ── Unsealed Arts ─────────────────────────────────────────────────────
        SubscribeLocalEvent<HereticComponent, HereticUnsealedArtsActionEvent>(OnUnsealedArts);
    }

    private sealed class CosmosComboState
    {
        public EntityUid FirstTarget  = EntityUid.Invalid;
        public EntityUid SecondTarget = EntityUid.Invalid;
        public int ComboCount;
        public TimeSpan ResetAt;
    }

    private readonly Dictionary<EntityUid, CosmosComboState> _cosmosCombo = new();

    public void CleanupEntity(EntityUid uid)
    {
        EndAshShift(uid, silent: true);
        _danceOfBrandCooldowns.Remove(uid);
        _cosmosCombo.Remove(uid);

        if (_fireRingCancels.Remove(uid, out var fireRingCts))
            fireRingCts.Cancel();
    }

    // ─── General ──────────────────────────────────────────────────────────────

    private void OnCloakOfShadow(EntityUid uid, HereticComponent comp, HereticCloakOfShadowActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        const int durationSeconds = 180;
        args.Handled = true;

        var origWalk   = 2.5f;
        var origSprint = 4.5f;
        var origAccel  = 20f;
        if (TryComp<MovementSpeedModifierComponent>(uid, out var moveComp))
        {
            origWalk   = moveComp.BaseWalkSpeed;
            origSprint = moveComp.BaseSprintSpeed;
            origAccel  = moveComp.Acceleration;
        }
        var originalName = MetaData(uid).EntityName;

        // Effects: smoke + sound + popup
        Spawn("HereticEffectSmoke", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cloak-of-shadow"), uid, uid, PopupType.Medium);

        // Spawn persistent curse overlay parented to the player
        var effectUid = Spawn("HereticCloakEffect", Transform(uid).Coordinates);
        _xform.SetParent(effectUid, uid);

        // Rename player, apply 25% speed boost, hide original sprite
        _metaData.SetEntityName(uid, "Тень");
        _movementSpeed.ChangeBaseSpeed(uid, origWalk, origSprint * 1.25f, origAccel);
        var addedStealth = !HasComp<StealthComponent>(uid);
        var stealthComp = EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealthComp);
        _stealth.SetVisibility(uid, -1f, stealthComp);

        // Store deactivation data in component
        var cloakComp = EnsureComp<HereticCloakActiveComponent>(uid);
        cloakComp.OrigWalkSpeed    = origWalk;
        cloakComp.OrigSprintSpeed  = origSprint;
        cloakComp.OrigAcceleration = origAccel;
        cloakComp.OriginalName     = originalName;
        cloakComp.EffectEntity     = effectUid;
        cloakComp.AddedStealth     = addedStealth;
        cloakComp.IsActive         = true;

        // Grant the manual-disable action
        EntityUid? disableAction = null;
        _actions.AddAction(uid, ref disableAction, DisableCloakProto);
        cloakComp.DisableActionEntity = disableAction;

        // Curse icon trail
        void SpawnTrail()
        {
            if (!cloakComp.IsActive || Deleted(uid)) return;
            Spawn("HereticCloakTrailEffect", Transform(uid).Coordinates);
            Timer.Spawn(TimeSpan.FromMilliseconds(100), SpawnTrail);
        }
        Timer.Spawn(TimeSpan.FromMilliseconds(100), SpawnTrail);

        Timer.Spawn(TimeSpan.FromSeconds(durationSeconds), () =>
        {
            if (Deleted(uid)) return;
            if (!TryComp<HereticCloakActiveComponent>(uid, out var cc)) return;
            if (!cc.IsActive) return;
            DeactivateCloak(uid, cc);
        });
    }

    private void OnDisableCloak(EntityUid uid, HereticComponent comp, HereticDisableCloakActionEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<HereticCloakActiveComponent>(uid, out var cloakComp)) return;
        args.Handled = true;
        DeactivateCloak(uid, cloakComp);
    }

    private void DeactivateCloak(EntityUid uid, HereticCloakActiveComponent cloakComp)
    {
        if (!cloakComp.IsActive) return;
        cloakComp.IsActive = false;

        if (!Deleted(cloakComp.EffectEntity))
            QueueDel(cloakComp.EffectEntity);

        _metaData.SetEntityName(uid, cloakComp.OriginalName);
        _movementSpeed.ChangeBaseSpeed(uid, cloakComp.OrigWalkSpeed, cloakComp.OrigSprintSpeed, cloakComp.OrigAcceleration);

        if (cloakComp.AddedStealth)
            RemComp<StealthComponent>(uid);
        else if (TryComp<StealthComponent>(uid, out var sc))
            _stealth.SetEnabled(uid, false, sc);

        if (cloakComp.DisableActionEntity.HasValue)
            _actions.RemoveAction(new Entity<ActionComponent?>(cloakComp.DisableActionEntity.Value, null));

        RemComp<HereticCloakActiveComponent>(uid);
    }

    private void OnHeartbeatMansus(EntityUid uid, HereticComponent comp, HereticHeartbeatMansusActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        TryReplaceHeartWithHereticHeart(uid);
        _heretic.OpenTargetBui(uid, comp);
    }

    private void TryReplaceHeartWithHereticHeart(EntityUid uid)
    {
        if (!_container.TryGetContainer(uid, BodyComponent.ContainerID, out var organs))
            return;

        foreach (var organ in organs.ContainedEntities)
        {
            if (HasComp<HereticHeartComponent>(organ))
                return;
        }

        EntityUid? oldHeart = null;
        foreach (var organ in organs.ContainedEntities.ToList())
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;
            if (category == "Heart")
            {
                oldHeart = organ;
                break;
            }
        }

        if (oldHeart.HasValue)
        {
            _container.Remove(oldHeart.Value, organs);
            Del(oldHeart.Value);
        }

        var newHeart = Spawn("OrganHereticHeart", Transform(uid).Coordinates);
        _container.Insert(newHeart, organs);
    }

    // ─── Ash ─────────────────────────────────────────────────────────────────

    private void OnAshenPassage(EntityUid uid, HereticComponent comp, HereticAshenPassageActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (_ashShiftStates.ContainsKey(uid))
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        var empowered = false;
        if (TryComp<FlammableComponent>(uid, out var fireComp) && fireComp.FireStacks > 3f)
        {
            foreach (var slot in new[] { "outerClothing", "outer" })
            {
                if (!_inventory.TryGetSlotEntity(uid, slot, out var slotEnt))
                    continue;

                if (!TryComp<HereticPathRobeComponent>(slotEnt.Value, out var robeComp) || robeComp.Path != HereticPath.Ash)
                    continue;

                empowered = true;
                break;
            }
        }

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-ashen-passage"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        var coords = Transform(uid).Coordinates;
        var orbUid = Spawn("MobHereticAshOrb", coords);

        if (!TryComp<HereticAshOrbComponent>(orbUid, out var orbComp))
        {
            QueueDel(orbUid);
            return;
        }

        orbComp.HereticUid = uid;
        orbComp.Empowered = empowered;

        var vis = EnsureComp<VisibilityComponent>(orbUid);
        _visibility.AddLayer((orbUid, vis), (int)VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((orbUid, vis), (int)VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(orbUid, visibilityComponent: vis);

        _xform.DetachParentToNull(uid, Transform(uid));

        _mind.TransferTo(mindId, orbUid, ghostCheckOverride: true);
        _eye.RefreshVisibilityMask(orbUid);

        _godmode.EnableGodmode(orbUid);

        _ashShiftStates[uid] = new AshShiftState { OrbUid = orbUid };

        if (empowered)
        {
            RemCompDeferred<KnockedDownComponent>(uid);
            RemCompDeferred<StunnedComponent>(uid);

            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                _cuffs.TryUncuff(uid, uid);

            if (TryComp<FlammableComponent>(uid, out var fireComp2))
                _flammable.Extinguish(uid, fireComp2);
        }

        Spawn("HereticEffectAshBlink", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg"), orbUid);
        _popup.PopupEntity(Loc.GetString(empowered ? "heretic-ash-passage-empowered" : "heretic-ash-passage"), orbUid, orbUid, PopupType.Medium);

        var duration = TimeSpan.FromSeconds(5);
        Timer.Spawn(duration, () =>
        {
            if (!Exists(uid)) return;
            EndAshShift(uid);
        });
    }

    private void OnAshOrbMove(EntityUid uid, HereticAshOrbComponent comp, ref MoveEvent args)
    {
        if (!_ashShiftStates.TryGetValue(comp.HereticUid, out var state))
            return;

        if (_turf.TryGetTileRef(args.NewPosition, out var tileRef)
            && !_turf.IsTileBlocked(tileRef.Value, CollisionGroup.Impassable))
        {
            state.ExitPoints.Add(args.NewPosition);
            if (state.ExitPoints.Count > 5)
                state.ExitPoints.RemoveAt(0);
        }
    }

    private void OnAshOrbGetVisMask(EntityUid uid, HereticAshOrbComponent comp, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Ghost;
    }

    private void OnAshOrbTerminating(EntityUid uid, HereticAshOrbComponent comp, ref EntityTerminatingEvent args)
    {
        EndAshShift(comp.HereticUid, silent: true);
    }

    private void OnHereticAshShiftDamage(EntityUid uid, HereticComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (_ashShiftStates.ContainsKey(uid))
            args.Cancelled = true;
    }

    private void OnVolcanoBlast(EntityUid uid, HereticComponent comp, HereticVolcanoBlastActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var maxBounces = (comp.AscensionTriggered && comp.CurrentPath == HereticPath.Ash) ? 8 : 4;
        const float chainRadius = 7f;

        // SS13: empowered if ash robes worn + fire_stacks > 3 → consume all stacks
        var empowered = false;
        if (TryComp<FlammableComponent>(uid, out var casterFlam) && casterFlam.FireStacks > 3f)
        {
            empowered = true;
            _flammable.AdjustFireStacks(uid, -casterFlam.FireStacks);
        }

        var hitSet = new HashSet<EntityUid> { uid };
        var firstTarget = GetVolcanoNextTarget(uid, uid, hitSet, chainRadius);

        if (firstTarget == null)
        {
            _popup.PopupEntity(Loc.GetString("heretic-volcano-blast-no-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-volcano-blast"), uid, uid, PopupType.Medium);
        if (empowered)
            _popup.PopupEntity(Loc.GetString("heretic-volcano-blast-empowered"), uid, uid, PopupType.LargeCaution);

        SendVolcanoBeam(uid, uid, firstTarget.Value, maxBounces, empowered, hitSet);
    }

    // beamSource — откуда вылетает луч (первый раз кастер, дальше — предыдущая жертва)
    private void SendVolcanoBeam(EntityUid caster, EntityUid beamSource, EntityUid target, int bounces, bool empowered, HashSet<EntityUid> hitSet)
    {
        if (Deleted(caster) || Deleted(beamSource) || Deleted(target)) return;

        const float chainRadius = 7f;
        const float hitDamage = 20f;

        hitSet.Add(target);

        _beam.TryCreateBeam(beamSource, target, "HereticFireBeam");
        Spawn("HereticEffectFireExplosion", Transform(target).Coordinates);

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Heat"] = FixedPoint2.New(hitDamage);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        _flammable.AdjustFireStacks(target, empowered ? 6f : 3f, ignite: true);

        if (bounces <= 0)
        {
            DoVolcanoAoE(target, hitSet, empowered);
            return;
        }

        Timer.Spawn(TimeSpan.FromSeconds(1.0), () =>
            ContinueVolcanoBeam(caster, target, bounces - 1, empowered, hitSet, chainRadius));
    }

    private void ContinueVolcanoBeam(EntityUid caster, EntityUid lastTarget, int bounces, bool empowered, HashSet<EntityUid> hitSet, float chainRadius)
    {
        if (Deleted(caster) || Deleted(lastTarget)) return;
        if (!TryComp<MobStateComponent>(lastTarget, out var lastMobState) || !_mobs.IsAlive(lastTarget, lastMobState)) return;
        if (!TryComp<FlammableComponent>(lastTarget, out var lastFlam) || !lastFlam.OnFire) return;

        var next = GetVolcanoNextTarget(caster, lastTarget, hitSet, chainRadius);
        if (next == null)
        {
            // Некого поджечь дальше — AoE на последней цели
            DoVolcanoAoE(lastTarget, hitSet, empowered);
            return;
        }

        SendVolcanoBeam(caster, lastTarget, next.Value, bounces, empowered, hitSet);
    }

    private void DoVolcanoAoE(EntityUid center, HashSet<EntityUid> hitSet, bool empowered)
    {
        const float aoeDamage = 15f;
        var coords = Transform(center).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f))
        {
            var aoeTarget = ent.Owner;
            if (hitSet.Contains(aoeTarget)) continue;
            if (!_mobs.IsAlive(aoeTarget, ent.Comp)) continue;
            if (HasComp<HereticComponent>(aoeTarget)) continue;

            var aoeDmg = new DamageSpecifier();
            aoeDmg.DamageDict["Heat"] = FixedPoint2.New(aoeDamage);
            _damage.TryChangeDamage(aoeTarget, aoeDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(aoeTarget, empowered ? 4f : 2f, ignite: true);
            _stun.TryKnockdown(aoeTarget, TimeSpan.FromSeconds(0.8), true);
        }
    }

    // SS13 get_target: random pick, priority = already burning mobs
    private EntityUid? GetVolcanoNextTarget(EntityUid caster, EntityUid searchCenter, HashSet<EntityUid> alreadyHit, float radius)
    {
        var centerCoords = Transform(searchCenter).Coordinates;
        var validTargets = new List<EntityUid>();
        var priorityTargets = new List<EntityUid>();

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(centerCoords, radius))
        {
            var target = ent.Owner;
            if (target == caster) continue;
            if (alreadyHit.Contains(target)) continue;
            if (HasComp<HereticComponent>(target)) continue;
            if (!_mobs.IsAlive(target, ent.Comp)) continue;

            if (TryComp<FlammableComponent>(target, out var flam) && flam.OnFire)
                priorityTargets.Add(target);
            else
                validTargets.Add(target);
        }

        if (priorityTargets.Count > 0)
            return _random.Pick(priorityTargets);
        if (validTargets.Count > 0)
            return _random.Pick(validTargets);

        return null;
    }

    private void OnAshlordsRebirth(EntityUid uid, HereticComponent comp, HereticAshlordsRebirthActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (TryComp<FlammableComponent>(uid, out var selfFlam))
            _flammable.Extinguish(uid, selfFlam);

        var coords = Transform(uid).Coordinates;
        Spawn("HereticEffectFireExplosion", coords);

        var victimsHit = 0;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 14f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            if (!TryComp<FlammableComponent>(ent.Owner, out var flam) || !flam.OnFire) continue;
            if (ent.Comp.CurrentState == MobState.Dead) continue;

            // Instant kill mobs already in critical state (SS13: CAN_SUCCUMB check before damage)
            if (ent.Comp.CurrentState == MobState.Critical)
                _mobs.ChangeMobState(ent.Owner, MobState.Dead, ent.Comp);

            var victimDmg = new DamageSpecifier();
            victimDmg.DamageDict["Heat"] = FixedPoint2.New(20);
            _damage.TryChangeDamage(ent.Owner, victimDmg, ignoreResistances: false);
            _flammable.Extinguish(ent.Owner, flam);
            Spawn("HereticEffectAshBlink", Transform(ent.Owner).Coordinates);

            victimsHit++;
        }

        if (victimsHit > 0)
        {
            var perVictimVal = FixedPoint2.New(-10);
            var healDmg = new DamageSpecifier();
            healDmg.DamageDict["Blunt"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Heat"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Poison"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Asphyxiation"] = perVictimVal * victimsHit;
            _damage.TryChangeDamage(uid, healDmg, ignoreResistances: true);
            _stamina.TakeStaminaDamage(uid, -10f * victimsHit, visual: false);

            var reducedCd = Math.Max(9, 60 - victimsHit * 10);
            var now = _timing.CurTime;
            foreach (var action in _actions.GetActions(uid))
            {
                if (!TryComp<InstantActionComponent>(action.Owner, out var iac)) continue;
                if (iac.Event is not HereticAshlordsRebirthActionEvent) continue;
                _actions.SetCooldown((action.Owner, (ActionComponent?)action.Comp), now, now + TimeSpan.FromSeconds(reducedCd));
                break;
            }
        }

        _popup.PopupEntity(Loc.GetString("heretic-ashlords-rebirth"), uid, uid, PopupType.Large);
    }

    private void OnAshBladeMeleeHit(Entity<HereticAshBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var comp)) return;
        if (comp.CurrentPath != HereticPath.Ash) return;

        var hasFieryBlade = _heretic.HasKnowledge(comp, "KnowledgeFieryBlade");
        var hasMarkOfAsh = comp.CurrentPath == HereticPath.Ash;
        if (!hasFieryBlade && !hasMarkOfAsh) return;

        foreach (var target in args.HitEntities)
        {
            if (hasFieryBlade)
                _flammable.AdjustFireStacks(target, 1f, ignite: true);

            if (!hasMarkOfAsh || !HasComp<AshMarkComponent>(target))
                continue;

            var bonusDmg = new DamageSpecifier();
            bonusDmg.DamageDict["Stamina"] = FixedPoint2.New(10);
            bonusDmg.DamageDict["Heat"] = FixedPoint2.New(10);
            _damage.TryChangeDamage(target, bonusDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(target, 4f, ignite: true);
            Spawn("HereticEffectAshBlink", Transform(target).Coordinates);

            RemCompDeferred<AshMarkComponent>(target);

            foreach (var nearby in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(target).Coordinates, 5f))
            {
                if (nearby.Owner == args.User || nearby.Owner == target) continue;
                if (!_mobs.IsAlive(nearby.Owner, nearby.Comp)) continue;
                EnsureComp<AshMarkComponent>(nearby.Owner);
                break;
            }

            foreach (var action in _actions.GetActions(args.User))
            {
                if (!TryComp<InstantActionComponent>(action.Owner, out var ia)) continue;
                if (ia.Event is not HereticMansusGraspActionEvent) continue;
                if (action.Comp.Cooldown is not {} cd) break;

                var now = _timing.CurTime;
                var remaining = cd.End - now;
                if (remaining > TimeSpan.Zero)
                    _actions.SetCooldown((action.Owner, (ActionComponent?)action.Comp), now, now + remaining * 0.25);
                break;
            }

            _popup.PopupEntity(Loc.GetString("heretic-ash-mark-triggered"), args.User, args.User, PopupType.Medium);
        }
    }

    // ─── Flesh familiars ─────────────────────────────────────────────────────

    private void OnVoicelessDeadDied(EntityUid uid, HereticVoicelessDeadComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead) return;

        Spawn("HereticLivingHeart", Transform(uid).Coordinates);
    }

    // ─── Void ─────────────────────────────────────────────────────────────────

    private void OnVoidPull(EntityUid uid, HereticComponent comp, HereticVoidPullActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var myLocalPos = Transform(uid).LocalPosition;
        var coords = Transform(uid).Coordinates;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = FixedPoint2.New(30);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 3f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            if (_mobs.IsDead(ent.Owner)) continue;
            if (Transform(ent.Owner).ParentUid != Transform(uid).ParentUid) continue;

            var targetLocalPos = Transform(ent.Owner).LocalPosition;
            var dir = myLocalPos - targetLocalPos;
            var dist = dir.Length();

            _stun.TryAddParalyzeDuration(ent.Owner, TimeSpan.FromSeconds(0.5));
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(3), true);

            if (dist > 0.01f)
            {
                var moveAmount = Math.Min(3f, dist);
                _xform.SetLocalPosition(ent.Owner, targetLocalPos + dir.Normalized() * moveAmount);
            }

            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 3);
        }

        Spawn("HereticEffectVoidBlinkIn", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-void-pull-activate"), uid, uid, PopupType.Medium);
    }

    private void OnVoidConduit(EntityUid uid, HereticComponent comp, HereticVoidConduitActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        args.Handled = true;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_cloth_rip.ogg"), uid);

        var conduit = Spawn("HereticVoidConduit", _xform.ToMapCoordinates(args.Target));
        if (TryComp<HereticVoidConduitComponent>(conduit, out var conduitComp))
            conduitComp.Caster = uid;

        _popup.PopupEntity(Loc.GetString("heretic-void-conduit-place"), uid, uid, PopupType.Large);
    }

    private void OnVoidBladeMeleeHit(Entity<HereticVoidBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var comp)) return;
        if (comp.CurrentPath != HereticPath.Void) return;

        var hasUpgrade = _heretic.HasKnowledge(comp, "KnowledgeVoidBladeUpgrade");
        foreach (var target in args.HitEntities)
        {
            if (HasComp<HereticComponent>(target)) continue;
            _hereticEffects.ApplyVoidChill(target, hasUpgrade ? 2 : 1);
        }
    }

    private void OnVoidSeekingBlade(EntityUid uid, HereticComponent comp, HereticVoidSeekingBladeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        var target = args.Target;

        if (!HasComp<VoidMarkComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-void-seeking-blade-no-mark"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        RemComp<VoidMarkComponent>(target);

        var userPos = _xform.GetWorldPosition(Transform(uid));
        var targetPos = _xform.GetWorldPosition(Transform(target));

        var dir = targetPos - userPos;
        if (dir.Length() > 0.01f)
            dir = Vector2.Normalize(dir);

        var behindPos = targetPos - dir;
        _xform.SetWorldPosition(uid, behindPos);

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon))
        {
            var combatComp = EnsureComp<CombatModeComponent>(uid);
            var wasCombat = combatComp.IsInCombatMode;
            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, true, combatComp);

            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, false, combatComp);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-void-seeking-blade-activate"), uid, uid, PopupType.Medium);
    }

    private void OnBladeBladeMeleeHit(Entity<HereticBladeWeaponComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var comp)) return;

        // Lock path: Opening Blade — bleed on hit (65% after ascension, 35% before)
        if (comp.CurrentPath == HereticPath.Lock && _heretic.HasKnowledge(comp, "KnowledgeOpeningBlade"))
        {
            var bleedChance = HasComp<HereticLockAscendedComponent>(args.User) ? 0.65f : 0.35f;
            foreach (var target in args.HitEntities)
            {
                if (HasComp<HereticComponent>(target)) continue;
                if (!_random.Prob(bleedChance)) continue;
                _bloodstream.TryModifyBleedAmount(target, 10f);
            }
        }

        // Lock path: consume LockMark on blade hit — strip all access from the target's ID card
        if (comp.CurrentPath == HereticPath.Lock)
        {
            foreach (var target in args.HitEntities)
            {
                if (HasComp<HereticComponent>(target)) continue;
                _heretic.TryTriggerLockMark(args.User, target);
            }
        }

        if (comp.CurrentPath != HereticPath.Blade) return;

        if (HasComp<HereticMaelstromOfSilverComponent>(args.User))
        {
            foreach (var target in args.HitEntities)
            {
                if (target == args.User) continue;
                if (_mobs.IsDead(target)) continue;

                // Blood steal
                if (TryComp<BloodstreamComponent>(target, out var targetBlood))
                    _bloodstream.TryModifyBloodLevel((target, targetBlood), -FixedPoint2.New(10));

                // SS13: on_eldritch_blade — bonus_damage ~10 BRUTE + self-heal brute/burn 5+5
                _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(10) } }, ignoreResistances: false);
                _damage.TryChangeDamage(args.User, new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(-5), ["Heat"] = FixedPoint2.New(-5) } }, ignoreResistances: true);
            }
        }

        // SS13: on_eldritch_blade — any blade hit consumes existing blade mark, gives orbiting blade
        if (comp.CurrentPath == HereticPath.Blade)
        {
            foreach (var target in args.HitEntities)
            {
                if (!_mobs.IsAlive(target) || target == args.User) continue;
                _heretic.TryTriggerBladeMark(args.User, target);
            }
        }

        if (!_heretic.HasKnowledge(comp, "KnowledgeEmpoweredBlades")) return;

        foreach (var target in args.HitEntities)
            _heretic.TryEmpoweredBladesOnHit(args.User, comp, target, blade.Owner);
    }

    private void OnBladeUseInHand(Entity<HereticBladeWeaponComponent> blade, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<HereticComponent>(args.User, out var hComp)) return;

        // After gaining aura (UnlimitedBlades), breaking is disabled
        if (hComp.UnlimitedBlades) return;

        args.Handled = true;

        var userXform = Transform(args.User);
        var destCoords = FindRandomSafeTileOnGrid(userXform);
        if (destCoords.HasValue)
            _xform.SetCoordinates(args.User, destCoords.Value);

        var afterMsg = hComp.CurrentPath switch
        {
            HereticPath.Blade   => Loc.GetString("heretic-blade-shatter-blade"),
            HereticPath.Rust    => Loc.GetString("heretic-blade-shatter-rust"),
            HereticPath.Ash     => Loc.GetString("heretic-blade-shatter-ash"),
            HereticPath.Flesh   => Loc.GetString("heretic-blade-shatter-flesh"),
            HereticPath.Void    => Loc.GetString("heretic-blade-shatter-void"),
            HereticPath.Cosmos  => Loc.GetString("heretic-blade-shatter-cosmos"),
            HereticPath.Lock    => Loc.GetString("heretic-blade-shatter-lock"),
            HereticPath.Moon    => Loc.GetString("heretic-blade-shatter-moon"),
            _                   => Loc.GetString("heretic-blade-shatter"),
        };

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/runebreak.ogg"), args.User);
        _popup.PopupEntity(afterMsg, args.User, args.User, PopupType.MediumCaution);
        QueueDel(blade.Owner);
    }

    private EntityCoordinates? FindRandomSafeTileOnGrid(TransformComponent userXform)
    {
        var gridUid = userXform.GridUid;
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var gridEntity = new Entity<MapGridComponent>(gridUid.Value, grid);
        var tilesEnum = _map.GetAllTilesEnumerator(gridUid.Value, grid, ignoreEmpty: true);
        var candidates = new ValueList<Vector2i>();

        while (tilesEnum.MoveNext(out var tile))
        {
            if (_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable | CollisionGroup.MobMask))
                continue;
            candidates.Add(tile.Value.GridIndices);
        }

        if (candidates.Count == 0) return null;

        var chosen = candidates[_random.Next(candidates.Count)];
        return new EntityCoordinates(gridUid.Value, _map.TileCenterToVector(gridEntity, chosen));
    }

    private void EndAshShift(EntityUid hereticUid, bool silent = false)
    {
        if (!_ashShiftStates.Remove(hereticUid, out var state))
            return;

        var orbUid = state.OrbUid;

        if (!Exists(hereticUid))
        {
            if (Exists(orbUid))
                QueueDel(orbUid);
            return;
        }

        if (!_mind.TryGetMind(orbUid, out var mindId, out _))
        {
            if (Exists(orbUid))
                QueueDel(orbUid);
            return;
        }

        var exitCoords = state.ExitPoints.Count > 0
            ? state.ExitPoints[^1]
            : (Exists(orbUid) ? Transform(orbUid).Coordinates : Transform(hereticUid).Coordinates);

        if (silent)
        {
            _xform.SetCoordinates(hereticUid, exitCoords);
            _mind.TransferTo(mindId, hereticUid, ghostCheckOverride: true);
            if (Exists(orbUid))
                QueueDel(orbUid);
            return;
        }

        Spawn("HereticEffectAshBlink", exitCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_exit.ogg"), exitCoords);

        var capturedHeretic = hereticUid;
        var capturedOrb = orbUid;
        var capturedMind = mindId;
        var capturedCoords = exitCoords;

        Timer.Spawn(TimeSpan.FromSeconds(0.7), () =>
        {
            if (!Exists(capturedHeretic))
            {
                if (Exists(capturedOrb))
                    QueueDel(capturedOrb);
                return;
            }

            _xform.SetCoordinates(capturedHeretic, capturedCoords);

            if (_mind.TryGetMind(capturedOrb, out var currentMind, out _))
                _mind.TransferTo(currentMind, capturedHeretic, ghostCheckOverride: true);
            else
                _mind.TransferTo(capturedMind, capturedHeretic, ghostCheckOverride: true);

            _stun.TryKnockdown(capturedHeretic, TimeSpan.FromSeconds(1.3), true);

            if (Exists(capturedOrb))
                QueueDel(capturedOrb);
        });
    }

    // ─── Dance of the Brand (passive counter) ─────────────────────────────────

    private void OnHereticDamaged(EntityUid uid, HereticComponent comp, DamageChangedEvent args)
    {
        if (comp.CurrentPath != HereticPath.Blade) return;
        if (!_heretic.HasKnowledge(comp, "KnowledgeDanceOfBrand")) return;
        if (args.DamageDelta == null || args.DamageDelta.GetTotal() <= FixedPoint2.Zero) return;
        if (args.Origin is not EntityUid attacker || attacker == uid || Deleted(attacker)) return;

        var now = _timing.CurTime;
        if (_danceOfBrandCooldowns.TryGetValue(uid, out var cdEnds) && now < cdEnds)
            return;

        var holdsHereticBlade = false;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!_tag.HasTag(held, HereticBladeTag))
                continue;

            holdsHereticBlade = true;
            break;
        }

        if (!holdsHereticBlade) return;

        _danceOfBrandCooldowns[uid] = now + DanceOfBrandCooldown;

        var counter = new DamageSpecifier();
        counter.DamageDict["Slash"] = FixedPoint2.New(15);
        _damage.TryChangeDamage(attacker, counter, ignoreResistances: false, origin: uid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-dance-of-brand"), uid, uid, PopupType.Medium);
    }

    // ─── Blade ────────────────────────────────────────────────────────────────

    private void OnRealignment(EntityUid uid, HereticComponent comp, HereticRealignmentActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        args.Handled = true;

        var cooldown = TimeSpan.FromSeconds(6 + 6 * comp.RealignmentLevel);

        RemCompDeferred<KnockedDownComponent>(uid);
        RemCompDeferred<StunnedComponent>(uid);
        EnsureComp<PacifiedComponent>(uid);
        if (TryComp<StaminaComponent>(uid, out var staminaComp))
            _stamina.TakeStaminaDamage(uid, -staminaComp.StaminaDamage, visual: false);

        _alert.ShowAlert(uid, "HereticRealignment");

        var captured = uid;
        Timer.Spawn(TimeSpan.FromSeconds(8), () =>
        {
            if (!Deleted(captured))
            {
                RemCompDeferred<PacifiedComponent>(captured);
                _alert.ClearAlert(captured, "HereticRealignment");
            }
        });

        if (comp.RealignmentLevel < 10)
            comp.RealignmentLevel++;
        Dirty(uid, comp);

        _actions.SetCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp), cooldown);

        Timer.Spawn(TimeSpan.FromSeconds(90), () =>
        {
            if (Deleted(captured)) return;
            if (!TryComp<HereticComponent>(captured, out var h)) return;
            if (h.RealignmentLevel > 0)
            {
                h.RealignmentLevel--;
                Dirty(captured, h);
            }
        });

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-realignment"), uid, uid, PopupType.Large);
    }

    private void OnSanguineSurge(EntityUid uid, HereticComponent comp, HereticSanguineSurgeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(25);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 2f))
        {
            if (ent.Owner == uid) continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        Spawn("HereticEffectCleave", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-sanguine-surge"), uid, uid, PopupType.Medium);
    }

    private void OnCleave(EntityUid uid, HereticComponent comp, HereticCleaveActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // Remove all bleeding from owner (SS13: remove all wounds on cast)
        _bloodstream.TryModifyBleedAmount(uid, -9999f);

        var healDmg = new DamageSpecifier();
        healDmg.DamageDict["Slash"] = FixedPoint2.New(-15);

        var targetCoords = args.Target;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(targetCoords, 1.5f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(15);
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _damage.TryChangeDamage(uid, healDmg, ignoreResistances: true);
            Spawn("HereticEffectCleave", Transform(ent.Owner).Coordinates);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cleave"), uid, uid, PopupType.Medium);
    }

    private void OnSummonBlades(EntityUid uid, HereticComponent comp, HereticSummonBladesActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (comp.OrbitingBlades.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-blades-already-active"), uid, uid, PopupType.Small);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        var bladeLimit = comp.CurrentPath switch
        {
            HereticPath.Blade  => 4,
            HereticPath.Flesh  => 3,
            HereticPath.Lock   => 2,
            _                  => 1,
        };
        if (!comp.UnlimitedBlades && comp.BladesCreated >= bladeLimit)
        {
            _popup.PopupEntity(Loc.GetString("heretic-blades-limit-reached"), uid, uid, PopupType.SmallCaution);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var delay = i * 660;
            Timer.Spawn(delay, () =>
            {
                if (!TryComp<HereticComponent>(uid, out var hComp)) return;
                _heretic.SpawnOrbitingBlade(uid, hComp);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
            });
        }

        _popup.PopupEntity(Loc.GetString("heretic-furious-steel"), uid, uid, PopupType.Medium);
    }

    private void OnFuriousSteel(EntityUid uid, HereticComponent comp, HereticFuriousSteelActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (comp.OrbitingBlades.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-no-blades"), uid, uid, PopupType.Small);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        var shooterPos = _xform.GetWorldPosition(Transform(uid));
        var targetPos = _xform.ToMapCoordinates(args.Target).Position;
        var direction = targetPos - shooterPos;

        if (direction == Vector2.Zero)
        {
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        _heretic.TryConsumeOrbitingBlade(uid, comp);

        var blade = Spawn("HereticThrownBlade", Transform(uid).Coordinates);
        _gun.ShootProjectile(blade, direction, Vector2.Zero, uid, uid, 9f);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-furious-steel-thrown"), uid, uid, PopupType.Large);

        if (comp.OrbitingBlades.Count > 0)
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));

        // SS13: protective_blades/recharging — 60s recharge after ascension
        if (HasComp<HereticMaelstromOfSilverComponent>(uid))
        {
            Timer.Spawn(TimeSpan.FromSeconds(60), () =>
            {
                if (Deleted(uid) || !HasComp<HereticMaelstromOfSilverComponent>(uid)) return;
                _heretic.SpawnOrbitingBlade(uid);
                _popup.PopupEntity(Loc.GetString("heretic-maelstrom-blade-recharged"), uid, uid, PopupType.Small);
            });
        }
    }

    private void OnHereticDamageModify(EntityUid uid, HereticComponent comp, DamageModifyEvent args)
    {
        if (comp.OrbitingBlades.Count == 0) return;
        if (args.Damage.GetTotal() <= FixedPoint2.Zero) return;

        if (!_heretic.TryConsumeOrbitingBlade(uid, comp)) return;

        args.Damage = new DamageSpecifier();
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/parry.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-blade-deflected"), uid, uid, PopupType.Medium);

        // SS13: protective_blades/recharging — 60s recharge after ascension
        if (HasComp<HereticMaelstromOfSilverComponent>(uid))
        {
            Timer.Spawn(TimeSpan.FromSeconds(60), () =>
            {
                if (Deleted(uid) || !HasComp<HereticMaelstromOfSilverComponent>(uid)) return;
                _heretic.SpawnOrbitingBlade(uid);
                _popup.PopupEntity(Loc.GetString("heretic-maelstrom-blade-recharged"), uid, uid, PopupType.Small);
            });
        }
    }


    private void OnWolvesAmongSheep(EntityUid uid, HereticComponent comp, HereticWolvesAmongSheepActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (_lookup.GetEntitiesInRange<HereticArenaComponent>(Transform(uid).Coordinates, 25f).Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-wolves-arena-exists"), uid, uid, PopupType.SmallCaution);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        _chat.TrySendInGameICMessage(uid, "D'M'N XP'NS'N!", InGameICChatType.Speak, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/airlock_open.ogg"), uid);

        var targetCoords = args.Target.SnapToGrid(EntityManager);

        // Периметр 19×19 тайлов (радиус 9)
        var walls = new System.Collections.Generic.List<EntityUid>();
        for (var dx = -9; dx <= 9; dx++)
        {
            for (var dy = -9; dy <= 9; dy++)
            {
                if (System.Math.Abs(dx) != 9 && System.Math.Abs(dy) != 9)
                    continue;
                var wall = Spawn("HereticArenaWall", targetCoords.Offset(new Vector2(dx, dy)));
                walls.Add(wall);
            }
        }

        var marker = Spawn("HereticArenaMarker", targetCoords);
        if (!TryComp<HereticArenaComponent>(marker, out var arena))
            return;

        arena.Heretic = uid;
        arena.Walls.AddRange(walls);

        // Пол арены — визуальные декали rose_stone
        var gridId = _xform.GetGrid(targetCoords);
        if (gridId != null)
        {
            arena.FloorGrid = gridId.Value;
            for (var dx = -8; dx <= 8; dx++)
            {
                for (var dy = -8; dy <= 8; dy++)
                {
                    var stateNum = _random.Next(1, 9);
                    var decalId = $"HereticRoseStone{stateNum}";
                    if (_decals.TryAddDecal(decalId, targetCoords.Offset(new Vector2(dx, dy)), out var addedId))
                        arena.FloorDecals.Add(addedId);
                }
            }
        }

        // Добавить всех не-еретиков в радиусе 9 как участников и выдать тренировочные клинки
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(targetCoords, 9f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;

            _arena.AddParticipant(ent.Owner, marker, arena);

            var blade = Spawn("HereticBladeBase", Transform(ent.Owner).Coordinates);
            if (TryComp<HereticArenaParticipantComponent>(ent.Owner, out var partComp))
                partComp.TrainingBlade = blade;
            _hands.TryPickupAnyHand(ent.Owner, blade);
        }

        _popup.PopupEntity(Loc.GetString("heretic-wolves-start"), uid, uid, PopupType.Large);
    }

    // ─── Rust ─────────────────────────────────────────────────────────────────

    private void OnCorrode(EntityUid uid, HereticComponent comp, HereticCorrodeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not {} target) return;

        // Wiki: huge damage against inorganic/robotic targets
        if (TryComp<DamageableComponent>(target, out var damageable) &&
            damageable.DamageContainerID is { } containerId &&
            (containerId.Id == "Inorganic" || containerId.Id == "Silicon"))
        {
            var inorganicDmg = new DamageSpecifier();
            inorganicDmg.DamageDict["Blunt"] = FixedPoint2.New(500);
            _damage.TryChangeDamage(target, inorganicDmg, ignoreResistances: false);
            Spawn("HereticEffectSmoke", Transform(target).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("heretic-corrode"), target, target, PopupType.LargeCaution);
            return;
        }

        // Toxic Blade: lets Corrode rust through Titanium/Plastitanium-grade structures
        if (_heretic.HasKnowledge(comp, "KnowledgeToxicBlade") &&
            TryComp<DamageableComponent>(target, out var structDamageable) &&
            structDamageable.DamageContainerID is { Id: "StructuralInorganic" })
        {
            var coords = Transform(target).Coordinates;
            QueueDel(target);
            Spawn("HereticRustWall", coords);
            Spawn("HereticEffectSmoke", coords);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("heretic-corrode-structure"), uid, uid, PopupType.LargeCaution);
            return;
        }

        // SS13 Corrode (Mansus Grasp): 10 BRUTE + 80 stamina loss + Knockdown 5s
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = FixedPoint2.New(10);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        _stamina.TakeStaminaDamage(target, 80f, visual: false);
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(5), true);
        Spawn("HereticEffectSmoke", Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-corrode"), target, target, PopupType.MediumCaution);
    }

    private void OnRustCoat(EntityUid uid, HereticComponent comp, HereticRustCoatActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        _heretic.RustTile(args.Target);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-coat"), uid, uid, PopupType.Medium);
    }

    private void OnRustBladeMeleeHit(Entity<HereticRustBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var comp)) return;
        if (comp.CurrentPath != HereticPath.Rust) return;

        var hasMarkOfRust = _heretic.HasKnowledge(comp, "KnowledgeMarkOfRust");
        var hasToxicBlade = _heretic.HasKnowledge(comp, "KnowledgeToxicBlade");
        if (!hasMarkOfRust && !hasToxicBlade) return;

        foreach (var target in args.HitEntities)
        {
            if (hasMarkOfRust && HasComp<RustMarkComponent>(target))
            {
                RemCompDeferred<RustMarkComponent>(target);
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(3), true);
                _popup.PopupEntity(Loc.GetString("heretic-rust-mark-triggered"), args.User, args.User, PopupType.Medium);
            }

            if (hasToxicBlade)
                _vomit.Vomit(target, force: true);
        }
    }

    private void OnRustWave(EntityUid uid, HereticComponent comp, HereticRustWaveActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        // SS13 Patron's Reach (rust_wave): 30 TOX damage, no knockdown
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Caustic"] = FixedPoint2.New(30);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 8f))
        {
            if (ent.Owner == uid) continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        Spawn("HereticEffectEntropicPlume", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-wave"), uid, uid, PopupType.Large);
    }

    private void OnEntropicPlague(EntityUid uid, HereticComponent comp, HereticEntropicPlagueActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not {} target) return;
        // SS13: amok + cloudstruck/disorient; no direct HP damage.
        var blindDuration = TimeSpan.FromSeconds(5);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(target).Coordinates, 4f))
        {
            if (ent.Owner == uid) continue;
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(ent.Owner, TemporaryBlindnessSystem.BlindingStatusEffect, blindDuration, true);
            _hereticEffects.ApplyInsanity(ent.Owner, TimeSpan.FromSeconds(10));
        }
        Spawn("HereticEffectEntropicPlume", Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-entropic-plague"), uid, uid, PopupType.LargeCaution);
    }

    private void OnRustingCrown(EntityUid uid, HereticComponent comp, HereticRustingCrownActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var now = _timing.CurTime;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 8f))
        {
            if (ent.Owner == uid) continue;
            var mark = EnsureComp<HereticRustingCrownMarkComponent>(ent.Owner);
            mark.TicksRemaining = 6;
            mark.NextTick = now + TimeSpan.FromSeconds(5);
            _popup.PopupEntity(Loc.GetString("heretic-rusting-crown-marked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
        }

        Spawn("HereticEffectSmoke", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rusting-crown"), uid, uid, PopupType.Large);
    }

    // ─── Ash (expanded) ──────────────────────────────────────────────────────

    private void OnAshlordRite(EntityUid uid, HereticComponent comp, HereticAshlordRiteActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13: требует 3 обугленных или горящих трупа поблизости
        var corpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 5f))
        {
            if (ent.Owner == uid) continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var mobState)) continue;
            if (mobState.CurrentState != MobState.Dead) continue;
            if (!TryComp<FlammableComponent>(ent.Owner, out var flamCorpse) || flamCorpse.FireStacks <= 0f) continue;
            corpses.Add(ent.Owner);
            if (corpses.Count >= 3) break;
        }

        if (corpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ashlord-rite-fail"), uid, uid, PopupType.SmallCaution);
            args.Handled = false;
            return;
        }

        // Сжигаем трупы
        foreach (var corpse in corpses)
            QueueDel(corpse);

        // Огненное торнадо: несколько взрывов вокруг
        var xformUid = Transform(uid);
        var pos = xformUid.Coordinates;
        Spawn("HereticEffectFireExplosion", pos);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * (Math.PI * 2 / 6);
            var offset = new Vector2((float)Math.Cos(angle) * 2f, (float)Math.Sin(angle) * 2f);
            Spawn("HereticEffectFireExplosion", pos.Offset(offset));
        }

        // AoE: большой огонь + поджог
        var fireDmg = new DamageSpecifier();
        fireDmg.DamageDict["Heat"] = FixedPoint2.New(30);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(pos, 5f))
        {
            if (ent.Owner == uid) continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var ms)) continue;
            if (ms.CurrentState == MobState.Dead) continue;
            _damage.TryChangeDamage(ent.Owner, fireDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(ent.Owner, 5f, ignite: true);
        }

        // Исцеление еретику за ритуал
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = FixedPoint2.New(-40);
        heal.DamageDict["Slash"] = FixedPoint2.New(-40);
        heal.DamageDict["Heat"]  = FixedPoint2.New(-20);
        _damage.TryChangeDamage(uid, heal, ignoreResistances: true);

        // SS13: fire shield — ring of fire around caster for 60s (same as FireRingOath)
        var shieldDmg = new DamageSpecifier();
        shieldDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        StartFireRingOath(uid, shieldDmg, 120);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse_curse2.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ashlord-rite"), uid, uid, PopupType.Large);
    }

    private void OnFireRingOath(EntityUid uid, HereticComponent comp, HereticFireRingOathActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13 fire_sworn: ring of fire around caster for 60s, 5 Heat / 0.5s to nearby mobs in radius 2
        var coords = Transform(uid).Coordinates;
        Spawn("HereticEffectFireExplosion", coords);
        _popup.PopupEntity(Loc.GetString("heretic-fire-ring-oath"), uid, uid, PopupType.Large);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);

        var ringDmg = new DamageSpecifier();
        ringDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        StartFireRingOath(uid, ringDmg, 120); // 60s / 0.5s
    }

    private void StartFireRingOath(EntityUid casterUid, DamageSpecifier ringDmg, int ticksLeft)
    {
        if (_fireRingCancels.Remove(casterUid, out var oldCts))
            oldCts.Cancel();

        var cts = new CancellationTokenSource();
        _fireRingCancels[casterUid] = cts;
        FireRingOathTick(casterUid, ringDmg, ticksLeft, cts.Token);
    }

    private void FireRingOathTick(EntityUid casterUid, DamageSpecifier ringDmg, int ticksLeft, CancellationToken token)
    {
        if (ticksLeft <= 0)
        {
            _fireRingCancels.Remove(casterUid);
            return;
        }

        Timer.Spawn(500, () =>
        {
            if (!Exists(casterUid)) return;
            if (!TryComp<MobStateComponent>(casterUid, out var mobState) || !_mobs.IsAlive(casterUid, mobState)) return;
            foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(casterUid).Coordinates, 2f))
            {
                if (ent.Owner == casterUid) continue;
                if (!_mobs.IsAlive(ent.Owner, ent.Comp)) continue;
                _damage.TryChangeDamage(ent.Owner, ringDmg, ignoreResistances: false);
                _flammable.AdjustFireStacks(ent.Owner, 0.5f, ignite: true);
            }
            FireRingOathTick(casterUid, ringDmg, ticksLeft - 1, token);
        }, token);
    }

    private void OnFireCascade(EntityUid uid, HereticComponent comp, HereticFireCascadeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13 fire_cascade: expanding ring of fire — 3 expanding pulses (inner → outer)
        var targetCoords = args.Target;
        Spawn("HereticEffectFireExplosion", targetCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-fire-cascade"), uid, uid, PopupType.Medium);

        var alreadyHit = new HashSet<EntityUid> { uid };
        var innerDmg = new DamageSpecifier();
        innerDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        var outerDmg = new DamageSpecifier();
        outerDmg.DamageDict["Heat"] = FixedPoint2.New(8);

        // Wave 1 — immediate, radius 1.5
        FireCascadeWave(targetCoords, 1.5f, innerDmg, 1f, alreadyHit);

        // Wave 2 — t+250ms, radius 2.5
        Timer.Spawn(250, () =>
        {
            if (!Exists(uid)) return;
            Spawn("HereticEffectFireExplosion", targetCoords);
            FireCascadeWave(targetCoords, 2.5f, innerDmg, 1.5f, alreadyHit);
        });

        // Wave 3 — t+500ms, radius 4 (outermost, hits harder)
        Timer.Spawn(500, () =>
        {
            if (!Exists(uid)) return;
            Spawn("HereticEffectFireExplosion", targetCoords);
            FireCascadeWave(targetCoords, 4f, outerDmg, 2f, alreadyHit);
        });
    }

    private void FireCascadeWave(EntityCoordinates center, float radius, DamageSpecifier dmg, float fireStacks, HashSet<EntityUid> alreadyHit)
    {
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(center, radius))
        {
            if (alreadyHit.Contains(ent.Owner)) continue;
            if (!_mobs.IsAlive(ent.Owner, ent.Comp)) continue;
            alreadyHit.Add(ent.Owner);
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(ent.Owner, fireStacks, ignite: true);
            Spawn("HereticEffectFireExplosion", Transform(ent.Owner).Coordinates);
        }
    }

    // ─── Void (expanded) ─────────────────────────────────────────────────────

    private void OnVoidPhase(EntityUid uid, HereticComponent comp, HereticVoidPhaseActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        var srcCoords = Transform(uid).Coordinates;

        if (_xform.InRange(srcCoords, args.Target, 2.99f))
        {
            _popup.PopupEntity(Loc.GetString("heretic-void-phase-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-void-phase"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        Spawn("HereticEffectVoidBlinkIn", srcCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), srcCoords);

        var aoe = new DamageSpecifier();
        aoe.DamageDict["Blunt"] = FixedPoint2.New(40);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(srcCoords, 1f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            _damage.TryChangeDamage(ent.Owner, aoe, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 2);
        }

        _xform.SetWorldPosition(uid, _xform.ToMapCoordinates(args.Target).Position);
        var dstCoords = Transform(uid).Coordinates;
        Spawn("HereticEffectVoidBlinkOut", dstCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), dstCoords);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(dstCoords, 1f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            _damage.TryChangeDamage(ent.Owner, aoe, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 2);
        }

        _popup.PopupEntity(Loc.GetString("heretic-void-phase"), uid, uid, PopupType.Medium);
    }

    private void OnVoidPrison(EntityUid uid, HereticComponent comp, HereticVoidPrisonActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var coords = Transform(uid).Coordinates;
        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-void-prison"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/glass_break3.ogg"), coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), coords);

        var targetCoords = _xform.GetMapCoordinates(args.Target);
        var victims = _lookup.GetEntitiesInRange<MobStateComponent>(targetCoords, 3f);
        foreach (var victim in victims)
        {
            if (HasComp<HereticComponent>(victim)) continue;
            if (_mobs.IsDead(victim)) continue;
            _voidPrison.ApplyVoidPrison(victim);
        }

        _popup.PopupEntity(Loc.GetString("heretic-void-prison"), uid, uid, PopupType.Medium);
    }

    private void OnWaveOfDesperation(EntityUid uid, HereticComponent comp, HereticWaveOfDesperationActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        if (!TryComp<CuffableComponent>(uid, out var cuffable) || cuffable.CuffedHandCount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        // Instantly destroy all restraints — no DoAfter, this is an escape spell.
        foreach (var cuffEnt in cuffable.Container.ContainedEntities.ToArray())
            _cuffs.Uncuff(uid, uid, cuffEnt, cuffable);

        var coords = Transform(uid).Coordinates;
        var myPos = _xform.GetWorldPosition(uid);
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = FixedPoint2.New(15);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 3f))
        {
            if (ent.Owner == uid) continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(3), true);
            ApplyWaveMansusTouch(uid, comp, ent.Owner);
            var dir = _xform.GetWorldPosition(ent.Owner) - myPos;
            if (dir.Length() > 0.01f)
                _throw.TryThrow(ent.Owner, dir.Normalized() * 3f, 4f);
        }
        Spawn("HereticEffectRingleader", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/swap.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation"), uid, uid, PopupType.Large);

        // Speed boost for 12 seconds — caster ignores slowdowns until they fall unconscious.
        _movementMod.TryAddMovementSpeedModDuration(uid, "HereticLastResortStatusEffect", TimeSpan.FromSeconds(12), 1.3f);
        _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation-buff"), uid, uid, PopupType.Medium);

        // Caster loses consciousness 12 seconds after the spell fires (not immediately).
        var capturedUid = uid;
        Timer.Spawn(TimeSpan.FromSeconds(12), () =>
        {
            if (Deleted(capturedUid) || !_mobs.IsAlive(capturedUid)) return;
            _stun.TryKnockdown(capturedUid, TimeSpan.FromSeconds(5), true);
        });
    }

    private void ApplyWaveMansusTouch(EntityUid caster, HereticComponent comp, EntityUid target)
    {
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
                _flammable.AdjustFireStacks(target, 2f, ignite: true);
                break;

            case HereticPath.Moon:
                _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(10));
                _brainDamage.AddBrainDamage(target, 20f);
                break;

            case HereticPath.Flesh when _heretic.HasKnowledge(comp, "KnowledgeMarkOfFlesh"):
                _bloodstream.TryModifyBleedAmount(target, 6f);
                break;

            case HereticPath.Void when _heretic.HasKnowledge(comp, "KnowledgeVoidTraveler"):
                _hereticEffects.ApplyVoidChill(target, 1);
                break;

            case HereticPath.Blade when _heretic.HasKnowledge(comp, "KnowledgeSanguineSurge"):
                _bloodstream.TryModifyBleedAmount(target, 6f);
                break;

            case HereticPath.Rust when _heretic.HasKnowledge(comp, "KnowledgeMarkOfRust"):
                EnsureComp<RustMarkComponent>(target);
                break;

        }
    }


    private void OnMaidInMirror(EntityUid uid, HereticComponent comp, HereticMaidInMirrorActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var coords = Transform(uid).Coordinates;
        for (var i = 0; i < 3; i++)
        {
            var angle = i * (Math.PI * 2 / 3);
            var offset = new Vector2((float) Math.Cos(angle) * 1.5f, (float) Math.Sin(angle) * 1.5f);
            Spawn("EffectVoidBlink", coords.Offset(offset));
        }
        Spawn("MobHereticMaidInMirror", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-maid-in-mirror"), uid, uid, PopupType.Large);
    }


    // ─── Blade (expanded) ────────────────────────────────────────────────────

    private void OnRawRitual(EntityUid uid, HereticComponent comp, HereticRawRitualActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(15);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 3f))
        {
            if (ent.Owner == uid) continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        var selfDmg = new DamageSpecifier();
        selfDmg.DamageDict["Slash"] = FixedPoint2.New(10);
        _damage.TryChangeDamage(uid, selfDmg, ignoreResistances: true);
        Spawn("HereticEffectCleave", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-raw-ritual"), uid, uid, PopupType.Medium);
    }


    private void OnStanceOfTornChampion(EntityUid uid, HereticComponent comp, HereticStanceOfTornChampionActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (HasComp<TornChampionStanceComponent>(uid))
        {
            RemComp<TornChampionStanceComponent>(uid);
            _popup.PopupEntity(Loc.GetString("heretic-stance-torn-champion-off"), uid, uid, PopupType.Medium);
        }
        else
        {
            EnsureComp<TornChampionStanceComponent>(uid);
            _popup.PopupEntity(Loc.GetString("heretic-stance-torn-champion-on"), uid, uid, PopupType.Large);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
    }

    private void OnTornChampionBleed(Entity<TornChampionStanceComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= 0.5f;
        args.BleedReductionAmount *= 1.5f;
    }

    private void OnTornChampionDamageModify(EntityUid uid, TornChampionStanceComponent comp, DamageModifyEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero) return;

        if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold)) return;
        if (_damage.GetTotalDamage(uid) < critThreshold * 0.5f) return;

        args.Damage *= 0.7f;
    }

    private void OnTornChampionKnockdownAttempt(Entity<TornChampionStanceComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (!_mobThreshold.TryGetThresholdForState(ent.Owner, MobState.Critical, out var critThreshold)) return;
        if (_damage.GetTotalDamage(ent.Owner) < critThreshold * 0.5f) return;

        args.Cancelled = true;
    }

    private void OnLionhunterRifle(EntityUid uid, HereticComponent comp, HereticLionhunterRifleActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not {} target) return;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(40);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        Spawn("HereticEffectCleave", Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-lionhunter-rifle"), uid, uid, PopupType.Medium);

        // SS220: "Прицеливание даёт теплозрение" — выстрел даёт временное теплозрение
        EnsureComp<ThermalEntityVisionComponent>(uid);
        Timer.Spawn(TimeSpan.FromSeconds(6), () =>
        {
            if (!TerminatingOrDeleted(uid))
                RemComp<ThermalEntityVisionComponent>(uid);
        });
    }

    // ─── Rust (expanded) ─────────────────────────────────────────────────────

    private void OnAggressiveSpread(EntityUid uid, HereticComponent comp, HereticAggressiveSpreadActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        _heretic.SpreadRustNearby(Transform(uid).Coordinates, 2f);
        Spawn("HereticEffectEntropicPlume", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-aggressive-spread"), uid, uid, PopupType.Large);
    }

    private static readonly string[] RustRuneEffects = {
        "HereticSmallRuneEffect1", "HereticSmallRuneEffect4", "HereticSmallRuneEffect7", "HereticSmallRuneEffect10"
    };

    private void OnHereticRustWalkerAggressiveSpread(EntityUid uid, HereticComponent comp, HereticRustWalkerAggressiveSpreadActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var mapCoords = _xform.GetMapCoordinates(uid);
        for (var dx = -2; dx <= 2; dx++)
            for (var dy = -2; dy <= 2; dy++)
            {
                if (dx * dx + dy * dy > 4) continue;
                var pos = new MapCoordinates(mapCoords.Position + new Vector2(dx, dy), mapCoords.MapId);
                if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(pos, 0.4f).Count == 0)
                    Spawn("HereticRustOverlay", pos);
                Spawn(_random.Pick(RustRuneEffects), pos);
            }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_items_tools_welder.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-walker-aggressive-spread"), uid, uid, PopupType.Large);
    }

    private void OnRustConstruction(EntityUid uid, HereticComponent comp, HereticRustConstructionActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (!_heretic.IsTileRusted(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-rust-construction-fail"), uid, uid, PopupType.MediumCaution);
            return;
        }

        var coords = args.Target.SnapToGrid(EntityManager);
        var mapCoords = _xform.ToMapCoordinates(coords);
        var tilePos = mapCoords.Position;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = FixedPoint2.New(10);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.6f))
        {
            if (_mobs.IsDead(ent.Owner)) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;

            var mobPos = _xform.GetMapCoordinates(ent.Owner).Position;
            var dir = mobPos - tilePos;
            if (dir.Length() > 0.01f)
                _throw.TryThrow(ent.Owner, dir.Normalized() * 3f, 3f);

            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(5), true);
        }

        Spawn("HereticRustWall", coords);

        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.4f).Count == 0)
            Spawn("HereticRustOverlay", mapCoords);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/constructform.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-construction"), uid, uid, PopupType.Large);
    }

    private void OnEntropicPlume(EntityUid uid, HereticComponent comp, HereticEntropicPlumeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var casterCoords = _xform.ToMapCoordinates(Transform(uid).Coordinates);
        var targetCoords = _xform.ToMapCoordinates(args.Target);

        var dir = targetCoords.Position - casterCoords.Position;
        if (dir.LengthSquared() < 0.01f)
            dir = new Vector2(1f, 0f);
        dir = dir.Normalized();

        var right = new Vector2(dir.Y, -dir.X);

        var plumeEnt = Spawn("HereticEffectEntropicPlume", Transform(uid).Coordinates);
        _xform.SetWorldRotation(plumeEnt, Angle.FromWorldVec(dir));

        int[] halfWidths = { 0, 1, 2 };
        var blocked = new bool[5]; // columns -2..2 mapped to indices 0..4

        for (var row = 0; row < 3; row++)
        {
            var hw = halfWidths[row];
            for (var col = -hw; col <= hw; col++)
            {
                var idx = col + 2;
                if (blocked[idx])
                    continue;

                var tilePos = casterCoords.Position + dir * (row + 1) + right * col;
                var tileCoords = new MapCoordinates(tilePos, casterCoords.MapId);

                var isWall = false;
                foreach (var ent in _lookup.GetEntitiesInRange<PhysicsComponent>(tileCoords, 0.4f, LookupFlags.Static))
                {
                    if ((ent.Comp.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                    {
                        isWall = true;
                        break;
                    }
                }

                if (isWall)
                {
                    blocked[idx] = true;
                    continue;
                }

                if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(tileCoords, 0.4f).Count == 0)
                    Spawn("HereticRustOverlay", tileCoords);

                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(tileCoords, 0.5f))
                {
                    if (mob.Owner == uid || HasComp<HereticComponent>(mob.Owner))
                        continue;

                    EnsureComp<HereticAmokComponent>(mob.Owner);
                    Spawn("HereticEffectCloudSwirl", Transform(mob.Owner).Coordinates);
                }
            }
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-entropic-plume"), uid, uid, PopupType.Large);
    }

    // ─── Ascension Ash flame abilities ───────────────────────────────────────

    private void OnHereticFlameOath(EntityUid uid, HereticComponent comp, HereticAshSpiritFlameOathActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        _ashSpiritSystem.StartFlameOath(uid, 300);
    }

    private void OnGreatFireCascade(EntityUid uid, HereticComponent comp, HereticGreatFireCascadeActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-great-fire-cascade"), uid, uid, PopupType.Large);

        var alreadyHit = new HashSet<EntityUid> { uid };
        FireCascadeRing(uid, 1, alreadyHit);
    }

    private void FireCascadeRing(EntityUid uid, int radius, HashSet<EntityUid> alreadyHit)
    {
        if (radius > 6) return;

        var origin = Transform(uid).Coordinates;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Heat"] = FixedPoint2.New(10);

        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
        {
            if ((int) Math.Round(Math.Sqrt(dx * dx + dy * dy)) != radius) continue;
            var tileCoords = origin.Offset(new Vector2(dx, dy));
            Spawn("HereticAshSpiritFire", tileCoords);

            foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(tileCoords, 0.7f))
            {
                if (alreadyHit.Contains(ent.Owner)) continue;
                if (!_mobs.IsAlive(ent.Owner, ent.Comp)) continue;
                alreadyHit.Add(ent.Owner);
                _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
                _flammable.AdjustFireStacks(ent.Owner, 3f, ignite: true);
                Spawn("HereticEffectFireExplosion", Transform(ent.Owner).Coordinates);
            }
        }

        Timer.Spawn(300, () =>
        {
            if (!Exists(uid)) return;
            FireCascadeRing(uid, radius + 1, alreadyHit);
        });
    }

    // ─── Ascension abilities ──────────────────────────────────────────────────

    public void ExecutePathAscension(EntityUid uid, HereticComponent comp)
    {
        var coords = Transform(uid).Coordinates;
        var myPos = _xform.GetWorldPosition(uid);

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
            {
                EntityUid? ringAction = null;
                _actions.AddAction(uid, ref ringAction, AshSpiritFlameOathActionId);
                if (ringAction.HasValue) _heretic.AddGrantedAction(uid, comp, ringAction.Value);

                EntityUid? cascadeAction = null;
                _actions.AddAction(uid, ref cascadeAction, GreatFireCascadeActionId);
                if (cascadeAction.HasValue) _heretic.AddGrantedAction(uid, comp, cascadeAction.Value);

                foreach (var action in _actions.GetActions(uid))
                {
                    if (!TryComp<InstantActionComponent>(action.Owner, out var ia)) continue;
                    if (ia.Event is not HereticVolcanoBlastActionEvent) continue;
                    if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                        _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.66));
                    break;
                }
                foreach (var action in _actions.GetActions(uid))
                {
                    if (!TryComp<InstantActionComponent>(action.Owner, out var ia)) continue;
                    if (ia.Event is not HereticAshlordsRebirthActionEvent) continue;
                    if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                        _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.16));
                    break;
                }

                // SS13 traits: TRAIT_NOFIRE, TRAIT_NOBREATH, TRAIT_RESISTCOLD, TRAIT_RESISTHEAT, TRAIT_RESIST*PRESSURE
                if (TryComp<FlammableComponent>(uid, out var flam))
                    flam.Damage = new DamageSpecifier();
                RemComp<RespiratorComponent>(uid);
                if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
                {
                    tempDmg.ColdDamageThreshold = 0f;
                    tempDmg.HeatDamageThreshold = 99999f;
                }
                EnsureComp<PressureImmunityComponent>(uid);

                _popup.PopupEntity(Loc.GetString("heretic-ascension-ash"), uid, uid, PopupType.Large);
                break;
            }

            case HereticPath.Moon:
            {
                // Взрыв разума в радиусе 8f
                var headExplosionDmg = new DamageSpecifier();
                headExplosionDmg.DamageDict["Cellular"] = FixedPoint2.New(30);
                foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 8f))
                {
                    if (ent.Owner == uid) continue;
                    _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(ent.Owner, TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(8), true);
                    if (HasComp<MindShieldComponent>(ent.Owner))
                        _damage.TryChangeDamage(ent.Owner, headExplosionDmg, ignoreResistances: false);
                    else
                        _hereticEffects.ApplyInsanity(ent.Owner, TimeSpan.FromSeconds(90));
                }

                // SS13 on_gain: немедленно конвертировать ~20% живого экипажа по всей станции
                var eligibleCrew = new List<EntityUid>();
                var crewQuery = EntityQueryEnumerator<MobStateComponent>();
                while (crewQuery.MoveNext(out var mobUid, out var mobState))
                {
                    if (mobUid == uid) continue;
                    if (!_mobs.IsAlive(mobUid, mobState)) continue;
                    if (HasComp<HereticComponent>(mobUid)) continue;
                    if (HasComp<HereticMoonConvertedComponent>(mobUid)) continue;
                    if (HasComp<MindShieldComponent>(mobUid)) continue;
                    eligibleCrew.Add(mobUid);
                }
                var convertCount = Math.Max(1, eligibleCrew.Count / 5);
                _random.Shuffle(eligibleCrew);
                for (var i = 0; i < Math.Min(convertCount, eligibleCrew.Count); i++)
                {
                    var target = eligibleCrew[i];
                    _moonAmulet.TryMoonConvert(target, 0f);
                    Spawn("HereticMoonAmulet", Transform(target).Coordinates);
                }

                if (TryComp<HumanoidProfileComponent>(uid, out var ascHumanoid) &&
                    _prototype.Resolve(ascHumanoid.Species, out var ascSpeciesProto))
                {
                    var casterMeta = MetaData(uid);
                    for (var i = 0; i < 5; i++)
                    {
                        var illusionAngle  = _random.NextFloat(0f, MathF.PI * 2f);
                        var illusionDist   = _random.NextFloat(1f, 5f);
                        var illusionOffset = new Vector2(MathF.Cos(illusionAngle) * illusionDist, MathF.Sin(illusionAngle) * illusionDist);
                        var illusion = Spawn(ascSpeciesProto.Prototype, coords.Offset(illusionOffset));
                        _visualBody.CopyAppearanceFrom(uid, illusion);
                        var illusionCoords = Transform(illusion).Coordinates;
                        var slotEnum = _inventory.GetSlotEnumerator(uid);
                        while (slotEnum.NextItem(out var item, out var slot))
                        {
                            var protoId = MetaData(item).EntityPrototype?.ID;
                            if (protoId == null) continue;
                            var copy = Spawn(protoId, illusionCoords);
                            _inventory.TryEquip(illusion, copy, slot.Name, silent: true);
                        }
                        RemComp<DamageableComponent>(illusion);
                        RemComp<MindContainerComponent>(illusion);
                        RemComp<SSDIndicatorComponent>(illusion);
                        RemComp<InputMoverComponent>(illusion);
                        RemComp<MobMoverComponent>(illusion);
                        _visibility.SetLayer(illusion, (ushort) VisibilityFlags.HereticIllusion);
                        EnsureComp<TimedDespawnComponent>(illusion).Lifetime = 30f;
                        EnsureComp<HereticMoonIllusionComponent>(illusion);
                        EnsureComp<HereticMoonIllusionMovementComponent>(illusion);
                        EnsureComp<CanMoveInAirComponent>(illusion);
                        _metaData.SetEntityName(illusion, casterMeta.EntityName);
                        _metaData.SetEntityDescription(illusion, casterMeta.EntityDescription);
                    }
                }
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Heretic/ascend_moon.ogg"), uid);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-moon"), uid, uid, PopupType.Large);
                EnsureComp<HereticMoonAscendedComponent>(uid);
                break;
            }

            case HereticPath.Flesh:
            {
                _actions.AddAction(uid, ShedHumanFormProto);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-flesh"), uid, uid, PopupType.Large);
                break;
            }

            case HereticPath.Void:
            {
                // SS13: TRAIT_RESISTLOWPRESSURE
                EnsureComp<PressureImmunityComponent>(uid);
                // SS13: TRAIT_NEGATES_GRAVITY + TRAIT_MOVE_FLYING
                var gravity = EnsureComp<MovementIgnoreGravityComponent>(uid);
                gravity.Weightless = true;
                // SS13: RegisterSignal(COMSIG_ATOM_PRE_BULLET_ACT) → 75% deflect back
                var reflect = EnsureComp<ReflectComponent>(uid);
                reflect.ReflectProb = 0.75f;
                // SS13: sound_loop = new(user, TRUE, TRUE) + COMSIG_LIVING_LIFE
                EnsureComp<HereticVoidAscendedComponent>(uid);
                Spawn("HereticEffectSpaceExplosion", coords);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-void"), uid, uid, PopupType.Large);
                break;
            }

            case HereticPath.Blade:
            {
                EnsureComp<HereticMaelstromOfSilverComponent>(uid);
                _heretic.ClearOrbitingBlades(uid, comp);
                _heretic.SpawnOrbitingBlade(uid, comp);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/unsheath.ogg"), uid, AudioParams.Default.WithVolume(-9.6f));
                for (var i = 1; i < 8; i++)
                {
                    var delayIndex = i;
                    Timer.Spawn(TimeSpan.FromSeconds(delayIndex * 0.25), () =>
                    {
                        if (Deleted(uid)) return;
                        _heretic.SpawnOrbitingBlade(uid);
                        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/unsheath.ogg"), uid, AudioParams.Default.WithVolume(-9.6f));
                    });
                }
                foreach (var action in _actions.GetActions(uid))
                {
                    if (!TryComp<WorldTargetActionComponent>(action.Owner, out var wta)) continue;
                    if (wta.Event is not HereticFuriousSteelActionEvent) continue;
                    if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                        _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.5));
                    break;
                }
                Spawn("HereticEffectCleave", coords);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-blade"), uid, uid, PopupType.Large);
                break;
            }

            case HereticPath.Rust:
            {
                // При авто-триггере: выдаём RustAscended (трупы уже потреблены ритуалом)
                EnsureComp<HereticRustAscendedComponent>(uid);
                _heretic.TriggerRustAscensionWave(coords);
                Spawn("HereticEffectSmoke", coords);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg"), uid);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-rust"), uid, uid, PopupType.Large);
                break;
            }

            case HereticPath.Cosmos:
            {
                RemComp<RespiratorComponent>(uid);
                if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
                {
                    tempDmg.ColdDamageThreshold = 0f;
                    tempDmg.HeatDamageThreshold = 99999f;
                }
                EnsureComp<PressureImmunityComponent>(uid);
                if (TryComp<EyeComponent>(uid, out var eye))
                    _eye.SetDrawFov(uid, false, eye);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_cosmic.ogg"), uid);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos"), uid, uid, PopupType.Large);
                var gazerUid = Spawn("MobHereticStarGazer", coords);
                var gazerComp = EnsureComp<HereticStarGazerComponent>(gazerUid);
                gazerComp.Master = uid;
                break;
            }

            case HereticPath.Lock:
            {
                EnsureComp<HereticLockAscendedComponent>(uid);
                var tearUid = Spawn("HereticLockTear", coords);
                var tearComp = EnsureComp<HereticLockTearComponent>(tearUid);
                tearComp.Master = uid;
                _actions.AddAction(uid, LockShapeshiftProto);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/ascend_knock.ogg"), uid);
                _popup.PopupEntity(Loc.GetString("heretic-ascension-lock"), uid, uid, PopupType.Large);
                break;
            }

        }
    }

    private void OnMaelstromStunned(EntityUid uid, HereticMaelstromOfSilverComponent comp, ref StunnedEvent args)
    {
        // SS13: add_stun_absorption — max 45s absorbed, then 2-min recharge
        if (comp.StunAbsorptionRechargeDoneAt.HasValue && _timing.CurTime < comp.StunAbsorptionRechargeDoneAt.Value)
            return; // absorption depleted, stun passes through

        if (_statusEffects.TryGetTime(uid, "Stun", out var stunTime))
        {
            var remaining = stunTime.Value.Item2 - _timing.CurTime;
            if (remaining > TimeSpan.Zero)
            {
                comp.StunAbsorbedSeconds += (float) remaining.TotalSeconds;
                if (comp.StunAbsorbedSeconds >= 45f)
                {
                    comp.StunAbsorbedSeconds = 0f;
                    comp.StunAbsorptionRechargeDoneAt = _timing.CurTime + TimeSpan.FromMinutes(2);
                    Timer.Spawn(TimeSpan.FromMinutes(2), () =>
                    {
                        if (!Deleted(uid) && TryComp<HereticMaelstromOfSilverComponent>(uid, out var c))
                            c.StunAbsorptionRechargeDoneAt = null;
                    });
                    _popup.PopupEntity(Loc.GetString("heretic-maelstrom-absorption-depleted"), uid, uid, PopupType.MediumCaution);
                }
            }
        }

        _statusEffects.TryRemoveStatusEffect(uid, "Stun");
        _statusEffects.TryRemoveStatusEffect(uid, "KnockedDown");
        _statusEffects.TryRemoveStatusEffect(uid, "SlowedDown");
    }

    private void OnMaelstromKnockdownAttempt(EntityUid uid, HereticMaelstromOfSilverComponent comp, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAscensionRust(EntityUid uid, HereticComponent comp, HereticAscensionRustActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        // Wiki: transmute 3 corpses nearby to Ascend.
        const float corpseSearchRadius = 5f;
        var coords = Transform(uid).Coordinates;
        var corpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, corpseSearchRadius))
        {
            if (ent.Owner == uid) continue;
            if (_mobs.IsDead(ent.Owner, ent.Comp))
                corpses.Add(ent.Owner);
            if (corpses.Count >= 3) break;
        }

        if (corpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ascension-rust-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        foreach (var corpse in corpses)
            Del(corpse);

        // Wiki: while on rust tiles, healing is tripled and resistant to a wide variety of effects.
        EnsureComp<HereticRustAscendedComponent>(uid);

        // SS13: on ascension, rust spreads globally across the station.
        _heretic.TriggerRustAscensionWave(coords);

        Spawn("HereticEffectSmoke", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-rust"), uid, uid, PopupType.Large);
    }

    private void OnAscensionCosmos(EntityUid uid, HereticComponent comp, HereticAscensionCosmosActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        const float corpseSearchRadius = 7f;
        var coords = Transform(uid).Coordinates;
        var markedCorpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, corpseSearchRadius))
        {
            if (ent.Owner == uid) continue;
            if (_mobs.IsDead(ent.Owner, ent.Comp) && HasComp<StarMarkComponent>(ent.Owner))
                markedCorpses.Add(ent.Owner);
            if (markedCorpses.Count >= 3) break;
        }

        if (markedCorpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var xform = Transform(uid);
        var parentUid = xform.ParentUid;
        var center = new Vector2(MathF.Floor(xform.LocalPosition.X) + 0.5f, MathF.Floor(xform.LocalPosition.Y) + 0.5f);
        int passiveLevel = comp.PassiveLevel;

        var cardinals = new[]
        {
            new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 0), new Vector2(-1, 0)
        };
        foreach (var dir in cardinals)
        {
            for (var i = 1; i <= 3; i++)
            {
                var pos = center + dir * i;
                var carpet = Spawn("HereticCosmicCarpet", new EntityCoordinates(parentUid, pos));
                if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
                    field.PassiveLevel = passiveLevel;
            }
        }

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 7f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            _statusEffects.TryAddStatusEffect<StarMarkStatusEffectComponent>(ent.Owner, "StarMarkStatusEffect", TimeSpan.FromSeconds(30), true);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_cosmic_expansion.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos-action"), uid, uid, PopupType.Large);
    }

    private void OnRustAscendedDamageModify(EntityUid uid, HereticRustAscendedComponent comp, DamageModifyEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        var total = args.Damage.GetTotal();
        if (total < FixedPoint2.Zero)
        {
            // Healing — triple it.
            args.Damage *= 3;
        }
        else if (total > FixedPoint2.Zero)
        {
            // Resistance to a wide variety of effects — soften incoming damage.
            args.Damage *= 0.5f;
        }
    }

    private void OnRustAscendedStunned(EntityUid uid, HereticRustAscendedComponent comp, ref StunnedEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        _statusEffects.TryRemoveStatusEffect(uid, "Stun");
        _statusEffects.TryRemoveStatusEffect(uid, "KnockedDown");
        _statusEffects.TryRemoveStatusEffect(uid, "SlowedDown");
    }

    private void OnRustAscendedKnockdownAttempt(EntityUid uid, HereticRustAscendedComponent comp, ref KnockDownAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        args.Cancelled = true;
    }

    private void OnRustAscendedSlipAttempt(EntityUid uid, HereticRustAscendedComponent comp, SlipAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        args.NoSlip = true;
    }

    private void OnRustAscendedElectrocution(EntityUid uid, HereticRustAscendedComponent comp, ElectrocutionAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        args.SiemensCoefficient = 0f;
    }

    private void OnRustAscendedSleepAttempt(EntityUid uid, HereticRustAscendedComponent comp, ref TryingToSleepEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        args.Cancelled = true;
    }

    // ─── Special abilities ────────────────────────────────────────────────────

    private void OnRelentlessHeartbeat(EntityUid uid, HereticComponent comp, HereticRelentlessHeartbeatActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (!_mind.TryGetMind(uid, out var mindId, out _)) return;
        _heretic.AssignNamedTargets(uid, mindId);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), Filter.Entities(uid), false);
        _popup.PopupEntity(Loc.GetString("heretic-relentless-heartbeat"), uid, uid, PopupType.Large);
    }

    private void OnSummonFamiliar(EntityUid uid, HereticComponent comp, HereticSummonFamiliarActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var familiarId = comp.CurrentPath switch
        {
            HereticPath.Ash    => "HereticFamiliarStalker",
            HereticPath.Void   => "HereticFamiliarStalker",
            HereticPath.Moon   => "HereticFamiliarAshWalker",
            HereticPath.Blade  => "HereticFamiliarProphet",
            HereticPath.Flesh  => "HereticFamiliarMoonMass",
            HereticPath.Rust   => "HereticFamiliarMoonMass",
            _                  => "HereticFamiliarStalker"
        };

        var pos = _xform.GetMapCoordinates(uid);
        EntityManager.SpawnEntity(familiarId, pos);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_castsummon.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-summon-familiar"), uid, uid, PopupType.Medium);
    }

    // ─── Unsealed Arts ────────────────────────────────────────────────────────

    private static readonly string[] PaintingEntities =
    [
        "HereticPaintingWeeping",
        "HereticPaintingBeauty",
        "HereticPaintingVines",
        "HereticPaintingRust",
        "HereticPaintingDesire",
    ];

    private void OnUnsealedArts(EntityUid uid, HereticComponent comp, HereticUnsealedArtsActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var coords = args.Target;
        var painting = PaintingEntities[_random.Next(PaintingEntities.Length)];
        Spawn(painting, coords);
        _audio.PlayPvs(new SoundCollectionSpecifier("storageRustle"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-unsealed-arts-placed"), uid, uid, PopupType.Medium);
    }

    private void OnMoonGate(EntityUid uid, HereticComponent comp, HereticMoonGateActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var target = args.Target;

        ApplyMoonGateEffects(uid, target);
        _popup.PopupEntity(Loc.GetString("heretic-moon-gate"), uid, uid, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("heretic-moon-gate-popup"), target, target, PopupType.LargeCaution);
    }

    private void ApplyMoonGateEffects(EntityUid uid, EntityUid target)
    {
        // SS13: living_owner.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20, 140)
        _brainDamage.AddBrainDamage(uid, 20f, isCasterCapped: true);

        // SS13: cast_on.mob_mood.adjust_sanity(-20)
        if (!TryComp<HereticMoonBrainDamageComponent>(target, out var targetBrain))
            targetBrain = AddComp<HereticMoonBrainDamageComponent>(target);

        var targetSanity = targetBrain.Sanity;
        targetBrain.Sanity = Math.Max(0f, targetBrain.Sanity - 20f);

        // SS13: cast_on.adjust_oxy_loss(30)
        var oxyDmg = new DamageSpecifier();
        oxyDmg.DamageDict["Asphyxiation"] = FixedPoint2.New(30);
        _damage.TryChangeDamage(target, oxyDmg, ignoreResistances: false);

        // SS13: cast_on.cause_hallucination(body) + cause_hallucination(heretic/gate)
        _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(10));

        // SS13: cast_on.adjust_organ_loss(ORGAN_SLOT_BRAIN, 30)
        _brainDamage.AddBrainDamage(target, 30f, isCasterCapped: false);

        // SS13 duration formula: ((SANITY_MAXIMUM - sanity) / (SANITY_MAXIMUM - SANITY_INSANE)) * 15s + 1s
        // SANITY_MAXIMUM=150, SANITY_INSANE=40 → divisor = 110
        var durationSecs = Math.Clamp((150f - targetSanity) / 110f * 15f + 1f, 1f, 16f);
        var duration = TimeSpan.FromSeconds(durationSecs);

        // SS13: cast_on.adjust_temp_blindness(duration) + cast_on.adjust_silence(duration)
        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect, duration, true);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", duration, true);

        // SS13: cast_on.adjust_confusion(10 SECONDS) — insanity is the closest SS14 analog
        _hereticEffects.ApplyInsanity(target, TimeSpan.FromSeconds(10));

        // SS13: if(cast_on.mob_mood.sanity < 40) cast_on.AdjustKnockdown(2 SECONDS)
        if (targetSanity < 40f)
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);

        // Level 3: Gate усиливает амулет — canal_amulet на цель
        if (TryComp<HereticComponent>(uid, out var gateHeretic) && gateHeretic.PassiveLevel >= 3)
            _moonAmulet.ApplyChannelAmulet(uid, target, targetBrain);
    }

    // ─── Cosmos blade (combo) ─────────────────────────────────────────────────

    private void OnCosmosBladeMeleeHit(Entity<HereticCosmosBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var herComp)) return;
        if (herComp.CurrentPath != HereticPath.Cosmos) return;

        var now = _timing.CurTime;

        if (!_cosmosCombo.TryGetValue(args.User, out var combo))
        {
            combo = new CosmosComboState();
            _cosmosCombo[args.User] = combo;
        }

        if (combo.ComboCount > 0 && now > combo.ResetAt)
        {
            combo.FirstTarget  = EntityUid.Invalid;
            combo.SecondTarget = EntityUid.Invalid;
            combo.ComboCount   = 0;
        }

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target)) continue;
            if (target == args.User) continue;

            if (HasComp<CosmosMarkComponent>(target))
            {
                if (TryComp<CosmosMarkComponent>(target, out var cosmosMark)
                    && cosmosMark.AnchorEntity.HasValue && !TerminatingOrDeleted(cosmosMark.AnchorEntity.Value))
                    QueueDel(cosmosMark.AnchorEntity.Value);
                RemCompDeferred<CosmosMarkComponent>(target);
                var currentPos = _xform.GetWorldPosition(target);
                var angle = _random.NextFloat(0f, MathF.PI * 2f);
                var dist = _random.NextFloat(3f, 6f);
                var teleportPos = currentPos + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_repulse.ogg"), target);
                Spawn("HereticEffectCosmicCloud", Transform(target).Coordinates);
                _xform.SetWorldPosition(target, teleportPos);
                Spawn("HereticEffectCosmicCloud", Transform(target).Coordinates);
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);
            }

            _statusEffects.TryAddStatusEffect<StarMarkStatusEffectComponent>(target, "StarMarkStatusEffect", TimeSpan.FromSeconds(30), true);

            _damage.TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(5) } },
                ignoreResistances: false);

            if (combo.ComboCount == 0)
            {
                combo.FirstTarget = target;
                combo.ComboCount  = 1;
                combo.ResetAt     = now + TimeSpan.FromSeconds(3);
            }
            else if (combo.ComboCount == 1 && target != combo.FirstTarget)
            {
                combo.SecondTarget = target;
                combo.ComboCount   = 2;
                combo.ResetAt      = now + TimeSpan.FromSeconds(3);

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(14) } },
                    ignoreResistances: false);

                Spawn("HereticEffectSpaceExplosion", Transform(target).Coordinates);
            }
            else if (combo.ComboCount == 2 && target != combo.FirstTarget && target != combo.SecondTarget)
            {
                combo.ComboCount = 3;

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(28) } },
                    ignoreResistances: false);

                Spawn("HereticEffectSpaceExplosion", Transform(target).Coordinates);

                var capturedUser = args.User;
                Timer.Spawn(TimeSpan.FromSeconds(3), () =>
                {
                    if (!_cosmosCombo.TryGetValue(capturedUser, out var c)) return;
                    c.FirstTarget  = EntityUid.Invalid;
                    c.SecondTarget = EntityUid.Invalid;
                    c.ComboCount   = 0;
                });
            }

            break;
        }
    }

    private void OnAnyMobDied(MobStateChangedEvent args)
    {
        // Soul Bottle (Lock path) — placeholder for future soul-capture logic
    }

    // ── Lock: Шейпшифт возвышения ─────────────────────────────────────────────

    private static readonly Dictionary<HereticLockShapeshiftCreature, ProtoId<PolymorphPrototype>> LockShapeshiftProtos = new()
    {
        { HereticLockShapeshiftCreature.RustWalker, "HereticLockPolymorph_RustWalker" },
        { HereticLockShapeshiftCreature.AshOrb,     "HereticLockPolymorph_AshOrb"     },
        { HereticLockShapeshiftCreature.FleshWorm,  "HereticLockPolymorph_FleshWorm"  },
        { HereticLockShapeshiftCreature.RawProphet, "HereticLockPolymorph_RawProphet" },
    };

    private void OnLockShapeshift(EntityUid uid, HereticComponent comp, HereticLockShapeshiftActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        if (!HasComp<HereticLockAscendedComponent>(uid)) return;

        args.Handled = true;

        _ui.TryOpenUi(comp.BuiHolder, HereticLockShapeshiftUiKey.Key, uid);
    }

    private void OnLockShapeshiftSelect(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticLockShapeshiftSelectMessage args)
    {
        var uid = args.Actor;
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        if (!HasComp<HereticLockAscendedComponent>(uid)) return;

        if (!LockShapeshiftProtos.TryGetValue(args.Creature, out var proto))
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_knock.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-lock-shapeshift-activate"), uid, uid, PopupType.Large);
        _polymorph.PolymorphEntity(uid, proto);
    }

}
