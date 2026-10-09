using Content.Server.Actions;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Носитель блоба (datum/action/innate/blobpop): разрывается и становится овермандом.
/// </summary>
public sealed class BlobCarrierSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly BlobStructureSystem _structure = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;
    [Dependency] private readonly Content.Server.Station.Systems.StationSystem _station = default!;
    [Dependency] private readonly Content.Server.Chat.Managers.IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobCarrierComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlobCarrierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobCarrierComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobCarrierComponent, BlobPopActionEvent>(OnPop);
    }

    private void OnMapInit(Entity<BlobCarrierComponent> ent, ref MapInitEvent args) => Setup(ent);

    private void OnStartup(Entity<BlobCarrierComponent> ent, ref ComponentStartup args)
    {
        if (MetaData(ent).EntityLifeStage >= EntityLifeStage.MapInitialized)
            Setup(ent);
    }

    private void Setup(Entity<BlobCarrierComponent> ent)
    {
        if (ent.Comp.PopActionEntity != null)
            return;

        _actions.AddAction(ent, ref ent.Comp.PopActionEntity, ent.Comp.PopAction);
        ent.Comp.AutoPopTime = _timing.CurTime + ent.Comp.AutoPopDelay;
        _overmind.SendMessage(ent, Loc.GetString("blob-carrier-autopop", ("time", _overmind.FormatTime(ent.Comp.AutoPopDelay))));
        _overmind.SendMessage(ent, Loc.GetString("blob-carrier-greet"));
    }

    private void OnShutdown(Entity<BlobCarrierComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.PopActionEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BlobCarrierComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PopActionEntity != null && now >= comp.AutoPopTime)
                Pop((uid, comp), true);
        }
    }

    private void OnPop(Entity<BlobCarrierComponent> ent, ref BlobPopActionEvent args)
    {
        args.Handled = true;
        Pop(ent, false);
    }

    /// <summary>blobpop/Activate().</summary>
    public void Pop(Entity<BlobCarrierComponent> ent, bool timerActivated)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
        {
            RemCompDeferred<BlobCarrierComponent>(ent);
            return;
        }

        var valid = true;
        if (!_structure.TryGetTile(ent, out var grid, out var tile) || !_structure.IsStationGrid(grid) ||
            !_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
        {
            _overmind.SendMessage(ent, Loc.GetString("blob-core-invalid-spot"));
            valid = false;
        }
        else if (IsDense(grid, tile, ent))
        {
            _overmind.SendMessage(ent, Loc.GetString("blob-core-too-dense"));
            valid = false;
        }

        var placement = BlobOvermindSystem.BlobPlacement.Force;
        if (!valid)
        {
            if (!timerActivated)
                return;
            placement = BlobOvermindSystem.BlobPlacement.Random;
            _overmind.SendMessage(ent, Loc.GetString("blob-carrier-random-location"));
        }

        var coords = Transform(ent).Coordinates;
        var overmind = Spawn(ent.Comp.OvermindPrototype, coords);
        var om = Comp<BlobOvermindComponent>(overmind);
        om.Points = ent.Comp.StartingPoints;
        om.Rule = ent.Comp.Rule;

        var sound = ent.Comp.AlertSound;
        RemComp<BlobCarrierComponent>(ent);
        _overmind.PlaceCore(overmind, om, placement, popOverride: true);
        _mind.TransferTo(mindId, overmind, ghostCheckOverride: true, mind: mind);
        _gibbing.Gib(ent);

        _audio.PlayPvs(sound, overmind, AudioParams.Default.WithVolume(BlobStructureSystem.Db(50)));

        // notify_ghosts("A Blob host has burst in ...").
        var ghosts = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        var area = _station.GetOwningStation(overmind) is { } station ? Name(station) : Loc.GetString("blob-node-unknown-area");
        var text = Loc.GetString("blob-ghost-notify-burst", ("area", area));
        _chatManager.ChatMessageToManyFiltered(ghosts, ChatChannel.Server, text, text, overmind, false, true, Color.FromHex("#4aa34a"));
        _audio.PlayGlobal(sound, ghosts, true, AudioParams.Default.WithVolume(BlobStructureSystem.Db(75)));
    }

    private bool IsDense(Entity<MapGridComponent> grid, Vector2i tile, EntityUid self)
    {
        foreach (var uid in _structure.EntitiesOnTile(grid, tile))
        {
            if (uid == self || !TryComp<PhysicsComponent>(uid, out var physics) || !physics.Hard || !physics.CanCollide ||
                !Transform(uid).Anchored)
                continue;
            if ((physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                return true;
        }

        return false;
    }
}
