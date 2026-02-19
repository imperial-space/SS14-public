using System.Globalization;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Server.Doors.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Light.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Seriozha.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class NedraNukeProtocolCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private const string NukeMusicPath = "/Audio/Imperial/Seriozha/SCP/event/nuke.ogg";
    private const int DefaultPhaseSeconds = 125;
    private const string CassieSender = "C.A.S.S.I.E";
    private const float ExplosionIntensity = 900f;
    private const float ExplosionSlope = 1f;
    private const float ExplosionMaxTileIntensity = 32f;

    public string Command => "nedranukeprotocol";
    public string Description => "Starts Nedra nuke protocol sequence on a map.";
    public string Help => "nedranukeprotocol [mapId]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var phaseSeconds = DefaultPhaseSeconds;

        MapId? targetMap = null;
        if (args.Length >= 1)
        {
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapInt))
            {
                shell.WriteError($"Invalid map id: {args[0]}");
                return;
            }

            var mapId = new MapId(mapInt);
            if (!_mapManager.MapExists(mapId))
            {
                shell.WriteError($"Map with id {mapInt} does not exist.");
                return;
            }

            targetMap = mapId;
        }

        if (targetMap == null)
        {
            if (shell.Player is not { AttachedEntity: { } attached })
            {
                shell.WriteError("No map specified and unable to infer executor map.");
                return;
            }

            if (!_entManager.TryGetComponent(attached, out TransformComponent? xform))
            {
                shell.WriteError("No map specified and unable to infer executor map.");
                return;
            }

            targetMap = xform.MapID;
        }

        if (!_mapManager.MapExists(targetMap.Value))
        {
            shell.WriteError($"Map with id {(int) targetMap.Value} does not exist.");
            return;
        }

        var mapUid = _mapManager.GetMapEntityId(targetMap.Value);
        if (!mapUid.IsValid())
        {
            shell.WriteError("Unable to resolve map entity.");
            return;
        }

        var protocolAirlocks = new HashSet<EntityUid>();

        SetMapAmbientColor(mapUid, Color.FromHex("#ff0000"));
        SetAllPointLightsColor(targetMap.Value, Color.FromHex("#ff0000"));
        PlayMapSound(targetMap.Value, NukeMusicPath);
        SendCassieAnnouncement(phaseSeconds);
        OpenHermeticsAndAirlocks(targetMap.Value, protocolAirlocks);

        shell.WriteLine($"Nedra protocol started on map {(int) targetMap.Value}. Detonation phase in {phaseSeconds} seconds.");

        Timer.Spawn(phaseSeconds * 1000, () =>
        {
            if (_entManager.Deleted(mapUid))
                return;

            var centcommHermetics = CloseCentcommHermeticsAndEnableGodmode(targetMap.Value);

            TriggerExplosionSpawners(targetMap.Value);

            SetMapAmbientColor(mapUid, Color.FromHex("#000000"));
            SetAllPointLightsColor(targetMap.Value, Color.White);
            UnboltAirlocks(protocolAirlocks);
            DisableGodmode(centcommHermetics);
        });
    }

    private void SendCassieAnnouncement(int detonationSeconds)
    {
        var chat = _entManager.System<ChatSystem>();
        var message = $"По решению совета O5, запущен протокол \"Мёртвая рука\". Детонация боеголовок произойдет через... {detonationSeconds} секунд. Объявлена эвакуация.";
        chat.DispatchGlobalAnnouncement(message, CassieSender, playSound: true, colorOverride: Color.Gold);
    }

    private void PlayMapSound(MapId mapId, string soundPath)
    {
        var audio = AudioParams.Default.AddVolume(-8);
        var filter = Filter.BroadcastMap(mapId);
        _entManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(filter, soundPath, audio, true);
    }

    private void SetMapAmbientColor(EntityUid mapUid, Color color)
    {
        var light = _entManager.EnsureComponent<MapLightComponent>(mapUid);
        light.AmbientLightColor = color;
        _entManager.Dirty(mapUid, light);
    }

    private void SetAllPointLightsColor(MapId mapId, Color color)
    {
        var pointLightSystem = _entManager.System<PointLightSystem>();
        var query = _entManager.EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pointLight, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            pointLightSystem.SetColor(uid, color, pointLight);
        }
    }

    private void OpenHermeticsAndAirlocks(MapId mapId, HashSet<EntityUid> protocolAirlocks)
    {
        var doorSystem = _entManager.System<DoorSystem>();
        var query = _entManager.EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var door, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (!IsHermeticOrAirlock(uid))
                continue;

            if (_entManager.TryGetComponent<DoorBoltComponent>(uid, out var preBolts))
                doorSystem.SetBoltsDown((uid, preBolts), false);

            doorSystem.TryOpen(uid, door);

            if (!_entManager.HasComponent<AirlockComponent>(uid))
                continue;

            if (_entManager.TryGetComponent<DoorBoltComponent>(uid, out var bolts))
                doorSystem.SetBoltsDown((uid, bolts), true);

            protocolAirlocks.Add(uid);
        }
    }

    private HashSet<EntityUid> CloseCentcommHermeticsAndEnableGodmode(MapId mapId)
    {
        var doorSystem = _entManager.System<DoorSystem>();
        var godmodeSystem = _entManager.System<SharedGodmodeSystem>();
        var result = new HashSet<EntityUid>();

        var query = _entManager.EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var door, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (!IsCentcommHermetic(uid))
                continue;

            if (_entManager.TryGetComponent<DoorBoltComponent>(uid, out var bolts))
                doorSystem.SetBoltsDown((uid, bolts), false);

            doorSystem.TryClose(uid, door);
            godmodeSystem.EnableGodmode(uid);
            result.Add(uid);
        }

        return result;
    }

    private void TriggerExplosionSpawners(MapId mapId)
    {
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var explosionSystem = _entManager.System<ExplosionSystem>();
        var query = _entManager.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (xform.MapID != mapId)
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (!string.Equals(protoId, "NedraNukeExplosionSpawner", StringComparison.Ordinal))
                continue;

            var coords = new MapCoordinates(xformSystem.GetWorldPosition(xform), xform.MapID);
            explosionSystem.QueueExplosion(
                coords,
                ExplosionSystem.DefaultExplosionPrototypeId,
                ExplosionIntensity,
                ExplosionSlope,
                ExplosionMaxTileIntensity,
                null,
                canCreateVacuum: true,
                maxTileBreak: 0);
        }
    }

    private void UnboltAirlocks(HashSet<EntityUid> airlocks)
    {
        var doorSystem = _entManager.System<DoorSystem>();
        foreach (var uid in airlocks)
        {
            if (!_entManager.EntityExists(uid))
                continue;

            if (!_entManager.TryGetComponent<DoorBoltComponent>(uid, out var bolts))
                continue;

            doorSystem.SetBoltsDown((uid, bolts), false);
        }
    }

    private void DisableGodmode(HashSet<EntityUid> entities)
    {
        var godmodeSystem = _entManager.System<SharedGodmodeSystem>();
        foreach (var uid in entities)
        {
            if (!_entManager.EntityExists(uid))
                continue;

            godmodeSystem.DisableGodmode(uid);
        }
    }

    private bool IsHermeticOrAirlock(EntityUid uid)
    {
        if (_entManager.HasComponent<AirlockComponent>(uid))
            return true;

        if (_entManager.HasComponent<FirelockComponent>(uid))
            return true;

        var protoId = _entManager.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        return protoId.Contains("BlastDoor", StringComparison.OrdinalIgnoreCase)
               || protoId.Contains("Shutter", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCentcommHermetic(EntityUid uid)
    {
        var protoId = _entManager.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        if (!protoId.Contains("CentralCommand", StringComparison.OrdinalIgnoreCase))
            return false;

        return protoId.Contains("BlastDoor", StringComparison.OrdinalIgnoreCase)
               || protoId.Contains("Shutter", StringComparison.OrdinalIgnoreCase);
    }
}
