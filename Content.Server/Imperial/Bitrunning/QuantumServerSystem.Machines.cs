using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Консоль, голо-лестница, байтфорж (quantum_console.dm, hololadder.dm, byteforge.dm).</summary>
public sealed partial class QuantumServerSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan ShutdownTime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ForgeDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ConsoleRefresh = TimeSpan.FromSeconds(2);
    private TimeSpan _nextConsoleRefresh;

    private void InitializeConsole()
    {
        SubscribeLocalEvent<QuantumConsoleComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateConsoleUi((uid, comp)));
        SubscribeLocalEvent<QuantumConsoleComponent, QuantumConsoleSetDomainMessage>(OnSetDomain);
        SubscribeLocalEvent<QuantumConsoleComponent, QuantumConsoleRandomDomainMessage>(OnRandomDomain);
        SubscribeLocalEvent<QuantumConsoleComponent, QuantumConsoleStopDomainMessage>(OnStopDomain);
        SubscribeLocalEvent<QuantumConsoleComponent, QuantumConsoleRefreshMessage>((uid, comp, _) => UpdateConsoleUi((uid, comp)));
        SubscribeLocalEvent<QuantumServerComponent, QuantumServerShutdownDoAfterEvent>(OnShutdownDoAfter);
    }

    #region Консоль

    /// <summary>find_server(): сервер вплотную к консоли.</summary>
    private Entity<QuantumServerComponent>? ConsoleServer(Entity<QuantumConsoleComponent> console)
    {
        if (console.Comp.Server is { } cached && TryComp<QuantumServerComponent>(cached, out var cachedComp))
            return (cached, cachedComp);

        foreach (var near in _lookup.GetEntitiesInRange<QuantumServerComponent>(Transform(console).Coordinates, 1.5f))
        {
            console.Comp.Server = near;
            return near;
        }

        return null;
    }

    private void OnSetDomain(Entity<QuantumConsoleComponent> ent, ref QuantumConsoleSetDomainMessage args)
    {
        if (ConsoleServer(ent) is { } server)
            ColdBoot(server, args.Id, random: false);
        UpdateConsoleUi(ent);
    }

    private void OnRandomDomain(Entity<QuantumConsoleComponent> ent, ref QuantumConsoleRandomDomainMessage args)
    {
        if (ConsoleServer(ent) is { } server)
            ColdBoot(server, GetRandomDomainId(server), random: true);
        UpdateConsoleUi(ent);
    }

    private void OnStopDomain(Entity<QuantumConsoleComponent> ent, ref QuantumConsoleStopDomainMessage args)
    {
        if (ConsoleServer(ent) is { } server)
            BeginShutdown(server, args.Actor);
        UpdateConsoleUi(ent);
    }

    /// <summary>get_random_domain_id(): платные домены по карману, дешевле EXTREME.</summary>
    private string? GetRandomDomainId(Entity<QuantumServerComponent> ent)
    {
        if (ent.Comp.Points < 1)
            return null;

        var pool = _proto.EnumeratePrototypes<BitrunningDomainPrototype>()
            .Where(d => (d.Flags & BitrunningDomainFlags.TestOnly) == 0
                        && d.Cost <= ent.Comp.Points
                        && d.Cost > BitrunningConstants.CostNone
                        && d.Cost < BitrunningConstants.CostExtreme)
            .ToList();
        return pool.Count == 0 ? null : _random.Pick(pool).ID;
    }

    public void UpdateConsoleUi(Entity<QuantumConsoleComponent> ent)
    {
        if (!_ui.HasUi(ent, QuantumConsoleUiKey.Key))
            return;

        if (ConsoleServer(ent) is not { } server)
        {
            _ui.SetUiState(ent.Owner, QuantumConsoleUiKey.Key,
                new QuantumConsoleState(false, null, 0, 0, false, false, 1, 0, new(), new()));
            return;
        }

        var comp = server.Comp;
        var domains = new List<QuantumConsoleDomainEntry>();
        foreach (var domain in _proto.EnumeratePrototypes<BitrunningDomainPrototype>().OrderBy(d => d.Cost).ThenBy(d => d.Difficulty))
        {
            if ((domain.Flags & BitrunningDomainFlags.TestOnly) != 0)
                continue;

            // can_view_name / can_view_reward.
            var canView = (int) domain.Difficulty < comp.ScannerTier && domain.Cost <= comp.Points + 5;
            var canViewReward = (int) domain.Difficulty < comp.ScannerTier + 1 && domain.Cost <= comp.Points + 3;
            domains.Add(new QuantumConsoleDomainEntry(
                domain.ID,
                canView ? Loc.GetString(domain.Name) : Loc.GetString("bitrunning-console-redacted"),
                canView ? Loc.GetString(domain.Description) : Loc.GetString("bitrunning-console-scan-limited"),
                domain.Cost,
                domain.Difficulty,
                canViewReward ? domain.Reward.ToString() : Loc.GetString("bitrunning-console-redacted"),
                domain.Modular,
                domain.SecondaryLoot.Count > 0));
        }

        var avatars = new List<QuantumConsoleAvatarEntry>();
        foreach (var avatar in comp.Avatars)
        {
            if (_avatars.GetVitals(avatar) is { } vitals)
                avatars.Add(vitals);
        }

        var state = new QuantumConsoleState(
            true,
            comp.Domain?.Id,
            comp.Avatars.Count,
            comp.Points,
            comp.Randomized,
            comp.Ready && IsOperational(server),
            comp.ScannerTier,
            comp.ExitSpots.Count - comp.RetriesSpent,
            domains,
            avatars);
        _ui.SetUiState(ent.Owner, QuantumConsoleUiKey.Key, state);
    }

    private void RefreshOpenConsoles(TimeSpan now)
    {
        if (now < _nextConsoleRefresh)
            return;

        _nextConsoleRefresh = now + ConsoleRefresh;
        var query = EntityQueryEnumerator<QuantumConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, QuantumConsoleUiKey.Key))
                UpdateConsoleUi((uid, console));
        }
    }

    private void StartShutdownDoAfter(Entity<QuantumServerComponent> ent, EntityUid user)
    {
        var args = new DoAfterArgs(EntityManager, user, ShutdownTime, new QuantumServerShutdownDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 3f,
        };
        _doAfter.TryStartDoAfter(args);
    }

    private void OnShutdownDoAfter(Entity<QuantumServerComponent> ent, ref QuantumServerShutdownDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Reset(ent);
    }

    #endregion

    #region Голо-лестница

    private void InitializeHololadder()
    {
        SubscribeLocalEvent<BitrunningHololadderComponent, InteractHandEvent>(OnLadderInteract);
        SubscribeLocalEvent<BitrunningHololadderComponent, StepTriggerAttemptEvent>(OnLadderStepAttempt);
        SubscribeLocalEvent<BitrunningHololadderComponent, StepTriggeredOnEvent>(OnLadderStep);
        SubscribeLocalEvent<BitrunningAvatarComponent, HololadderDoAfterEvent>(OnLadderDoAfter);
    }

    private void OnLadderInteract(Entity<BitrunningHololadderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryLadderDisconnect(ent, args.User);
    }

    private void OnLadderStepAttempt(Entity<BitrunningHololadderComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = HasComp<BitrunningAvatarComponent>(args.Tripper);
    }

    private void OnLadderStep(Entity<BitrunningHololadderComponent> ent, ref StepTriggeredOnEvent args)
    {
        TryLadderDisconnect(ent, args.Tripper);
    }

    /// <summary>hololadder/disconnect(): глюки домена не дают уйти, пока он не пройден.</summary>
    private void TryLadderDisconnect(Entity<BitrunningHololadderComponent> ent, EntityUid user)
    {
        if (!HasComp<BitrunningAvatarComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-ladder-no-connection"), ent, user);
            return;
        }

        if (ent.Comp.Server is { } serverUid && TryComp<QuantumServerComponent>(serverUid, out var server) && !server.Complete)
        {
            foreach (var threat in server.SpawnedThreats)
            {
                if (!TerminatingOrDeleted(threat) && _mobs.IsAlive(threat) && HasComp<ActorComponent>(threat))
                {
                    _popup.PopupEntity(Loc.GetString("bitrunning-ladder-blocked"), user, user, Content.Shared.Popups.PopupType.MediumCaution);
                    return;
                }
            }
        }

        _popup.PopupEntity(Loc.GetString("bitrunning-ladder-disconnecting"), user, user);
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.TravelTime, new HololadderDoAfterEvent(), user, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(args);
    }

    private void OnLadderDoAfter(Entity<BitrunningAvatarComponent> ent, ref HololadderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _avatars.Disconnect(ent, causeDamage: false);
    }

    #endregion

    #region Байтфорж

    /// <summary>start_to_spawn(): вспышка, через секунду награда появляется на байтфорже.</summary>
    private void StartToSpawn(EntityUid forge, EntityUid cache)
    {
        if (!TryComp<ByteforgeComponent>(forge, out var comp))
            return;

        comp.Pending.Add((cache, _timing.CurTime + ForgeDelay));
        _appearance.SetData(forge, BitrunningVisuals.Flicker, true);
        _light.SetEnabled(forge, true);
    }

    private void UpdateByteforges(TimeSpan now)
    {
        RefreshOpenConsoles(now);

        var query = EntityQueryEnumerator<ByteforgeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var forge, out var xform))
        {
            if (forge.Pending.Count == 0)
                continue;

            for (var i = forge.Pending.Count - 1; i >= 0; i--)
            {
                var (cache, at) = forge.Pending[i];
                if (now < at)
                    continue;

                forge.Pending.RemoveAt(i);
                if (TerminatingOrDeleted(cache))
                    continue;

                // spawn_cache(): flash() + forceMove.
                _transform.SetCoordinates(cache, xform.Coordinates);
                SparkAt(xform.Coordinates);
            }

            if (forge.Pending.Count == 0)
            {
                _appearance.SetData(uid, BitrunningVisuals.Flicker, false);
                _light.SetEnabled(uid, false);
            }
        }
    }

    #endregion

}
