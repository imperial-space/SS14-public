using System.Linq;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Station;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Reflection;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>
/// Угрозы домена (server/threats.dm, antagonists/*, components/virtual_entity.dm, components/glitch.dm):
/// глюки из существ домена, граница сейфхауса, взлом сервера и прорыв глюка на станцию через байтфорж.
/// </summary>
public sealed partial class QuantumServerSystem
{
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IReflectionManager _reflection = default!;
    [Dependency] private readonly SharedStationSpawningSystem _gear = default!;

    /// <summary>Глюки по угрозе: спавнер гост-роли и порог threat (cyber_police 0, cyber_tac 50, netguardian 90).</summary>
    private static readonly (EntProtoId Spawner, int Threat)[] GlitchRoles =
    {
        ("BitrunningGlitchSpawnerCyberPolice", 0),
        ("BitrunningGlitchSpawnerCyberTac", 50),
        ("BitrunningGlitchSpawnerNetguardian", 90),
    };

    private static readonly ProtoId<Content.Shared.Dataset.DatasetPrototype> CyberauthNames = "BitrunningCyberauthNames";

    private static readonly TimeSpan BorderCheck = TimeSpan.FromSeconds(0.1);
    private TimeSpan _nextBorderCheck;

    private void InitializeThreats()
    {
        SubscribeLocalEvent<QuantumServerComponent, GotEmaggedEvent>(OnServerEmagged);
        SubscribeLocalEvent<BitrunningGlitchComponent, GhostRoleSpawnerUsedEvent>(OnGlitchSpawned);
        SubscribeLocalEvent<BitrunningGlitchComponent, BitrunningBreachDoAfterEvent>(OnBreachDoAfter);
        SubscribeLocalEvent<BitrunningGlitchComponent, MobStateChangedEvent>(OnGlitchMobState);
        SubscribeLocalEvent<NetguardianRocketActionEvent>(OnNetguardianRockets);
    }

    #region Глюки

    /// <summary>setup_glitch(): существо домена начинает «мутировать» — призраки могут занять роль глюка.</summary>
    public bool SetupGlitch(Entity<QuantumServerComponent> ent, EntProtoId? forcedSpawner = null)
    {
        if (!ValidateMutationCandidates(ent))
            return false;

        var target = _random.Pick(ent.Comp.MutationCandidates);
        var spawner = forcedSpawner ?? GetGlitchRole(ent);
        EnsureComp<BitrunningDigitalAuraComponent>(target);

        var marker = Spawn(spawner, Transform(target).Coordinates);
        var comp = EnsureComp<BitrunningGlitchSpawnerComponent>(marker);
        comp.Server = ent;
        comp.Target = target;
        _transform.SetParent(marker, target);
        Log.Info($"Квантовый сервер {ToPrettyString(ent)}: глюк {spawner} из {ToPrettyString(target)}");
        return true;
    }

    /// <summary>get_antagonist_role(): случайная роль из доступных по угрозе, половина порога вычитается.</summary>
    private EntProtoId GetGlitchRole(Entity<QuantumServerComponent> ent)
    {
        var available = GlitchRoles.Where(r => ent.Comp.Threat >= r.Threat).ToList();
        var chosen = _random.Pick(available);
        ent.Comp.Threat -= chosen.Threat / 2;
        return chosen.Spawner;
    }

    /// <summary>validate_mutation_candidates(): живые существа домена без разума.</summary>
    private bool ValidateMutationCandidates(Entity<QuantumServerComponent> ent)
    {
        ent.Comp.MutationCandidates.RemoveAll(c => TerminatingOrDeleted(c) || !_mobs.IsAlive(c)
            || TryComp<MindContainerComponent>(c, out var mind) && mind.HasMind);
        return ent.Comp.MutationCandidates.Count > 0;
    }

    /// <summary>spawn_glitch(): существо распадается, на его месте — игрок-глюк.</summary>
    private void OnGlitchSpawned(Entity<BitrunningGlitchComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<BitrunningGlitchSpawnerComponent>(args.Spawner, out var spawner)
            || !TryComp<QuantumServerComponent>(spawner.Server, out var server))
            return;

        if (!TerminatingOrDeleted(spawner.Target))
        {
            _transform.SetCoordinates(ent, Transform(spawner.Target).Coordinates);
            Spawn("EffectSparks", Transform(spawner.Target).Coordinates);
            QueueDel(spawner.Target);
        }

        _transform.AttachToGridOrMap(ent);
        ent.Comp.Server = spawner.Server;
        EnsureComp<BitrunningDigitalAuraComponent>(ent);
        EnsureComp<BitrunningVirtualEntityComponent>(ent).Server = spawner.Server;
        if (ent.Comp.MartialArt is { } art)
            TeachMartialArt(ent, art);

        // convert_agent(): имя из cyberauth_names.
        if (HasComp<HumanoidProfileComponent>(ent) && _proto.TryIndex(CyberauthNames, out var names))
            _meta.SetEntityName(ent, _random.Pick(names.Values));

        AddThreat((spawner.Server, server), ent);
    }

    /// <summary>
    /// MartialArtsSystem.Teach через отражение: битраннинг не зависит от сборки с Imperial/MartialArts.
    /// </summary>
    private void TeachMartialArt(EntityUid uid, string art)
    {
        if (_reflection.GetType("Content.Server.Imperial.MartialArts.MartialArtsSystem") is not { } systemType
            || _reflection.GetType("Content.Shared.Imperial.MartialArts.MartialArtPrototype") is not { } protoType
            || !EntityManager.EntitySysManager.TryGetEntitySystem(systemType, out var system)
            || systemType.GetMethod("Teach") is not { } teach)
            return;

        var protoId = Activator.CreateInstance(typeof(ProtoId<>).MakeGenericType(protoType), art);
        teach.Invoke(system, new object?[] { uid, protoId, null });
    }

    /// <summary>add_threats(): угроза увеличивает награду, аватары получают тревогу.</summary>
    private void AddThreat(Entity<QuantumServerComponent> ent, EntityUid threat)
    {
        ent.Comp.SpawnedThreats.Add(threat);
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Magic/ethereal_exit.ogg"), threat);
        foreach (var avatar in ent.Comp.Avatars)
        {
            _avatars.Alert(avatar, "BitrunningThreat");
        }
    }

    private void OnGlitchMobState(Entity<BitrunningGlitchComponent> ent, ref MobStateChangedEvent args)
    {
        // component/glitch: вырвавшийся на станцию глюк не оставляет тела.
        if (args.NewMobState != MobState.Dead || !ent.Comp.OnStation)
            return;

        Spawn("EffectSparks", Transform(ent).Coordinates);
        QueueDel(ent);
    }

    /// <summary>notify_spawned_threats().</summary>
    private void NotifySpawnedThreats(Entity<QuantumServerComponent> ent)
    {
        foreach (var threat in ent.Comp.SpawnedThreats)
        {
            if (!TerminatingOrDeleted(threat) && HasComp<ActorComponent>(threat))
                _popup.PopupEntity(Loc.GetString("bitrunning-threat-deletion"), threat, threat, Content.Shared.Popups.PopupType.LargeCaution);
        }
    }

    #endregion

    #region Граница сейфхауса

    /// <summary>virtual_entity/on_parent_pre_move(): существа домена не заходят в сейфхаус (пока сервер не взломан).</summary>
    private void UpdateBorders(TimeSpan now)
    {
        if (now < _nextBorderCheck)
            return;

        _nextBorderCheck = now + BorderCheck;
        var query = EntityQueryEnumerator<BitrunningGlitchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var glitch, out var xform))
        {
            if (glitch.OnStation || glitch.Server is not { } serverUid
                || !TryComp<QuantumServerComponent>(serverUid, out var server) || server.Emagged
                || xform.GridUid != server.DomainGrid || server.DomainGrid is not { } grid)
                continue;

            var tile = _loader.TileOf(grid, xform.Coordinates);
            if (!server.SafeTiles.Contains(tile))
            {
                glitch.LastOutside = xform.Coordinates;
                continue;
            }

            if (glitch.LastOutside is { } back)
            {
                _transform.SetCoordinates(uid, back);
                _popup.PopupEntity(Loc.GetString("bitrunning-threat-out-of-bounds"), uid, uid);
            }
        }
    }

    #endregion

    #region Взлом и прорыв

    /// <summary>emag_act(): удвоенный шанс глюков, глюки свободны и могут прорваться на станцию.</summary>
    private void OnServerEmagged(Entity<QuantumServerComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        ent.Comp.GlitchChance *= 2;
        ent.Comp.ThreatProbMax *= 2;
        _popup.PopupEntity(Loc.GetString("bitrunning-server-emagged"), ent, args.UserUid);
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/sparks1.ogg"), ent);
        args.Handled = true;
    }

    /// <summary>on_goal_turf_entered() у взломанного сервера: глюк на клетке расшифровки прорывается на станцию.</summary>
    private void CheckBreaches(Entity<QuantumServerComponent> ent)
    {
        if (!ent.Comp.Emagged || ent.Comp.DomainGrid is not { } grid)
            return;

        foreach (var threat in ent.Comp.SpawnedThreats.ToList())
        {
            if (TerminatingOrDeleted(threat) || !TryComp<BitrunningGlitchComponent>(threat, out var glitch) || glitch.Breaching
                || !_mobs.IsAlive(threat) || !HasComp<ActorComponent>(threat))
                continue;

            var xform = Transform(threat);
            if (xform.GridUid != grid || !ent.Comp.GoalTiles.Contains((grid, _loader.TileOf(grid, xform.Coordinates))))
                continue;

            StartBreach(ent, (threat, glitch));
        }
    }

    /// <summary>station_spawn(): чем больше живых битраннеров, тем дольше.</summary>
    private void StartBreach(Entity<QuantumServerComponent> ent, Entity<BitrunningGlitchComponent> glitch)
    {
        if (GetRandomNearbyForge(ent) is not { } forge)
            return;

        glitch.Comp.Breaching = true;
        _appearance.SetData(forge, BitrunningVisuals.Angry, true);
        _radio.SendRadioMessage(ent, Loc.GetString("bitrunning-breach-warning"), SupplyChannel, ent);

        var timeout = TimeSpan.FromSeconds(HasComp<HumanoidProfileComponent>(glitch) ? 2 : 10);
        var alive = 0;
        foreach (var avatar in ent.Comp.Avatars)
        {
            if (!_mobs.IsAlive(avatar) || !HasComp<ActorComponent>(avatar))
                continue;

            alive++;
            timeout *= 5;
            _avatars.Alert(avatar, "BitrunningBreach");
        }

        if (alive > 0)
            _popup.PopupEntity(Loc.GetString("bitrunning-breach-criminals", ("count", alive)), glitch, glitch, Content.Shared.Popups.PopupType.MediumCaution);

        var args = new DoAfterArgs(EntityManager, glitch, timeout, new BitrunningBreachDoAfterEvent(), glitch, target: forge)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            DistanceThreshold = null,
        };

        if (!_doAfter.TryStartDoAfter(args))
            glitch.Comp.Breaching = false;
    }

    private void OnBreachDoAfter(Entity<BitrunningGlitchComponent> ent, ref BitrunningBreachDoAfterEvent args)
    {
        ent.Comp.Breaching = false;
        if (args.Handled || args.Target is not { } forge)
            return;

        args.Handled = true;
        _appearance.SetData(forge, BitrunningVisuals.Angry, false);
        if (args.Cancelled || ent.Comp.Server is not { } serverUid || !TryComp<QuantumServerComponent>(serverUid, out var server))
            return;

        // reset_equipment(): с собой ничего из домена.
        if (ent.Comp.Gear is { } gear)
        {
            var slots = _inventory.GetSlotEnumerator(ent.Owner);
            while (slots.NextItem(out var item))
            {
                Del(item);
            }

            _gear.EquipStartingGear(ent, gear);
        }

        server.SpawnedThreats.Remove(ent);
        ent.Comp.OnStation = true;
        RemComp<BitrunningVirtualEntityComponent>(ent);

        var coords = Transform(forge).Coordinates;
        _transform.SetCoordinates(ent, coords);
        _transform.AttachToGridOrMap(ent);
        SparkAt(coords);
        _radio.SendRadioMessage(serverUid, Loc.GetString("bitrunning-breach-done"), SupplyChannel, serverUid);
        Log.Info($"Глюк {ToPrettyString(ent)} прорвался на станцию через {ToPrettyString(forge)}");
    }

    #endregion

    #region Нетгардиан

    /// <summary>rapid_fire/netguardian: три ракеты с разбросом 15°, откат 30 с.</summary>
    private void OnNetguardianRockets(NetguardianRocketActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        _popup.PopupEntity(Loc.GetString("bitrunning-netguardian-target"), user, Content.Shared.Popups.PopupType.MediumCaution);
        var from = _transform.GetMapCoordinates(user);
        var to = _transform.ToMapCoordinates(args.Target);
        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        for (var i = -1; i <= 1; i++)
        {
            var angle = new Angle(direction).Degrees + i * 15;
            var rocket = Spawn("BulletRocket", from);
            _gun.ShootProjectile(rocket, Angle.FromDegrees(angle).ToVec(), System.Numerics.Vector2.Zero, user, user, 20f);
        }
    }

    #endregion
}

/// <summary>/datum/round_event/ghost_role/bitrunning_glitch: админ-ивент «Глюк битраннинга».</summary>
public sealed class BitrunningGlitchRuleSystem : GameRuleSystem<BitrunningGlitchRuleComponent>
{
    [Dependency] private readonly QuantumServerSystem _servers = default!;

    protected override void Started(EntityUid uid, BitrunningGlitchRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var servers = new List<Entity<QuantumServerComponent>>();
        var query = EntityQueryEnumerator<QuantumServerComponent>();
        while (query.MoveNext(out var server, out var comp))
        {
            if (comp.Domain != null && comp.Ready)
                servers.Add((server, comp));
        }

        RobustRandom.Shuffle(servers);
        foreach (var server in servers)
        {
            if (_servers.SetupGlitch(server, component.ForcedSpawner))
                break;
        }

        GameTicker.EndGameRule(uid, gameRule);
    }
}
