using System.Linq;
using System.Numerics;
using Content.Server.Popups;
using Content.Server.Engineering.Components;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.MedievalFactionFlag.Components;
using Content.Server.Imperial.Medieval.Engineering;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.CapturePoint.Systems;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AchievementSystem _achievement = default!;

    private float _updateTimer;
    private const float UpdateInterval = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CapturePointComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CapturePointComponent, StartCapturePointMessage>(OnStartCapture);
        SubscribeLocalEvent<SpawnAfterInteractComponent, BeforeSpawnAfterInteractEvent>(OnBeforeSpawn);
    }

    private void OnInteractHand(Entity<CapturePointComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var comp = ent.Comp;
        var actors = _userInterface.GetActors(ent.Owner, CapturePointUiKey.Key);

        if (actors.Any())
        {
            var message = Loc.GetString("machine-already-in-use", ("machine", ent.Owner));
            _popup.PopupEntity(message, args.User);
            return;
        }

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var member))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-no-faction"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!IsFactionAllowed(comp, member.Faction))
        {
            var names = string.Join(Loc.GetString("medieval-capture-point-faction-list-separator"),
                comp.AllowedFactions.Select(GetFactionDisplayName));
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-faction-not-allowed", ("factions", names)), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (comp.State == CapturePointState.Capturing)
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-already-capturing"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (comp.State == CapturePointState.Cooldown)
        {
            var remaining = comp.CooldownDuration - (float)(_timing.CurTime - comp.CooldownStartTime).TotalSeconds;
            if (remaining > 0)
            {
                var mins = (int)(remaining / 60);
                var secs = (int)(remaining % 60);
                _popup.PopupEntity(Loc.GetString("medieval-capture-point-on-cooldown", ("minutes", mins), ("seconds", secs)), ent, args.User, PopupType.MediumCaution);
                return;
            }
            comp.State = CapturePointState.Idle;
        }

        var allies = GetFactionEntitiesInRadius(ent, member.Faction);
        var allyNames = allies.Select(a => Name(a)).ToList();
        var estimatedDuration = CalculateCaptureDuration(comp, allies.Count);

        var enoughParticipants = allies.Count >= comp.MinParticipants;
        var isDominant = IsFactionDominant(ent, member.Faction);
        var noGlobalCapture = !IsAnyPointCapturing(ent.Owner);

        var canStart = enoughParticipants && isDominant && noGlobalCapture;
        string? reason = null;
        if (!enoughParticipants)
            reason = Loc.GetString("medieval-capture-point-min-participants", ("count", comp.MinParticipants));
        else if (!isDominant)
            reason = Loc.GetString("medieval-capture-point-not-dominant");
        else if (!noGlobalCapture)
            reason = Loc.GetString("medieval-capture-point-global-lock");

        _ui.SetUiState(ent.Owner,
            CapturePointUiKey.Key,
            new CapturePointBuiState(
            member.Faction,
            allyNames,
            estimatedDuration,
            canStart,
            reason));

        _ui.TryOpenUi(ent.Owner, CapturePointUiKey.Key, args.User);
    }

    private void OnStartCapture(Entity<CapturePointComponent> ent, ref StartCapturePointMessage msg)
    {
        var user = msg.Actor;
        var comp = ent.Comp;

        if (comp.State != CapturePointState.Idle)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(user, out var member))
            return;

        if (!IsFactionAllowed(comp, member.Faction))
            return;

        var allies = GetFactionEntitiesInRadius(ent, member.Faction);
        if (allies.Count < comp.MinParticipants)
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-not-enough-participants"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (!IsFactionDominant(ent, member.Faction))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-not-dominant"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (IsAnyPointCapturing(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-global-lock"), ent, user, PopupType.MediumCaution);
            return;
        }

        comp.State = CapturePointState.Capturing;
        comp.CapturingFaction = member.Faction;
        comp.CaptureStartTime = _timing.CurTime;
        comp.CurrentCaptureDuration = CalculateCaptureDuration(comp, allies.Count);
        comp.LastEmptyTime = null;
        comp.NextFactionIncome = _timing.CurTime + comp.FactionIncomeInterval;
        Dirty(ent);

        _ui.CloseUi(ent.Owner, CapturePointUiKey.Key);

        ApplyStatusEffectToEnemyFaction(ent);
        NotifyEnemyLeader(ent);

        _popup.PopupEntity(Loc.GetString("medieval-capture-point-capture-started", ("pointName", comp.PointName)), ent, PopupType.Large);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer -= UpdateInterval;

        var query = EntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            UpdateCapturePoint((uid, comp));
            UpdateFactionIncome((uid, comp));
        }
    }

    private void UpdateCapturePoint(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;

        var newCounts = new int[comp.AllowedFactions.Count];

        for (var i = 0; i < comp.AllowedFactions.Count; i++)
        {
            var entities = GetFactionEntitiesInRadius(ent, comp.AllowedFactions[i]);
            newCounts[i] = entities.Count;
        }

        var countsChanged = false;
        for (var i = 0; i < newCounts.Length && i < comp.FactionCounts.Length; i++)
        {
            if (newCounts[i] == comp.FactionCounts[i])
                continue;

            countsChanged = true;
            break;
        }
        comp.FactionCounts = newCounts;

        if (countsChanged)
        {
            var actors = _userInterface.GetActors(ent.Owner, CapturePointUiKey.Key);
            var viewer = actors.FirstOrDefault();

            if (viewer != default && TryComp<MedievalFactionMemberComponent>(viewer, out var member))
            {
                var allies = GetFactionEntitiesInRadius(ent, member.Faction);
                var allyNames = allies.Select(a => Name(a)).ToList();
                var estimatedDuration = CalculateCaptureDuration(comp, allies.Count);
                var canStart = allies.Count >= comp.MinParticipants;
                var reason = canStart
                    ? null
                    : Loc.GetString("medieval-capture-point-not-enough-participants");

                _ui.SetUiState(ent.Owner,
                    CapturePointUiKey.Key,
                    new CapturePointBuiState(
                    member.Faction,
                    allyNames,
                    estimatedDuration,
                    canStart,
                    reason));
            }
        }

        switch (comp.State)
        {
            case CapturePointState.Cooldown:
            {
                var elapsed = (float)(_timing.CurTime - comp.CooldownStartTime).TotalSeconds;
                var cooldownDuration = comp.OwningFaction == null
                    ? comp.CooldownDuration / 2
                    : comp.CooldownDuration;

                if (!(elapsed >= cooldownDuration))
                    return;

                comp.State = CapturePointState.Idle;
                Dirty(ent);
                return;
            }
            case CapturePointState.Idle:
                return;
        }

        var totalInZone = newCounts.Sum();

        if (totalInZone < comp.MinParticipants)
        {
            comp.LastEmptyTime ??= _timing.CurTime;

            var emptyElapsed = (float)(_timing.CurTime - comp.LastEmptyTime.Value).TotalSeconds;
            if (emptyElapsed >= comp.AbandonTimeout)
            {
                FinishCapture(ent, null);
                return;
            }
        }
        else
        {
            comp.LastEmptyTime = null;
        }

        var captureElapsed = (float)(_timing.CurTime - comp.CaptureStartTime).TotalSeconds;
        if (captureElapsed >= comp.CurrentCaptureDuration)
        {
            ProtoId<MedievalFactionPrototype>? winner = null;
            var maxCount = -1;
            var tie = false;

            for (var i = 0; i < comp.AllowedFactions.Count; i++)
            {
                var count = i < newCounts.Length ? newCounts[i] : 0;
                if (count > maxCount)
                {
                    maxCount = count;
                    winner = comp.AllowedFactions[i];
                    tie = false;
                }
                else if (count == maxCount)
                {
                    tie = true;
                }
            }

            FinishCapture(ent, tie ? null : winner);
            return;
        }

        Dirty(ent);
    }

    private void UpdateFactionIncome(Entity<CapturePointComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextFactionIncome)
            return;

        ent.Comp.NextFactionIncome = _timing.CurTime + ent.Comp.FactionIncomeInterval;

        if (IsPaused(ent.Owner))
            return;

        if (ent.Comp.OwningFaction == null)
            return;

        if (!ent.Comp.FactionIncome.TryGetValue(ent.Comp.OwningFaction.Value, out var resources))
            return;

        var flagQuery = EntityQueryEnumerator<MedievalFactionFlagComponent>();
        while (flagQuery.MoveNext(out var flagUid, out var flagComp))
        {
            if (flagComp.Faction != ent.Comp.OwningFaction.Value)
                continue;

            var spawnCoords = Transform(flagUid).Coordinates;

            foreach (var (proto, amount) in resources)
            {
                for (var i = 0; i < amount; i++)
                    Spawn(proto, spawnCoords);
            }
        }
    }

    private void FinishCapture(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype>? winner)
    {
        var comp = ent.Comp;
        comp.OwningFaction = winner;
        comp.State = CapturePointState.Cooldown;
        comp.CooldownStartTime = _timing.CurTime;
        comp.CapturingFaction = null;

        _appearance.SetData(ent,
            CapturePointVisuals.Faction,
            winner != null ? winner.Value.Id : "NoFaction");

        RemoveStatusEffectsFromAffected(comp);
        Dirty(ent);

        var resultEv = new CapturePointResultEvent(GetNetEntity(ent), winner);
        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var memberUid, out var member))
        {
            if (!IsFactionAllowed(comp, member.Faction))
                continue;

            if (_playerManager.TryGetSessionByEntity(memberUid, out var session))
                RaiseNetworkEvent(resultEv, session);
        }

        RaiseLocalEvent(resultEv); // Для waystones

        var resultText = winner != null
            ? Loc.GetString("medieval-capture-point-captured",
                ("pointName", comp.PointName),
                ("factionName", GetFactionDisplayName(winner.Value)))
            : Loc.GetString("medieval-capture-point-ended-in-draw", ("pointName", comp.PointName));

        _popup.PopupEntity(resultText, ent, PopupType.LargeCaution);

        if (winner != null)
        {
            var winnersInRadius = GetFactionEntitiesInRadius(ent, winner.Value);
            foreach (var playerUid in winnersInRadius)
            {
                _achievement.TryUpdateProgressAndGrant(playerUid, new CapturePointUpdateContext(), 
                    ach => ach.Conditions.Any(c => c is CapturePointCondition));
            }
        }
    }

    private void ApplyStatusEffectToEnemyFaction(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.CapturingFaction == null)
            return;

        var enemy = GetEnemyFaction(comp, comp.CapturingFaction.Value);
        if (enemy == null)
            return;

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Faction != enemy.Value)
                continue;

            if (_mobState.IsIncapacitated(uid))
                continue;

            if (_statusEffects.TryAddStatusEffectDuration(uid,
                    comp.CaptureStatusEffect,
                    TimeSpan.FromSeconds(comp.CurrentCaptureDuration * 1.1f)))
            {
                comp.AffectedByStatusEffect.Add(uid);
            }
        }
    }

    private void RemoveStatusEffectsFromAffected(CapturePointComponent comp)
    {
        foreach (var uid in comp.AffectedByStatusEffect.Where(uid => Exists(uid) && !Deleted(uid)))
        {
            _statusEffects.TryRemoveStatusEffect(uid, comp.CaptureStatusEffect);
        }

        comp.AffectedByStatusEffect.Clear();
    }

    private void NotifyEnemyLeader(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.CapturingFaction == null)
            return;

        var enemy = GetEnemyFaction(comp, comp.CapturingFaction.Value);
        if (enemy == null)
            return;

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Faction != enemy.Value || member.MenuAccess != FactionMenuAccess.Full)
                continue;

            if (!_playerManager.TryGetSessionByEntity(uid, out var session))
                continue;

            var ev = new CapturePointMessengerEvent(GetNetEntity(ent), comp.CapturingFaction.Value);
            RaiseNetworkEvent(ev, session);
        }
    }

    private bool IsFactionDominant(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype> faction)
    {
        var comp = ent.Comp;
        var factionIndex = comp.AllowedFactions.IndexOf(faction);
        if (factionIndex < 0)
            return false;

        var ownCount = GetFactionEntitiesInRadius(ent, faction).Count;
        for (var i = 0; i < comp.AllowedFactions.Count; i++)
        {
            if (i == factionIndex)
                continue;

            var otherCount = GetFactionEntitiesInRadius(ent, comp.AllowedFactions[i]).Count;
            if (otherCount >= ownCount)
                return false;
        }

        return true;
    }

    private bool IsAnyPointCapturing(EntityUid exclude)
    {
        var query = EntityQueryEnumerator<CapturePointComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == exclude)
                continue;

            if (comp.State == CapturePointState.Capturing)
                return true;
        }

        return false;
    }

    private HashSet<EntityUid> GetFactionEntitiesInRadius(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype> faction)
    {
        var result = new HashSet<EntityUid>();

        var xform = Transform(ent);
        var (pos, rot) = _transform.GetWorldPositionRotation(ent);

        var half = ent.Comp.Radius;
        var size = new Vector2(half * 2f, half * 2f);

        var box = Box2.CenteredAround(pos, size);
        var rotated = new Box2Rotated(box, rot, pos);

        var entities = _lookup.GetEntitiesIntersecting(xform.MapID, rotated,
            LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var uid in entities)
        {
            if (!TryComp<MedievalFactionMemberComponent>(uid, out var member))
                continue;

            #if RELEASE
            if (!HasComp<ActorComponent>(uid))
                continue;
            #endif

            if (member.Faction != faction)
                continue;

            if (_mobState.IsIncapacitated(uid))
                continue;

            result.Add(uid);
        }

        return result;
    }

    private void OnBeforeSpawn(Entity<SpawnAfterInteractComponent> ent, ref BeforeSpawnAfterInteractEvent args)
    {
        if (args.User is not { } user)
            return;

        var userPos = _transform.GetMapCoordinates(user);

        var query = EntityQueryEnumerator<CapturePointComponent>();
        while (query.MoveNext(out var pointUid, out var pointComp))
        {
            var (pos, rot) = _transform.GetWorldPositionRotation(pointUid);

            var half = pointComp.Radius * 2f; // чтоб вокруг не застроили ☺☻☺♿♿
            var size = new Vector2(half * 2f, half * 2f);

            var box = Box2.CenteredAround(pos, size);
            var rotated = new Box2Rotated(box, rot, pos);

            if (rotated.Contains(userPos.Position))
            {
                args.Cancelled = true;
                _popup.PopupEntity(Loc.GetString("construction-system-cannot-start"), ent, user);
                return;
            }
        }
    }
}
