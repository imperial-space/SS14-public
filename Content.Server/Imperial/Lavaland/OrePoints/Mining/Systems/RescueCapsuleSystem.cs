using System;
using System.Linq;
using System.Numerics;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

public sealed class RescueCapsuleSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly Vector2 SpawnAreaSize = new(7f, 7f);
    private const float SpawnAreaRadius = 4f;
    private const float CenterOffset = 3.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RescueCapsuleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RescueCapsuleComponent, RescueCapsuleDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(EntityUid uid, RescueCapsuleComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            TimeSpan.FromSeconds(comp.DeployDelay),
            new RescueCapsuleDoAfterEvent(),
            uid,
            used: uid)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-failed"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("rescue-capsule-calling"), uid, args.User, PopupType.Medium);
    }

    private void OnDoAfter(EntityUid uid, RescueCapsuleComponent comp, RescueCapsuleDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-failed"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var userUid = args.User;
        var xform = Transform(userUid);
        if (xform.MapID == MapId.Nullspace ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-blocked"), uid, userUid, PopupType.MediumCaution);
            return;
        }

        if (!CanDeployAtUserPosition((userUid, xform, gridComp)))
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-blocked"), uid, userUid, PopupType.MediumCaution);
            return;
        }

        var path = ResolveGridPath(comp.GridPath);
        var options = new DeserializationOptions { InitializeMaps = true };

        // Центрируем 7x7 грид на игроке.
        var spawnOffset = _transform.GetWorldPosition(userUid) - new Vector2(CenterOffset, CenterOffset);
        if (!_mapLoader.TryLoadGrid(xform.MapID, path, out var loadedGrid, options, spawnOffset))
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-failed"), uid, userUid, PopupType.MediumCaution);
            return;
        }

        if (loadedGrid is null)
        {
            _popup.PopupEntity(Loc.GetString("rescue-capsule-failed"), uid, userUid, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rescue-capsule-deployed"), uid, userUid, PopupType.Large);
        QueueDel(uid);
    }

    private bool CanDeployAtUserPosition(Entity<TransformComponent, MapGridComponent> user)
    {
        var (uid, xform, gridComp) = user;
        var worldPos = _transform.GetWorldPosition(uid);
        var box = Box2.CenteredAround(worldPos.Rounded(), SpawnAreaSize);

        // Не даем разворачивать рядом с другими отдельными гридами.
        if (_lookup.GetEntitiesInRange<MapGridComponent>(xform.Coordinates, SpawnAreaRadius)
            .Any(otherGrid => otherGrid != xform.GridUid))
        {
            return false;
        }

        // Площадка должна быть свободна только от реальных блокеров с коллизией.
        foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid!.Value, gridComp, box))
        {
            if (TryComp<PhysicsComponent>(ent, out var body) && body.CanCollide)
                return false;
        }

        return true;
    }

    private static ResPath ResolveGridPath(string configured)
    {
        var value = configured.Trim();
        if (!value.StartsWith('/'))
            value = '/' + value;

        if (!value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            value += ".yml";

        return new ResPath(value);
    }
}

