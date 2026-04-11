using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.SubFloor;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;

namespace Content.Server.Atmos.Piping.EntitySystems
{
    public sealed class VentCrawlerSystem : EntitySystem
    {
        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedEyeSystem _eye = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly WeldableSystem _weldable = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasVentPumpComponent, GetVerbsEvent<InteractionVerb>>(OnVentPumpGetVerbs);
            SubscribeLocalEvent<GasVentScrubberComponent, GetVerbsEvent<InteractionVerb>>(OnVentScrubberGetVerbs);
            SubscribeLocalEvent<GasPassiveVentComponent, GetVerbsEvent<InteractionVerb>>(OnPassiveVentGetVerbs);

            SubscribeLocalEvent<GasVentPumpComponent, EnterVentCrawlerDoAfterEvent>(OnVentPumpEnterDoAfter);
            SubscribeLocalEvent<GasVentScrubberComponent, EnterVentCrawlerDoAfterEvent>(OnVentScrubberEnterDoAfter);
            SubscribeLocalEvent<GasPassiveVentComponent, EnterVentCrawlerDoAfterEvent>(OnPassiveVentEnterDoAfter);

            SubscribeLocalEvent<VentCrawlingComponent, MoveEvent>(OnVentCrawlerMoved);
            SubscribeLocalEvent<VentCrawlingComponent, ComponentShutdown>(OnVentCrawlingShutdown);
        }

        private void OnVentPumpGetVerbs(Entity<GasVentPumpComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
        {
            AddVentCrawlerVerbs(ent.Owner, ref args);
        }

        private void OnVentScrubberGetVerbs(Entity<GasVentScrubberComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
        {
            AddVentCrawlerVerbs(ent.Owner, ref args);
        }

        private void OnPassiveVentGetVerbs(Entity<GasPassiveVentComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
        {
            AddVentCrawlerVerbs(ent.Owner, ref args);
        }

        private void OnVentPumpEnterDoAfter(Entity<GasVentPumpComponent> ent, ref EnterVentCrawlerDoAfterEvent args)
        {
            HandleEnterDoAfter(ent.Owner, ref args);
        }

        private void OnVentScrubberEnterDoAfter(Entity<GasVentScrubberComponent> ent, ref EnterVentCrawlerDoAfterEvent args)
        {
            HandleEnterDoAfter(ent.Owner, ref args);
        }

        private void OnPassiveVentEnterDoAfter(Entity<GasPassiveVentComponent> ent, ref EnterVentCrawlerDoAfterEvent args)
        {
            HandleEnterDoAfter(ent.Owner, ref args);
        }

        private void AddVentCrawlerVerbs(EntityUid vent, ref GetVerbsEvent<InteractionVerb> args)
        {
            if (!_actionBlocker.CanConsciouslyPerformAction(args.User) || !_interaction.InRangeUnobstructed(args.User, vent))
                return;

            var user = args.User;

            if (CanEnterVent(user, vent, out var crawler))
            {
                args.Verbs.Add(new InteractionVerb
                {
                    Priority = 1,
                    Text = Loc.GetString("vent-crawler-verb-enter"),
                    Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
                    DoContactInteraction = false,
                    Act = () => TryStartEnterVent(user, vent, crawler),
                });
            }

            if (TryComp<VentCrawlingComponent>(user, out var active) && CanExitVent(user, vent, active))
            {
                args.Verbs.Add(new InteractionVerb
                {
                    Priority = 2,
                    Text = Loc.GetString("vent-crawler-verb-exit"),
                    Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    DoContactInteraction = false,
                    Act = () => ExitVent(user, vent),
                });
            }
        }

        private void TryStartEnterVent(EntityUid user, EntityUid vent, VentCrawlerComponent crawler)
        {
            if (!CanEnterVent(user, vent, out _) || !_actionBlocker.CanConsciouslyPerformAction(user) || !_interaction.InRangeUnobstructed(user, vent))
                return;

            var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(crawler.EnterDelay), new EnterVentCrawlerDoAfterEvent(), vent, used: vent)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = true,
                BreakOnDamage = true,
                NeedHand = false,
            };

            _doAfter.TryStartDoAfter(doAfter);
        }

        private void HandleEnterDoAfter(EntityUid vent, ref EnterVentCrawlerDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            if (!CanEnterVent(args.User, vent, out _) || !_actionBlocker.CanConsciouslyPerformAction(args.User) || !_interaction.InRangeUnobstructed(args.User, vent))
                return;

            EnterVent(args.User, vent);
            args.Handled = true;
        }

        private bool CanEnterVent(EntityUid user, EntityUid vent, out VentCrawlerComponent crawler)
        {
            crawler = default!;
            if (!TryComp<VentCrawlerComponent>(user, out var component))
                return false;

            crawler = component;

            if (HasComp<VentCrawlingComponent>(user))
                return false;

            if (!Transform(vent).Anchored)
                return false;

            if (_weldable.IsWelded(vent))
                return false;

            return TryGetPipeNode(vent, out _);
        }

        private bool CanExitVent(EntityUid user, EntityUid vent, VentCrawlingComponent active)
        {
            if (!IsOnSameTile(user, vent))
                return false;

            return IsVentOnSameNetwork(vent, active);
        }

        private void EnterVent(EntityUid user, EntityUid vent)
        {
            var active = EnsureComp<VentCrawlingComponent>(user);
            active.SourceVent = vent;
            active.WasCollidable = true;
            active.AddedSubFloorHide = false;
            active.RevertingMove = false;

            if (TryComp(user, out PhysicsComponent? physics))
            {
                active.WasCollidable = physics.CanCollide;
                if (physics.CanCollide)
                    _physics.SetCanCollide(user, false, body: physics);

                _physics.ResetDynamics(user, physics);
            }

            if (!HasComp<SubFloorHideComponent>(user))
            {
                EnsureComp<SubFloorHideComponent>(user);
                active.AddedSubFloorHide = true;
            }

            var trayUser = EnsureComp<TrayScannerUserComponent>(user);
            trayUser.Count++;

            if (trayUser.Count == 1 && TryComp(user, out EyeComponent? eye))
                _eye.RefreshVisibilityMask((user, eye));

            _transform.SetCoordinates(user, Transform(user), Transform(vent).Coordinates);
        }

        private void ExitVent(EntityUid user, EntityUid vent)
        {
            if (!TryComp(user, out VentCrawlingComponent? active) || !CanExitVent(user, vent, active))
                return;

            _transform.SetCoordinates(user, Transform(user), Transform(vent).Coordinates);
            RemComp<VentCrawlingComponent>(user);
        }

        private void OnVentCrawlerMoved(Entity<VentCrawlingComponent> ent, ref MoveEvent args)
        {
            if (ent.Comp.RevertingMove)
            {
                ent.Comp.RevertingMove = false;
                return;
            }

            if (IsValidVentPosition(ent.Comp, args.NewPosition))
                return;

            ent.Comp.RevertingMove = true;
            _transform.SetCoordinates(ent.Owner, args.Component, args.OldPosition);

            if (TryComp(ent.Owner, out PhysicsComponent? physics))
                _physics.ResetDynamics(ent.Owner, physics);
        }

        private void OnVentCrawlingShutdown(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
        {
            if (ent.Comp.AddedSubFloorHide)
                RemComp<SubFloorHideComponent>(ent.Owner);

            if (TryComp(ent.Owner, out PhysicsComponent? physics))
            {
                _physics.SetCanCollide(ent.Owner, ent.Comp.WasCollidable, body: physics);
                _physics.ResetDynamics(ent.Owner, physics);
            }

            if (TryComp(ent.Owner, out TrayScannerUserComponent? trayUser))
            {
                trayUser.Count = Math.Max(0, trayUser.Count - 1);

                if (trayUser.Count == 0)
                    RemComp<TrayScannerUserComponent>(ent.Owner);

                if (TryComp(ent.Owner, out EyeComponent? eye))
                    _eye.RefreshVisibilityMask((ent.Owner, eye));
            }
        }

        private bool IsValidVentPosition(VentCrawlingComponent active, EntityCoordinates coordinates)
        {
            if (!TryGetPipeNode(active.SourceVent, out var sourcePipe))
                return false;

            if (!TryGetGridAndTile(coordinates, out var gridUid, out var grid, out var tilePos))
                return false;

            foreach (var anchored in _map.GetAnchoredEntities((gridUid, grid), tilePos))
            {
                if (HasConnectedPipeNode(anchored, sourcePipe, active.SourceVent))
                    return true;
            }

            return false;
        }

        private bool IsVentOnSameNetwork(EntityUid vent, VentCrawlingComponent active)
        {
            if (!TryGetPipeNode(active.SourceVent, out var sourcePipe))
                return false;

            return HasConnectedPipeNode(vent, sourcePipe, active.SourceVent);
        }

        private bool HasConnectedPipeNode(EntityUid uid, PipeNode sourcePipe, EntityUid sourceVent)
        {
            if (!TryComp(uid, out NodeContainerComponent? nodeContainer))
                return false;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not PipeNode pipe)
                    continue;

                if (sourcePipe.NodeGroup == null)
                    return uid == sourceVent;

                if (ReferenceEquals(pipe.NodeGroup, sourcePipe.NodeGroup))
                    return true;
            }

            return false;
        }

        private bool TryGetPipeNode(EntityUid uid, out PipeNode pipeNode)
        {
            pipeNode = default!;

            if (!TryComp(uid, out NodeContainerComponent? nodeContainer))
                return false;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not PipeNode pipe)
                    continue;

                pipeNode = pipe;
                return true;
            }

            return false;
        }

        private bool TryGetGridAndTile(EntityCoordinates coordinates, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tilePos)
        {
            gridUid = default;
            grid = default!;
            tilePos = default;

            if (!TryComp<MapGridComponent>(coordinates.EntityId, out var gridComp))
                return false;

            grid = gridComp;
            gridUid = coordinates.EntityId;
            tilePos = _map.TileIndicesFor(gridUid, grid, coordinates);
            return true;
        }

        private bool IsOnSameTile(EntityUid first, EntityUid second)
        {
            if (!TryGetGridAndTile(Transform(first).Coordinates, out var firstGridUid, out _, out var firstTile))
                return false;

            if (!TryGetGridAndTile(Transform(second).Coordinates, out var secondGridUid, out _, out var secondTile))
                return false;

            return firstGridUid == secondGridUid && firstTile == secondTile;
        }
    }
}