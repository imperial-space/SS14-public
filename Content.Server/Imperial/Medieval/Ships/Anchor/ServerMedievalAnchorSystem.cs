using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Anchor;

public sealed class ServerMedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalAnchorComponent, UseAnchorEvent>(OnUseAnchor);
        SubscribeLocalEvent<MedievalAnchorComponent, ExaminedEvent>(OnExamine);
    }

    private void OnStartup(EntityUid uid, MedievalAnchorComponent component, ComponentStartup args)
    {
        UpdateAnchorVisuals(uid, component);
    }

    private void OnUseAnchor(EntityUid uid, MedievalAnchorComponent component, UseAnchorEvent args)
    {
        if (args.Target == null || args.Cancelled || args.Handled)
        {
            component.User = null;
            Dirty(uid, component);
            return;
        }

        if (!_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            return;

        var anchorDown = component.Enabled;
        var anchorTransform = Transform(uid);
        var grid = anchorTransform.GridUid;

        ShuttleComponent? shuttleComponent = null;
        if (!grid.HasValue || !anchorTransform.Anchored || !Resolve(grid.Value, ref shuttleComponent) ||
            !TryComp<ShipDrowningComponent>(grid.Value, out var shipDrowningComponent))
            return;

        if (!anchorDown)
        {
            shuttleComponent.Enabled = false;

            if (TryComp<PhysicsComponent>(grid.Value, out var body))
            {
                // Keep the ship dynamic so sea waves and other ambient physics continue updating while anchored.
                _physics.SetBodyType(grid.Value, BodyType.Dynamic, body: body);
                _physics.SetBodyStatus(grid.Value, body, BodyStatus.InAir);
                _physics.SetFixedRotation(grid.Value, true, body: body);
                _physics.SetLinearVelocity(grid.Value, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(grid.Value, 0f, body: body);
            }

            if (SearchIslandInRange(uid, component.IslandSearchRange))
                component.AnchorUsedTime = _timing.CurTime;
            else
                component.AnchorUsedTime = null;
        }
        else
        {
            shuttleComponent.Enabled = true;
            _shuttleSystem.Enable(grid.Value);

            component.AnchorUsedTime = null;
        }

        shipDrowningComponent.DisableWavesTime = component.AnchorUsedTime + TimeSpan.FromSeconds(component.WavesTimer);

        component.Enabled = !anchorDown;
        UpdateAnchorVisuals(uid, component);
        _audio.PlayPvs(MedievalShipSounds.AnchorUse, uid);
        args.Handled = true;
        component.User = null;
        Dirty(uid, component);
    }

    private void UpdateAnchorVisuals(EntityUid uid, MedievalAnchorComponent component)
    {
        _appearance.SetData(uid, MedievalAnchorVisuals.Enabled, component.Enabled);
    }

    private bool SearchIslandInRange(EntityUid uid, float range)
    {
        var searchBox = Box2.CenteredAround(_transform.GetWorldPosition(uid), new Vector2(range, range));

        var mapManager = IoCManager.Resolve<IMapManager>();

        var worldPos = _transform.GetWorldPosition(uid);
        var gridRange = new Vector2(range, range);

        List<Entity<MapGridComponent>> grids = [];
        mapManager.FindGridsIntersecting(Transform(uid).MapID, new Box2(worldPos - gridRange, worldPos + gridRange), ref grids);

        foreach (var grid in grids)
        {
            if (HasComp<IslandComponent>(grid))
                return true;
        }

        return false;
    }

    private void OnExamine(EntityUid uid, MedievalAnchorComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var messageRange = new FormattedMessage();
        messageRange.AddText(Loc.GetString($"examine-anchor-island-search-range") + " ");
        messageRange.PushColor(Color.Aqua);
        messageRange.AddText($"{component.IslandSearchRange}\n");
        messageRange.Pop();
        args.PushMessage(messageRange);

        if (component.AnchorUsedTime is not { } timeUsed)
        {
            if (component.Enabled)
            {
                var messageWavesWillnotDisable = new FormattedMessage();
                messageWavesWillnotDisable.PushColor(Color.Orange);
                messageWavesWillnotDisable.AddText(Loc.GetString($"examine-anchor-waves-will-not-disable"));
                messageWavesWillnotDisable.Pop();
                args.PushMessage(messageWavesWillnotDisable);
            }
            else if (!component.Enabled && SearchIslandInRange(uid, component.IslandSearchRange))
            {
                var messageIslandNear = new FormattedMessage();
                messageIslandNear.PushColor(Color.Yellow);
                messageIslandNear.AddText(Loc.GetString($"examine-anchor-island-near"));
                messageIslandNear.Pop();
                args.PushMessage(messageIslandNear);
            }
            else if (!component.Enabled && SearchIslandInRange(uid, component.IslandSearchRange))
            {
                var messageIslandFar = new FormattedMessage();
                messageIslandFar.PushColor(Color.OrangeRed);
                messageIslandFar.AddText(Loc.GetString($"examine-anchor-island-far"));
                messageIslandFar.Pop();
                args.PushMessage(messageIslandFar);
            }
            return;
        }

        if (timeUsed + TimeSpan.FromSeconds(component.WavesTimer) > _timing.CurTime)
        {
            var messageTimeToDisable = new FormattedMessage();
            messageTimeToDisable.AddText(Loc.GetString($"examine-anchor-time-to-disable-waves") + " ");
            messageTimeToDisable.PushColor(Color.Aquamarine);
            messageTimeToDisable.AddText($"{(int)(timeUsed + TimeSpan.FromSeconds(component.WavesTimer) - _timing.CurTime).TotalSeconds}");
            messageTimeToDisable.Pop();
            args.PushMessage(messageTimeToDisable);
        }
        else
        {
            var messageWavesDisabled = new FormattedMessage();
            messageWavesDisabled.PushColor(Color.GreenYellow);
            messageWavesDisabled.AddText(Loc.GetString($"examine-anchor-waves-disabled"));
            messageWavesDisabled.Pop();
            args.PushMessage(messageWavesDisabled);
        }
    }
}
