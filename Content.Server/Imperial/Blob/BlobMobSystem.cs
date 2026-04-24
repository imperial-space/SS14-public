using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Blob;

public sealed class BlobMobSystem : EntitySystem
{
    private const string BlobFactionId = "Blob";
    private const string BlobRadioChannel = "Blob";
    private const string BlobHiveChannel = "BlobHive";
    private const float BlobTileAllyHealInterval = 1f;
    private const float BlobTileAllyHealAmount = 1f;

    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly BlobChemistrySystem _chemistry = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly BlobInfectionSystem _infection = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private float _blobTileAllyHealAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMobComponent, ComponentStartup>(OnBlobMobStartup);
        SubscribeLocalEvent<BlobMobComponent, ComponentShutdown>(OnBlobMobShutdown);
        SubscribeLocalEvent<BlobInfectedComponent, ComponentStartup>(OnBlobInfectedStartup);
        SubscribeLocalEvent<BlobInfectedComponent, ComponentShutdown>(OnBlobInfectedShutdown);
        SubscribeLocalEvent<BlobMobComponent, EntitySpokeEvent>(OnBlobMobSpoke);
        SubscribeLocalEvent<BlobMouseComponent, EntitySpokeEvent>(OnBlobMouseSpoke);
        SubscribeLocalEvent<BlobInfectedComponent, EntitySpokeEvent>(OnBlobInfectedSpoke);
        SubscribeLocalEvent<NpcFactionMemberComponent, EntitySpokeEvent>(OnBlobFactionSpoke);
        SubscribeLocalEvent<BlobMobComponent, MeleeHitEvent>(OnBlobMobMeleeHit);
    }

    private void OnBlobMobStartup(EntityUid uid, BlobMobComponent component, ComponentStartup args)
    {
        ConfigureMobForOwner(uid, component);
        ConfigureBlobFriendlyCollision(uid);

        if (TryComp<CombatModeComponent>(uid, out var combatMode))
            _combatMode.SetInCombatMode(uid, true, combatMode);
    }

    private void OnBlobMobShutdown(EntityUid uid, BlobMobComponent component, ComponentShutdown args)
    {
        RestoreBlobFriendlyCollision(uid);
    }

    private void OnBlobInfectedStartup(EntityUid uid, BlobInfectedComponent component, ComponentStartup args)
    {
        ConfigureBlobFriendlyCollision(uid);
    }

    private void OnBlobInfectedShutdown(EntityUid uid, BlobInfectedComponent component, ComponentShutdown args)
    {
        RestoreBlobFriendlyCollision(uid);
    }

    public void ConfigureMobForOwner(EntityUid uid, BlobMobComponent component)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (component.OwnerMind is { } ownerMind)
            component.Chemical = GetChemicalForOwner(ownerMind);

        melee.Damage = BuildDamage(uid, component.Chemical);
        Dirty(uid, melee);

        if (TryComp<CombatModeComponent>(uid, out var combatMode))
            _combatMode.SetInCombatMode(uid, true, combatMode);
    }

    public void ConfigureBlobFriendlyCollision(EntityUid uid)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        var blobFriendly = EnsureComp<BlobFriendlyCollisionComponent>(uid);
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (!fixture.Hard || blobFriendly.DisabledFixtureMasks.ContainsKey(fixtureId))
                continue;

            var removedMask = fixture.CollisionMask & (int) CollisionGroup.BlobImpassable;
            if (removedMask == 0)
                continue;

            blobFriendly.DisabledFixtureMasks.Add(fixtureId, removedMask);
            _physics.SetCollisionMask(uid, fixtureId, fixture, fixture.CollisionMask & ~removedMask, fixtures);
        }
    }

    public void RestoreBlobFriendlyCollision(EntityUid uid)
    {
        if (!TryComp<BlobFriendlyCollisionComponent>(uid, out var blobFriendly) ||
            !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (fixtureId, removedMask) in blobFriendly.DisabledFixtureMasks)
        {
            if (!fixtures.Fixtures.TryGetValue(fixtureId, out var fixture))
                continue;

            _physics.SetCollisionMask(uid, fixtureId, fixture, fixture.CollisionMask | removedMask, fixtures);
        }

        RemCompDeferred<BlobFriendlyCollisionComponent>(uid);
    }

    private void OnBlobMobSpoke(EntityUid uid, BlobMobComponent component, ref EntitySpokeEvent args)
    {
        RelayToBlobRadio(uid, ref args);
    }

    private void OnBlobMouseSpoke(EntityUid uid, BlobMouseComponent component, ref EntitySpokeEvent args)
    {
        RelayToBlobRadio(uid, ref args);
    }

    private void OnBlobInfectedSpoke(EntityUid uid, BlobInfectedComponent component, ref EntitySpokeEvent args)
    {
        RelayToBlobRadio(uid, ref args);
    }

    private void OnBlobFactionSpoke(EntityUid uid, NpcFactionMemberComponent component, ref EntitySpokeEvent args)
    {
        if (!_npcFaction.IsMember((uid, component), BlobFactionId))
            return;

        if (HasComp<BlobMobComponent>(uid) || HasComp<BlobOvermindComponent>(uid) || HasComp<BlobMouseComponent>(uid))
            return;

        if (GetBlobOwner(uid) == null)
            return;

        RelayToBlobRadio(uid, ref args);
    }

    private void OnBlobMobMeleeHit(EntityUid uid, BlobMobComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit || component.OwnerMind == null)
            return;

        foreach (var target in args.HitEntities)
        {
            if (HasComp<BlobSporeComponent>(uid) &&
                _infection.TryLatchSporeOntoTarget(uid, target, component.OwnerMind.Value, component.Chemical))
            {
                break;
            }

            if (HasComp<BlobMobComponent>(target) || HasComp<BlobStructureComponent>(target) || HasComp<BlobOvermindComponent>(target))
                continue;

            var canApplySecondaryEffects = TryComp<MobStateComponent>(target, out var mobState) && mobState.CurrentState != MobState.Dead;

            switch (component.Chemical)
            {
                case BlobChemicalType.Toxin:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 5f);

                    if (canApplySecondaryEffects && TryComp<StatusEffectsComponent>(target, out var targetStatusEffects))
                    {
                        _statusEffects.TryAddStatusEffect<MutedComponent>(
                            target,
                            "Muted",
                            TimeSpan.FromSeconds(2),
                            true,
                            targetStatusEffects);
                    }
                    break;
                case BlobChemicalType.Incendiary:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 4f);
                    break;
                case BlobChemicalType.Electromagnetic:
                    if (canApplySecondaryEffects)
                    {
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 4f);
                        if (TryComp<StatusEffectsComponent>(target, out var electromagneticStatusEffects))
                        {
                            _statusEffects.TryAddStatusEffect<MutedComponent>(
                                target,
                                "Muted",
                                TimeSpan.FromSeconds(1),
                                true,
                                electromagneticStatusEffects);
                        }
                    }
                    break;
                case BlobChemicalType.DistributedNeurons:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 3f);
                    break;
                case BlobChemicalType.RadioactiveGel:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 5f);
                    break;
                case BlobChemicalType.LexorinJelly:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 5f);
                    break;
                case BlobChemicalType.CryogenicLiquid:
                    if (canApplySecondaryEffects)
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 4f);
                    break;
                case BlobChemicalType.Sorium:
                    if (canApplySecondaryEffects)
                        ApplySoriumKnockback(uid, target, 2.25f);
                    break;
                case BlobChemicalType.EnvenomedFilaments:
                    if (canApplySecondaryEffects)
                    {
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 5f);
                        _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(1.5));
                    }
                    break;
                case BlobChemicalType.ParalyticToxins:
                    if (canApplySecondaryEffects)
                    {
                        _chemistry.ApplyChemicalEffect(target, component.Chemical, 4f);
                        _stun.TryKnockdown(target, TimeSpan.FromSeconds(1.5), true);
                    }
                    break;
                case BlobChemicalType.KineticGelatin:
                    break;
                case BlobChemicalType.Regenerative:
                    var healing = new DamageSpecifier();
                    healing.DamageDict.Add("Brute", -3);
                    healing.DamageDict.Add("Burn", -3);
                    _damage.TryChangeDamage(uid, healing, true);
                    break;
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _blobTileAllyHealAccumulator += frameTime;
        var shouldHealBlobTileAllies = _blobTileAllyHealAccumulator >= BlobTileAllyHealInterval;
        if (shouldHealBlobTileAllies)
            _blobTileAllyHealAccumulator -= BlobTileAllyHealInterval;

        if (!shouldHealBlobTileAllies)
            return;

        var allyQuery = EntityQueryEnumerator<TransformComponent, DamageableComponent>();
        while (allyQuery.MoveNext(out var uid, out var xform, out var damageable))
        {
            if (HasComp<BlobOvermindComponent>(uid) || HasComp<BlobOvermindControllerComponent>(uid) || HasComp<BlobStructureComponent>(uid))
                continue;

            if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            var ownerMind = GetBlobOwner(uid);
            if (ownerMind is not { } owner || !IsStandingOnOwnedBlobTile(xform, owner))
                continue;

            var healing = BuildBlobTileHealing((uid, damageable));
            if (healing.Empty)
                continue;

            _damage.TryChangeDamage(uid, healing, true);
        }
    }

    private DamageSpecifier BuildBlobTileHealing(Entity<DamageableComponent> target)
    {
        var currentDamage = _damage.GetPositiveDamage(target);
        var healing = new DamageSpecifier();

        foreach (var (damageType, amount) in currentDamage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            healing.DamageDict.Add(damageType, -BlobTileAllyHealAmount);
        }

        return healing;
    }

    private BlobChemicalType GetChemicalForOwner(EntityUid ownerMind)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent, MindContainerComponent>();
        while (query.MoveNext(out _, out var overmind, out var mindContainer))
        {
            if (overmind.BlobId == ownerMind || mindContainer.Mind == ownerMind)
                return overmind.Chemical;
        }

        return BlobChemicalType.Toxin;
    }

    private DamageSpecifier BuildDamage(EntityUid uid, BlobChemicalType chemical)
    {
        var damage = new DamageSpecifier();
        var prototype = MetaData(uid).EntityPrototype?.ID;

        damage.DamageDict.Add("Blunt", GetBaseBluntDamage(prototype, chemical));

        switch (chemical)
        {
            case BlobChemicalType.Toxin:
                damage.DamageDict.Add("Poison", 6);
                break;
            case BlobChemicalType.Incendiary:
                damage.DamageDict.Add("Heat", 6);
                break;
            case BlobChemicalType.Electromagnetic:
                damage.DamageDict.Add("Heat", 8);
                break;
            case BlobChemicalType.DistributedNeurons:
                damage.DamageDict.Add("Poison", 8);
                break;
            case BlobChemicalType.KineticGelatin:
                damage.DamageDict.Add("Stamina", 12);
                break;
            case BlobChemicalType.RadioactiveGel:
                damage.DamageDict.Add("Poison", 5);
                damage.DamageDict.Add("Radiation", 4);
                break;
            case BlobChemicalType.LexorinJelly:
                damage.DamageDict.Add("Asphyxiation", 18);
                break;
            case BlobChemicalType.CryogenicLiquid:
                damage.DamageDict.Add("Cold", 6);
                damage.DamageDict.Add("Stamina", 8);
                break;
            case BlobChemicalType.Sorium:
                damage.DamageDict.Add("Stamina", 12);
                break;
            case BlobChemicalType.EnvenomedFilaments:
                damage.DamageDict.Add("Poison", 9);
                damage.DamageDict.Add("Stamina", 6);
                break;
            case BlobChemicalType.ParalyticToxins:
                damage.DamageDict.Add("Poison", 6);
                damage.DamageDict.Add("Stamina", 5);
                break;
            case BlobChemicalType.Regenerative:
                damage.DamageDict.Add("Poison", 2);
                break;
        }

        return damage;
    }

    private int GetBaseBluntDamage(string? prototype, BlobChemicalType chemical)
    {
        return prototype switch
        {
            "MobBlobbernaut" when chemical == BlobChemicalType.Regenerative => 22,
            "MobBlobbernaut" when chemical == BlobChemicalType.Sorium => 17,
            "MobBlobbernaut" when chemical == BlobChemicalType.KineticGelatin => 24,
            "MobBlobbernaut" when chemical == BlobChemicalType.ParalyticToxins => 15,
            "MobBlobbernaut" when chemical == BlobChemicalType.RadioactiveGel => 12,
            "MobBlobbernaut" when chemical == BlobChemicalType.LexorinJelly => 10,
            "MobBlobbernaut" when chemical == BlobChemicalType.CryogenicLiquid => 12,
            "MobBlobbernaut" when chemical == BlobChemicalType.DistributedNeurons => 16,
            "MobBlobbernaut" when chemical == BlobChemicalType.Electromagnetic => 15,
            "MobBlobbernaut" => 18,
            _ when chemical == BlobChemicalType.Regenerative => 6,
            _ when chemical == BlobChemicalType.Sorium => 5,
            _ when chemical == BlobChemicalType.KineticGelatin => 7,
            _ when chemical == BlobChemicalType.ParalyticToxins => 3,
            _ when chemical == BlobChemicalType.RadioactiveGel => 2,
            _ when chemical == BlobChemicalType.LexorinJelly => 2,
            _ when chemical == BlobChemicalType.CryogenicLiquid => 2,
            _ when chemical == BlobChemicalType.DistributedNeurons => 3,
            _ when chemical == BlobChemicalType.Electromagnetic => 3,
            _ => 4,
        };
    }

    private void ApplySoriumKnockback(EntityUid sourceUid, EntityUid targetUid, float distance)
    {
        var sourceCoords = _transform.ToMapCoordinates(Transform(sourceUid).Coordinates);
        var targetCoords = _transform.ToMapCoordinates(Transform(targetUid).Coordinates);
        if (sourceCoords.MapId != targetCoords.MapId)
            return;

        var pushDir = targetCoords.Position - sourceCoords.Position;
        if (pushDir.LengthSquared() < 0.01f)
            pushDir = new Vector2(1f, 0f);
        else
            pushDir = pushDir.Normalized() * distance;

        _throwing.TryThrow(targetUid, pushDir, 8f);
    }

    private bool IsStandingOnOwnedBlobTile(TransformComponent xform, EntityUid ownerMind)
    {
        if (xform.GridUid is not { } gridUid)
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var structure, out var structureXform, out var metaData))
        {
            if (structure.OwnerMind != ownerMind)
                continue;

            if (structureXform.GridUid != gridUid)
                continue;

            var prototype = metaData.EntityPrototype?.ID;
            if (prototype != "BlobTile" && prototype != "BlobTileShield" && prototype != "BlobTileReflective")
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, structureXform.Coordinates);
            if (structureTile == tile)
                return true;
        }

        return false;
    }

    public void RelayToBlobRadio(EntityUid speakerUid, ref EntitySpokeEvent args, EntityUid? radioSource = null)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        var channelId = args.Channel?.ID == BlobHiveChannel
            ? BlobHiveChannel
            : BlobRadioChannel;

        SendToBlobRadio(speakerUid, args.Message, channelId, radioSource);
        args.Channel = null;
    }

    public void SendToBlobRadio(EntityUid speakerUid, string message, string channelId = BlobRadioChannel, EntityUid? radioSource = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (channelId == BlobRadioChannel || channelId == BlobHiveChannel)
        {
            DispatchBlobRadioFallback(speakerUid, message, GetBlobOwner(speakerUid), channelId);
            return;
        }

        _radio.SendRadioMessage(speakerUid, message, channelId, radioSource ?? speakerUid);
    }

    public void DispatchBlobRadioFallback(EntityUid source, string message, EntityUid? blobId, string channelId)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var filter = Filter.Empty();
        var hasRecipients = false;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached)
                continue;

            if (!IsBlobRadioRecipient(attached, blobId))
                continue;

            filter.AddPlayer(session);
            hasRecipients = true;
        }

        if (!hasRecipients)
            return;

        var channel = _prototypes.Index<RadioChannelPrototype>(channelId);
        var wrappedMessage = $"[{channel.LocalizedName}] {FormattedMessage.EscapeText(Name(source))}: \"{FormattedMessage.EscapeText(message)}\"";
        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, source, false, true, null);
    }

    public EntityUid? GetBlobOwner(EntityUid uid)
    {
        if (TryComp<BlobOvermindComponent>(uid, out var overmind))
            return overmind.BlobId;

        if (TryComp<BlobInfectedComponent>(uid, out var infected))
            return infected.OwnerMind;

        if (TryComp<BlobMobComponent>(uid, out var blobMob))
            return blobMob.OwnerMind;

        if (TryComp<BlobStructureComponent>(uid, out var structure))
            return structure.OwnerMind;

        if (TryComp<BlobOvermindControllerComponent>(uid, out var controller) &&
            controller.Overmind is { } overmindUid &&
            TryComp<BlobOvermindComponent>(overmindUid, out var controllerOvermind))
            return controllerOvermind.BlobId;

        return null;
    }

    public bool IsBlobRadioRecipient(EntityUid entity, EntityUid? blobId)
    {
        if (blobId is not { } owner)
        {
                 return _npcFaction.IsMember(entity, BlobFactionId) ||
                   HasComp<BlobMouseComponent>(entity) ||
                   HasComp<BlobOvermindComponent>(entity) ||
                   HasComp<BlobOvermindControllerComponent>(entity) ||
                   HasComp<BlobInfectedComponent>(entity);
        }

        return IsFriendlyBlobEntity(entity, owner);
    }

    public void EnsureBlobRadio(EntityUid uid)
    {
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(uid);
        transmitter.Channels.Add(BlobRadioChannel);
        transmitter.Channels.Add(BlobHiveChannel);
        Dirty(uid, transmitter);

        EnsureComp<IntrinsicRadioReceiverComponent>(uid);

        var activeRadio = EnsureComp<ActiveRadioComponent>(uid);
        activeRadio.Channels.Add(BlobRadioChannel);
        activeRadio.Channels.Add(BlobHiveChannel);
        Dirty(uid, activeRadio);
    }

    private bool IsFriendlyBlobEntity(EntityUid entity, EntityUid blobId)
    {
        if (TryComp<BlobStructureComponent>(entity, out var structure))
            return structure.OwnerMind == blobId;

        if (TryComp<BlobMobComponent>(entity, out var mob))
            return mob.OwnerMind == blobId;

        if (TryComp<BlobInfectedComponent>(entity, out var infected))
            return infected.OwnerMind == blobId;

        if (HasComp<BlobMouseComponent>(entity))
            return true;

        if (TryComp<BlobOvermindControllerComponent>(entity, out var controller) &&
            controller.Overmind is { } overmindUid &&
            TryComp<BlobOvermindComponent>(overmindUid, out var controllerOvermind))
        {
            return controllerOvermind.BlobId == blobId;
        }

        if (TryComp<BlobOvermindComponent>(entity, out var overmind))
            return overmind.BlobId == blobId;

        return false;
    }
}