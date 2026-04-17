using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Physics;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.SubFloor;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Server.Stealth;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;

namespace Content.Server.Atmos.Piping.EntitySystems
{
    public sealed class VentCrawlerSystem : EntitySystem
    {
        private static readonly SoundSpecifier VentCrawlSound = new SoundCollectionSpecifier("FootstepSCPMTF");
        private const float VentCrawlSoundDistance = 1.5f;

        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedEyeSystem _eye = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly StealthSystem _stealth = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly VisibilitySystem _visibility = default!;
        [Dependency] private readonly WeldableSystem _weldable = default!;

        private readonly Dictionary<EntityUid, int> _revealedEntityRefs = new();

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasVentPumpComponent, GetVerbsEvent<InteractionVerb>>(OnVentPumpGetVerbs);
            SubscribeLocalEvent<GasVentScrubberComponent, GetVerbsEvent<InteractionVerb>>(OnVentScrubberGetVerbs);
            SubscribeLocalEvent<GasPassiveVentComponent, GetVerbsEvent<InteractionVerb>>(OnPassiveVentGetVerbs);

            SubscribeLocalEvent<GasVentPumpComponent, EnterVentCrawlerDoAfterEvent>(OnVentPumpEnterDoAfter);
            SubscribeLocalEvent<GasVentScrubberComponent, EnterVentCrawlerDoAfterEvent>(OnVentScrubberEnterDoAfter);
            SubscribeLocalEvent<GasPassiveVentComponent, EnterVentCrawlerDoAfterEvent>(OnPassiveVentEnterDoAfter);
            SubscribeLocalEvent<GasVentPumpComponent, ExitVentCrawlerDoAfterEvent>(OnVentPumpExitDoAfter);
            SubscribeLocalEvent<GasVentScrubberComponent, ExitVentCrawlerDoAfterEvent>(OnVentScrubberExitDoAfter);
            SubscribeLocalEvent<GasPassiveVentComponent, ExitVentCrawlerDoAfterEvent>(OnPassiveVentExitDoAfter);

            SubscribeLocalEvent<VentCrawlingComponent, GetVisMaskEvent>(OnVentCrawlerGetVisMask);
            SubscribeLocalEvent<VentCrawlingComponent, RefreshMovementSpeedModifiersEvent>(OnVentCrawlerRefreshMove);
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

        private void OnVentPumpExitDoAfter(Entity<GasVentPumpComponent> ent, ref ExitVentCrawlerDoAfterEvent args)
        {
            HandleExitDoAfter(ent.Owner, ref args);
        }

        private void OnVentScrubberExitDoAfter(Entity<GasVentScrubberComponent> ent, ref ExitVentCrawlerDoAfterEvent args)
        {
            HandleExitDoAfter(ent.Owner, ref args);
        }

        private void OnPassiveVentExitDoAfter(Entity<GasPassiveVentComponent> ent, ref ExitVentCrawlerDoAfterEvent args)
        {
            HandleExitDoAfter(ent.Owner, ref args);
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
                    Act = () => TryStartExitVent(user, vent, active),
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

        private void TryStartExitVent(EntityUid user, EntityUid vent, VentCrawlingComponent active)
        {
            if (!CanExitVent(user, vent, active) || !TryComp(user, out VentCrawlerComponent? crawler))
                return;

            var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(crawler.ExitDelay), new ExitVentCrawlerDoAfterEvent(), vent, used: vent)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = true,
                BreakOnDamage = true,
                NeedHand = false,
                RequireCanInteract = false,
            };

            _doAfter.TryStartDoAfter(doAfter);
        }

        private void HandleExitDoAfter(EntityUid vent, ref ExitVentCrawlerDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            if (!TryComp(args.User, out VentCrawlingComponent? active) || !CanExitVent(args.User, vent, active))
                return;

            ExitVent(args.User, vent);
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
            EnsureComp<ActiveVentCrawlingComponent>(user);
            active.SourceVent = vent;
            active.RemovedComplexInteraction = false;
            active.WasCollidable = true;
            active.FixtureStates.Clear();
            active.AddedStealth = false;
            active.AddedVisibility = false;
            active.PreviousStealthEnabled = true;
            active.PreviousStealthVisibility = 1f;
            active.PreviousVisibilityLayer = (ushort) VisibilityFlags.Normal;
            active.RevertingMove = false;
            active.SoundDistance = 0f;
            active.RevealedEntities.Clear();
            active.DisabledActions.Clear();

            if (TryComp(user, out PhysicsComponent? physics))
            {
                active.WasCollidable = physics.CanCollide;
                EnterVentPhysics(user, active, physics);

                if (physics.CanCollide)
                    _physics.SetCanCollide(user, false, body: physics);

                _physics.ResetDynamics(user, physics);
            }

            EnterVentStealth(user, active);
            EnterVentVisibility(user, active);
            EnterVentInteraction(user, active);
            SetActionAbilitiesEnabled(user, false, active);
            RevealConnectedNetwork(active);
            _transform.SetCoordinates(user, Transform(user), Transform(vent).Coordinates);
            _movement.RefreshMovementSpeedModifiers(user);

            if (TryComp(user, out EyeComponent? eye))
                _eye.RefreshVisibilityMask((user, eye));
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
            {
                PlayVentCrawlSound(ent, ref args);
                return;
            }

            ent.Comp.RevertingMove = true;
            _transform.SetCoordinates(ent.Owner, args.Component, args.OldPosition);

            if (TryComp(ent.Owner, out PhysicsComponent? physics))
                _physics.ResetDynamics(ent.Owner, physics);
        }

        private void OnVentCrawlerGetVisMask(Entity<VentCrawlingComponent> ent, ref GetVisMaskEvent args)
        {
            args.VisibilityMask |= (int) VisibilityFlags.Subfloor;
            args.VisibilityMask |= (int) VisibilityFlags.SpiderVent;
        }

        private void OnVentCrawlingShutdown(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
        {
            if (TryComp(ent.Owner, out PhysicsComponent? physics))
            {
                ExitVentPhysics(ent.Owner, ent.Comp, physics);
                _physics.SetCanCollide(ent.Owner, ent.Comp.WasCollidable, body: physics);
                _physics.ResetDynamics(ent.Owner, physics);
            }

            HideConnectedNetwork(ent.Comp);
            RemComp<ActiveVentCrawlingComponent>(ent.Owner);
            ExitVentInteraction(ent.Owner, ent.Comp);
            ExitVentVisibility(ent.Owner, ent.Comp);
            ExitVentStealth(ent.Owner, ent.Comp);
            SetActionAbilitiesEnabled(ent.Owner, true, ent.Comp);
            _movement.RefreshMovementSpeedModifiers(ent.Owner);

            if (TryComp(ent.Owner, out EyeComponent? eye))
                _eye.RefreshVisibilityMask((ent.Owner, eye));
        }

        private void OnVentCrawlerRefreshMove(Entity<VentCrawlingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
        {
            if (!TryComp(ent.Owner, out VentCrawlerComponent? crawler))
                return;

            args.ModifySpeed(crawler.VentSpeedMultiplier, crawler.VentSpeedMultiplier);
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

        private void EnterVentStealth(EntityUid user, VentCrawlingComponent active)
        {
            var hadStealth = TryComp(user, out StealthComponent? stealth);
            if (!hadStealth)
            {
                stealth = EnsureComp<StealthComponent>(user);
                active.AddedStealth = true;
            }

            var stealthComp = stealth!;

            active.PreviousStealthEnabled = stealthComp.Enabled;
            active.PreviousStealthVisibility = _stealth.GetVisibility(user, stealthComp);

            _stealth.SetEnabled(user, true, stealthComp);
            _stealth.SetVisibility(user, -1f, stealthComp);
        }

        private void ExitVentStealth(EntityUid user, VentCrawlingComponent active)
        {
            if (!TryComp(user, out StealthComponent? stealth))
                return;

            if (active.AddedStealth)
            {
                RemComp<StealthComponent>(user);
                return;
            }

            _stealth.SetEnabled(user, active.PreviousStealthEnabled, stealth);
            _stealth.SetVisibility(user, active.PreviousStealthVisibility, stealth);
        }

        private void EnterVentVisibility(EntityUid user, VentCrawlingComponent active)
        {
            var hadVisibility = TryComp(user, out VisibilityComponent? visibility);
            if (!hadVisibility)
            {
                visibility = EnsureComp<VisibilityComponent>(user);
                active.AddedVisibility = true;
            }

            var visibilityComp = visibility!;

            active.PreviousVisibilityLayer = visibilityComp.Layer;
            _visibility.AddLayer((user, visibilityComp), (ushort) VisibilityFlags.SpiderVent, false);
            _visibility.RemoveLayer((user, visibilityComp), (ushort) VisibilityFlags.Normal, false);
            _visibility.RefreshVisibility(user, visibilityComponent: visibilityComp);
        }

        private void EnterVentInteraction(EntityUid user, VentCrawlingComponent active)
        {
            if (!HasComp<ComplexInteractionComponent>(user))
                return;

            active.RemovedComplexInteraction = true;
            RemComp<ComplexInteractionComponent>(user);
        }

        private void ExitVentInteraction(EntityUid user, VentCrawlingComponent active)
        {
            if (!active.RemovedComplexInteraction)
                return;

            EnsureComp<ComplexInteractionComponent>(user);
            active.RemovedComplexInteraction = false;
        }

        private void EnterVentPhysics(EntityUid user, VentCrawlingComponent active, PhysicsComponent physics)
        {
            if (!TryComp(user, out FixturesComponent? fixtures))
                return;

            active.FixtureStates.Clear();

            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                active.FixtureStates.Add(new VentCrawlerFixtureState
                {
                    Id = id,
                    Hard = fixture.Hard,
                    CollisionLayer = fixture.CollisionLayer,
                    CollisionMask = fixture.CollisionMask,
                });

                _physics.SetHard(user, fixture, false, fixtures);
                _physics.SetCollisionLayer(user, id, fixture, (int) CollisionGroup.None, fixtures, physics);
                _physics.SetCollisionMask(user, id, fixture, (int) CollisionGroup.None, fixtures, physics);
            }
        }

        private void ExitVentPhysics(EntityUid user, VentCrawlingComponent active, PhysicsComponent physics)
        {
            if (!TryComp(user, out FixturesComponent? fixtures))
                return;

            foreach (var state in active.FixtureStates)
            {
                if (!fixtures.Fixtures.TryGetValue(state.Id, out var fixture))
                    continue;

                _physics.SetHard(user, fixture, state.Hard, fixtures);
                _physics.SetCollisionLayer(user, state.Id, fixture, state.CollisionLayer, fixtures, physics);
                _physics.SetCollisionMask(user, state.Id, fixture, state.CollisionMask, fixtures, physics);
            }

            active.FixtureStates.Clear();
        }

        private void ExitVentVisibility(EntityUid user, VentCrawlingComponent active)
        {
            if (!TryComp(user, out VisibilityComponent? visibility))
                return;

            if (active.AddedVisibility)
            {
                RemComp<VisibilityComponent>(user);
                return;
            }

            _visibility.SetLayer((user, visibility), active.PreviousVisibilityLayer, false);
            _visibility.RefreshVisibility(user, visibilityComponent: visibility);
        }

        private void SetActionAbilitiesEnabled(EntityUid user, bool enabled, VentCrawlingComponent active)
        {
            if (!enabled)
            {
                foreach (var action in _actions.GetActions(user))
                {
                    if (!action.Comp.Enabled)
                        continue;

                    active.DisabledActions.Add(action.Owner);
                    var actionEnt = new Entity<ActionComponent?>(action.Owner, (ActionComponent?) action.Comp);
                    _actions.SetEnabled(actionEnt, false);
                }

                return;
            }

            foreach (var actionUid in active.DisabledActions)
            {
                if (!TryComp(actionUid, out ActionComponent? action))
                    continue;

                var actionEnt = new Entity<ActionComponent?>(actionUid, (ActionComponent?) action);
                _actions.SetEnabled(actionEnt, true);
            }

            active.DisabledActions.Clear();
        }

        private void RevealConnectedNetwork(VentCrawlingComponent active)
        {
            if (!TryGetPipeNode(active.SourceVent, out var sourcePipe))
                return;

            if (sourcePipe.NodeGroup is not BaseNodeGroup group)
            {
                RevealEntity(active.SourceVent, active);
                return;
            }

            foreach (var node in group.Nodes)
            {
                RevealEntity(node.Owner, active);
            }
        }

        private void RevealEntity(EntityUid uid, VentCrawlingComponent active)
        {
            if (!active.RevealedEntities.Add(uid))
                return;

            if (_revealedEntityRefs.TryGetValue(uid, out var count))
            {
                _revealedEntityRefs[uid] = count + 1;
                return;
            }

            _revealedEntityRefs[uid] = 1;
            _visibility.AddLayer((uid, CompOrNull<VisibilityComponent>(uid)), (ushort) VisibilityFlags.SpiderVent, false);
            SetSubfloorRevealed(uid, true);
            _visibility.RefreshVisibility(uid);
        }

        private void HideConnectedNetwork(VentCrawlingComponent active)
        {
            foreach (var uid in active.RevealedEntities)
            {
                if (!_revealedEntityRefs.TryGetValue(uid, out var count))
                    continue;

                if (count > 1)
                {
                    _revealedEntityRefs[uid] = count - 1;
                    continue;
                }

                _revealedEntityRefs.Remove(uid);
                _visibility.RemoveLayer((uid, CompOrNull<VisibilityComponent>(uid)), (ushort) VisibilityFlags.SpiderVent, false);
                SetSubfloorRevealed(uid, false);
                _visibility.RefreshVisibility(uid);
            }

            active.RevealedEntities.Clear();
        }

        private void SetSubfloorRevealed(EntityUid uid, bool revealed)
        {
            if (!TryComp(uid, out AppearanceComponent? appearance) || !HasComp<SubFloorHideComponent>(uid))
                return;

            _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, revealed, appearance);
        }

        private void PlayVentCrawlSound(Entity<VentCrawlingComponent> ent, ref MoveEvent args)
        {
            if (!args.OldPosition.TryDistance(EntityManager, args.NewPosition, out var distance) || distance <= 0f)
                return;

            if (distance > VentCrawlSoundDistance)
                ent.Comp.SoundDistance = VentCrawlSoundDistance;
            else
                ent.Comp.SoundDistance += distance;

            if (ent.Comp.SoundDistance < VentCrawlSoundDistance)
                return;

            ent.Comp.SoundDistance -= VentCrawlSoundDistance;
            _audio.PlayPvs(VentCrawlSound, ent.Owner);
        }
    }
}