using System.Numerics;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.ZLevels;

/// <summary>Отражение лампы верхнего этажа на нижнем (только у клиента).</summary>
[RegisterComponent]
public sealed partial class ZLightProxyComponent : Component;

/// <summary>
/// Свет сверху вниз: лампа у проёма (открытое пространство, пустота, решётка, стекло) светит и на этаж ниже.
/// Клиент ставит под ней на нижнем этаже такую же, но слабее лампу — её видно и тем, кто внизу, и сверху сквозь дыру;
/// стены нижнего этажа отбрасывают тени как обычно. Свет сквозь сплошное перекрытие не проходит.
/// </summary>
public sealed class ZLightProxySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PointLightSystem _lights = default!;

    /// <summary>Насколько слабее свет на этаже ниже.</summary>
    private const float EnergyScale = 0.6f;
    /// <summary>Как далеко от лампы ищется проём.</summary>
    private const int OpenRange = 2;
    /// <summary>Лампы дальше этого от игрока не отражаем.</summary>
    private const float Range = 40f;

    private readonly Dictionary<EntityUid, EntityUid> _proxies = new();
    private TimeSpan _next;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.RealTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(0.25);

        var alive = new HashSet<EntityUid>();
        if (_player.LocalEntity is { } player)
        {
            var center = _xform.GetMapCoordinates(player);
            var stackMaps = StackMaps(center.MapId);
            var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var light, out var xform))
            {
                if (HasComp<ZLightProxyComponent>(uid) || !light.Enabled || !stackMaps.Contains(xform.MapID))
                    continue;
                var pos = _xform.GetMapCoordinates(uid, xform);
                if ((pos.Position - center.Position).Length() > Range + light.Radius)
                    continue;
                if (_z.GetStep(pos, false) is not { } below || !HasOpenNear(pos))
                    continue;
                alive.Add(uid);
                UpdateProxy(uid, light, below);
            }
        }

        foreach (var (source, proxy) in new Dictionary<EntityUid, EntityUid>(_proxies))
        {
            if (alive.Contains(source))
                continue;
            if (!TerminatingOrDeleted(proxy))
                QueueDel(proxy);
            _proxies.Remove(source);
        }
    }

    private HashSet<MapId> StackMaps(MapId mapId)
    {
        var result = new HashSet<MapId>();
        if (!_map.TryGetMap(mapId, out var map) || !TryComp<ZLevelMapComponent>(map, out var z) || string.IsNullOrEmpty(z.Stack))
            return result;
        var query = EntityQueryEnumerator<ZLevelMapComponent, MapComponent>();
        while (query.MoveNext(out _, out var other, out var mapComp))
        {
            if (other.Stack == z.Stack && other.Below != null)
                result.Add(mapComp.MapId);
        }
        return result;
    }

    private void UpdateProxy(EntityUid source, PointLightComponent light, MapCoordinates below)
    {
        if (!_proxies.TryGetValue(source, out var proxy) || TerminatingOrDeleted(proxy) || Transform(proxy).MapID != below.MapId)
        {
            if (_proxies.TryGetValue(source, out var old) && !TerminatingOrDeleted(old))
                QueueDel(old);
            proxy = Spawn(null, below);
            AddComp<ZLightProxyComponent>(proxy);
            _xform.AttachToGridOrMap(proxy);
            _proxies[source] = proxy;
        }
        else
        {
            _xform.SetMapCoordinates(proxy, below);
        }

        var proxyLight = (PointLightComponent) _lights.EnsureLight(proxy);
        _lights.SetColor(proxy, light.Color, proxyLight);
        _lights.SetRadius(proxy, light.Radius, proxyLight);
        _lights.SetEnergy(proxy, light.Energy * EnergyScale, proxyLight);
        _lights.SetSoftness(proxy, light.Softness, proxyLight);
        _lights.SetEnabled(proxy, true, proxyLight);
    }

    private bool HasOpenNear(MapCoordinates coords)
    {
        for (var dx = -OpenRange; dx <= OpenRange; dx++)
        for (var dy = -OpenRange; dy <= OpenRange; dy++)
        {
            if (_z.IsTransparent(new MapCoordinates(coords.Position + new Vector2(dx, dy), coords.MapId)))
                return true;
        }
        return false;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var proxy in _proxies.Values)
        {
            if (!TerminatingOrDeleted(proxy))
                QueueDel(proxy);
        }
        _proxies.Clear();
    }
}
