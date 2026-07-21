using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Siege.Events;
using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Physics.Components; // Нужно для управления физикой
using Robust.Shared.Physics.Systems;   // Нужно для управления физикой
using Content.Shared.ShiftFront.Components; // Остальные зависимости оставлены как были
using Content.Shared.Actions;
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Spawners;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Damage;
using Content.Server.Mind;
using Content.Shared.Interaction.Events;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Physics.Events;
using Content.Shared.Audio;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.FPV;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Interaction.Components;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Shared.Radio.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Robust.Shared.Network;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftTankSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedActionsSystem _action = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly SharedEyeSystem _eye = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftTankHullComponent, ComponentStartup>(TankStart);
            SubscribeLocalEvent<ShiftTankHullComponent, ComponentShutdown>(TankEnd);
            SubscribeLocalEvent<ShiftTankTurretComponent, ComponentStartup>(TurretStart);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStartMoveEvent>(OnTankStartMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStopMoveEvent>(OnTankStopMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotateEvent>(OnToggleRotate);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotationDirectionEvent>(OnToggleRotationDirection);
            SubscribeLocalEvent<ShiftTankHullComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ShiftTankpartComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<ShiftTankHullComponent, ActivateInWorldEvent>(OnActivateTank);
            SubscribeLocalEvent<ShiftPlayerComponent, EntitySpokeEvent>(OnSpeak);
            SubscribeLocalEvent<ShiftFPVDroneComponent, EntitySpokeEvent>(OnTankSpeak);
            SubscribeLocalEvent<ShiftTankAmmoComponent, BeforeRangedInteractEvent>(OnUseInHandAmmo);
            SubscribeLocalEvent<ShiftTankpartComponent, ShiftTankLoadDoAfter>(OnAmmoDoAfter);

        }

        public void OnAmmoDoAfter(EntityUid uid, ShiftTankpartComponent comp, ShiftTankLoadDoAfter args)
        {
            RemComp<ShiftTankReloaderComponent>(args.User);
            if (args.Cancelled) return;
            if (comp.Part != "Cartridge") return;
            if (!TryComp<ShiftTankHullComponent>(comp.Tank, out var tank)) return;
            if (!TryComp<BallisticAmmoProviderComponent>(tank.LinkedTurret, out var ammo)) return;
            ammo.UnspawnedCount += 1;
            ammo.Proto = comp.Ammo;
            Dirty(uid, ammo);
        }
        public void OnUseInHandAmmo(EntityUid uid, ShiftTankAmmoComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            if (!TryComp<ShiftTankpartComponent>(args.Target, out var part) || part.Part != "Cartridge") return;
            if (!TryComp<ShiftTankHullComponent>(part.Tank, out var tank)) return;
            if (!TryComp<BallisticAmmoProviderComponent>(tank.LinkedTurret, out var ammo)) return;
            if (ammo.UnspawnedCount >= ammo.Capacity) return;
            var meta = EntityManager.GetComponent<MetaDataComponent>(uid);
            if (meta.EntityPrototype == null) return;
            if (meta.EntityPrototype.ID == null) return;
            EntProtoId protoId = meta.EntityPrototype.ID;
            if (!TryComp<CartridgeAmmoComponent>(uid, out var cartridgeAmmo)) return;
            part.Ammo = cartridgeAmmo.Prototype;
            QueueDel(uid);
            var doAfterHit = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(4.5f), new ShiftTankLoadDoAfter(), target: args.Target, eventTarget: args.Target)
            {
                BreakOnMove = false,
                BreakOnDamage = false,
                NeedHand = false,
                CancelDuplicate = true
            };
            EnsureComp<ShiftTankReloaderComponent>(args.User);
            _doAfter.TryStartDoAfter(doAfterHit);
            _audio.PlayPvs(part.SoundAmmoLoad, part.Owner);
            _audio.PlayPvs(part.SoundAmmoLoad, tank.Owner);

        }//MagazineLightRifleHard

        public bool CheckResearch(string research, string faction)
        {
            var requery = EntityQueryEnumerator<ShiftConsoleResearchComponent>();
            while (requery.MoveNext(out var reuid, out var recomp))
            {
                if (recomp.Researched != null && recomp.Researched.Contains(research) && recomp.Faction == faction)
                    return true;
            }
            return false;
        }
        private void OnTankSpeak(EntityUid uid, ShiftFPVDroneComponent comp, EntitySpokeEvent args)
        {
            if (args.Whisper) return;
            if (!comp.TankPart) return;
            if (comp.Pilot == null) return;
            if (!HasComp<ShiftFPVPilotComponent>(comp.Pilot)) return;
            _chat.TrySendInGameICMessage(comp.Pilot.Value, args.Message, InGameICChatType.Speak, false);
        }
        private void OnSpeak(EntityUid uid, ShiftPlayerComponent comp, EntitySpokeEvent args)
        {
            if (comp.Vehicle == null) return;
            if (HasComp<ShiftFPVPilotComponent>(uid)) return;
            _chat.TrySendInGameICMessage(comp.Vehicle.Value, args.Message, InGameICChatType.Whisper, false, nameOverride: "голос изнутри техники");
        }
        private void TankEnd(EntityUid uid, ShiftTankHullComponent comp, ref ComponentShutdown args)
        {
            //if (comp.LinkedGrid != null) QueueDel(comp.LinkedGrid.Value);
            if (comp.LinkedTurret != null) QueueDel(comp.LinkedTurret.Value);
            if (comp.LinkedObserver != null) QueueDel(comp.LinkedObserver.Value);
            if (comp.InsideControllerEntity != null) Spawn("MedievalExplodeAp2", Transform(comp.InsideControllerEntity.Value).Coordinates);
        }
        private void OnActivateTank(EntityUid uid, ShiftTankHullComponent comp, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;
            if (!HasComp<ShiftPlayerComponent>(args.User) && !HasComp<BypassInteractionChecksComponent>(args.User)) return;
            if (TryComp<ShiftPlayerComponent>(args.User, out var player) && player.Faction != comp.Faction) return;
            if (comp.InsideEntryEntity == null) return;
            var xform = Transform(comp.InsideEntryEntity.Value);
            var coords = xform.Coordinates;
            if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling != null)
            {
                _transform.SetCoordinates(puller.Pulling.Value, coords);
            }
            _transform.SetCoordinates(args.User, coords);
            if (player != null)
                player.Vehicle = uid;
        }
        public void TurretStart(EntityUid uid, ShiftTankTurretComponent component, ComponentStartup args)
        {
            //_action.AddAction(uid, "StopFPVControll");
        }

        private void OnActivate(EntityUid uid, ShiftTankpartComponent comp, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;
            if (comp.Tank == null) return;

            var tank = EnsureComp<ShiftTankHullComponent>(comp.Tank.Value);
            switch (comp.Part)
            {
                case "Controller":
                    break;
                case "Gunner":
                    break;
                case "Exit":
                    var xform = Transform(comp.Tank.Value);
                    var coords = xform.Coordinates;
                    if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling != null)
                    {
                        _transform.SetCoordinates(puller.Pulling.Value, coords);
                    }
                    _transform.SetCoordinates(args.User, coords);
                    if (TryComp<ShiftPlayerComponent>(args.User, out var player))
                        player.Vehicle = null;
                    break;
                case "Observer":
                    break;
            }

        }

        private void OnExamine(EntityUid uid, ShiftTankHullComponent comp, ExaminedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > 0)
                args.PushMarkup($"У техники [color=red]{Math.Round(dam.TotalDamage.Float(), 2)}[/color] единиц  повреждений", 5);
        }
        public void TankStart(EntityUid uid, ShiftTankHullComponent component, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontREBVehicle", component.Faction))
            {
                EnsureComp<ShiftREBComponent>(uid, out var reb);
                reb.Radius = component.RebRadius;
            }
            //_action.AddAction(uid, "TankStartMoveAction");
            //_action.AddAction(uid, "TankStopMoveAction");
            //_action.AddAction(uid, "TankChangeMoveDirectionAction");
            //_action.AddAction(uid, "TankToggleRotateAction");
            //_action.AddAction(uid, "TankToggleRotationDirectionAction");
            EnsureComp<PhysicsComponent>(uid);
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                _physics.SetFixedRotation(uid, false, body: physics);
            }
            if (component.TurretProto != "")
            {
                var turret = Spawn(component.TurretProto, Transform(uid).Coordinates);
                _transform.SetParent(turret, uid);
                EnsureComp<ShiftTankTurretComponent>(turret, out var turretcomp);
                turretcomp.LinkedTank = uid;
                component.LinkedTurret = turret;
            }
            if (component.ObserverProto != "")
            {
                var observ = Spawn(component.ObserverProto, Transform(uid).Coordinates);
                _transform.SetParent(observ, uid);
                component.LinkedObserver = observ;
                EnsureComp<ShiftTankTurretComponent>(observ, out var turretcomp2);
                turretcomp2.LinkedTank = uid;
            }
            var map = SpawnMap(component.GridLink);
            if (!map.HasValue) return;
            component.LinkedGrid = map.Value.Owner;
            var query = EntityQueryEnumerator<ShiftInsideMarkerComponent>();
            while (query.MoveNext(out var insuid, out var inside))
            {
                switch (inside.Inside)
                {
                    case "Controller":
                        if (component.InsideController == "") continue;
                        component.InsideControllerEntity = Spawn(component.InsideController, Transform(insuid).Coordinates);
                        HandleControllerSetup(insuid, component, uid);
                        break;
                    case "Gunner":
                        if (component.InsideGunner == "") continue;
                        component.InsideGunnerEntity = Spawn(component.InsideGunner, Transform(insuid).Coordinates);
                        HandleGunnerSetup(insuid, component, uid);
                        break;
                    case "Cartridge":
                        if (component.InsideCartridge == "") continue;
                        component.InsideCartridgeEntity = Spawn(component.InsideCartridge, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideCartridgeEntity.Value, component, uid);
                        break;
                    case "Exit":
                        if (component.InsideExit == "") continue;
                        component.InsideExitEntity = Spawn(component.InsideExit, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideExitEntity.Value, component, uid);
                        break;
                    case "Entry":
                        if (component.InsideEntry == "") continue;
                        component.InsideEntryEntity = Spawn(component.InsideEntry, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideEntryEntity.Value, component, uid);
                        break;
                    case "Rotor":
                        if (component.InsideRotor == "") continue;
                        component.InsideRotorEntity = Spawn(component.InsideRotor, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideRotorEntity.Value, component, uid);
                        break;
                    case "Ammo":
                        if (component.InsideAmmo == "") continue;
                        component.InsideAmmoEntity = Spawn(component.InsideAmmo, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideAmmoEntity.Value, component, uid);
                        break;
                    case "Motor":
                        if (component.InsideMotor == "") continue;
                        component.InsideMotorEntity = Spawn(component.InsideMotor, Transform(insuid).Coordinates);
                        HandleBasicPartSetup(insuid, component.InsideMotorEntity.Value, component, uid);
                        break;
                    case "Observer":
                        if (component.InsideObserver == "") continue;
                        component.InsideObserverEntity = Spawn(component.InsideObserver, Transform(insuid).Coordinates);
                        HandleObserverSetup(insuid, component, uid);
                        break;
                }
            }
        }

        private void HandleBasicPartSetup(EntityUid insuid, EntityUid partEntity, ShiftTankHullComponent component, EntityUid uid)
        {
            EnsureComp<TimedDespawnComponent>(insuid, out var despawn);
            EnsureComp<ShiftTankpartComponent>(partEntity, out var part);
            part.Tank = uid;
            despawn.Lifetime = 0.05f;
        }

        private void HandleControllerSetup(EntityUid insuid, ShiftTankHullComponent component, EntityUid uid)
        {
            EnsureComp<TimedDespawnComponent>(insuid, out var despawn);
            if (component.InsideControllerEntity == null) return;
            EnsureComp<ShiftTankpartComponent>(component.InsideControllerEntity.Value, out var controller);
            EnsureComp<ShiftFPVControllerComponent>(component.InsideControllerEntity.Value, out var control);
            control.TankPart = true;
            control.LinkedDrone = uid;
            EnsureComp<ShiftFPVDroneComponent>(uid, out var drone);
            drone.Controller = component.InsideControllerEntity.Value;
            drone.Explosive = false;
            drone.Pacif = false;
            drone.TankPart = true;
            controller.Tank = uid;
            despawn.Lifetime = 0.05f;
        }

        private void HandleGunnerSetup(EntityUid insuid, ShiftTankHullComponent component, EntityUid uid)
        {
            EnsureComp<TimedDespawnComponent>(insuid, out var despawn);
            if (component.InsideGunnerEntity == null) return;
            EnsureComp<ShiftTankpartComponent>(component.InsideGunnerEntity.Value, out var gunner);
            EnsureComp<ShiftFPVControllerComponent>(component.InsideGunnerEntity.Value, out var control);
            control.TankPart = true;
            if (component.LinkedTurret == null) return;
            control.LinkedDrone = component.LinkedTurret.Value;
            EnsureComp<ShiftFPVDroneComponent>(component.LinkedTurret.Value, out var drone);
            drone.Controller = component.InsideGunnerEntity.Value;
            drone.Explosive = false;
            drone.Pacif = false;
            drone.TankPart = true;
            gunner.Tank = uid;
            despawn.Lifetime = 0.05f;
        }

        private void HandleObserverSetup(EntityUid insuid, ShiftTankHullComponent component, EntityUid uid)
        {
            EnsureComp<TimedDespawnComponent>(insuid, out var despawn);
            if (component.InsideObserverEntity == null) return;
            EnsureComp<ShiftTankpartComponent>(component.InsideObserverEntity.Value, out var observer);
            observer.Tank = uid;
            despawn.Lifetime = 0.05f;
            EnsureComp<ShiftFPVControllerComponent>(component.InsideObserverEntity.Value, out var control);
            if (component.LinkedObserver == null) return;
            control.LinkedDrone = component.LinkedObserver.Value;
            control.TankPart = true;
            EnsureComp<ShiftFPVDroneComponent>(component.LinkedObserver.Value, out var drone);
            drone.Controller = component.InsideObserverEntity.Value;
            drone.Explosive = false;
            drone.Pacif = false;
            drone.TankPart = true;
        }
        private Entity<Robust.Shared.Map.Components.MapComponent>? SpawnMap(ResPath[] mappath)
        {
            var path = _random.Pick(mappath);
            var options = new DeserializationOptions
            {
                InitializeMaps = true,
            };

            if (_mapLoaderSystem.TryLoadMap(path, out var first, out var second, options))
                return first;
            return null;
        }
        private void OnTankShutdown(EntityUid uid, ShiftTankHullComponent component, ComponentShutdown args)
        {
            // Можно добавить другую логику очистки, если нужно
        }

        private void OnTankStartMove(EntityUid uid, ShiftTankHullComponent component, ref TankStartMoveEvent args)
        {
            if (component.IsMoving) return; // Уже движется

            component.IsMoving = true;
            Dirty(uid, component); // Синхронизировать состояние с клиентом, если нужно
        }

        private void OnTankStopMove(EntityUid uid, ShiftTankHullComponent component, ref TankStopMoveEvent args)
        {
            if (!component.IsMoving) return; // Уже остановлен

            component.IsMoving = false;
            //Log.Debug($"Tank {ToPrettyString(uid)} stopped moving.");

            // Явно останавливаем движение в физике
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            }
            Dirty(uid, component);
        }


        private void OnToggleRotate(EntityUid uid, ShiftTankHullComponent component, ref TankToggleRotateEvent args)
        {
            component.IsRotating = !component.IsRotating;
            Dirty(uid, component);
        }

        private void OnToggleRotationDirection(EntityUid uid, ShiftTankHullComponent component, ref TankToggleRotationDirectionEvent args)
        {
            component.RotationDirection *= -1; // Инвертируем направление вращения
            //Log.Debug($"Tank {ToPrettyString(uid)} toggled rotation direction to {component.RotationDirection}.");
            Dirty(uid, component);
            // Если танк вращается, смена направления должна сразу повлиять на угловую скорость в Update
        }

        // --- Логика непрерывного движения и вращения ---
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Создаем запрос для всех активных танков с физическим телом
            var query = EntityQueryEnumerator<ShiftTankHullComponent, PhysicsComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var tankComp, out var physicsComp, out var xform))
            {
                // Пропускаем танки на паузе или без физического тела/трансформа
                if (Paused(uid) || xform.MapUid == null)
                {
                    continue;
                }
                // --- Обработка линейного движения ---
                if (tankComp.MoveDirection != 0)
                {
                    if (tankComp.MoveDirection > 0)
                        tankComp.SoftMoveDir += tankComp.AccelSpeed;
                    else
                    {
                        if (tankComp.SoftMoveDir >= 0)
                            tankComp.SoftMoveDir -= tankComp.AccelSpeed * 3f;
                        else
                            tankComp.SoftMoveDir -= tankComp.AccelSpeed * tankComp.BackMoveModifier;
                    }
                }
                else if (tankComp.SoftMoveDir < tankComp.SlowdownSpeed * -1.1f || tankComp.SoftMoveDir > tankComp.SlowdownSpeed * 1.1f)
                {
                    if (tankComp.SoftMoveDir < tankComp.MaxSoftMoveDir * 0.9f && tankComp.SoftMoveDir > 0)
                    {
                        tankComp.SoftMoveDir -= tankComp.SlowdownSpeed;
                        tankComp.SoftMoveDir -= tankComp.SlowdownSpeed;
                    }
                    if (tankComp.SoftMoveDir < tankComp.MaxSoftMoveDir * 0.6f && tankComp.SoftMoveDir > 0)
                    {
                        tankComp.SoftMoveDir -= tankComp.SlowdownSpeed * 20f;
                    }
                    if (tankComp.SoftMoveDir > 0)
                        tankComp.SoftMoveDir -= tankComp.SlowdownSpeed;
                    else tankComp.SoftMoveDir += tankComp.SlowdownSpeed * 3f;
                }

                var baseForwardVector = -Vector2.UnitY;
                var currentRotation = xform.LocalRotation;
                var actualForwardVector = currentRotation.RotateVec(baseForwardVector);
                tankComp.SoftMoveDir = Math.Clamp(tankComp.SoftMoveDir, tankComp.MinSoftMoveDir, tankComp.MaxSoftMoveDir);
                var targetVelocity = actualForwardVector * tankComp.SoftMoveDir;
                if (tankComp.SoftMoveDir < tankComp.SlowdownSpeed * -2f || tankComp.SoftMoveDir > tankComp.SlowdownSpeed * 2f)
                {
                    if (tankComp.MoveDirection > 0)
                        _physics.SetLinearVelocity(uid, targetVelocity, body: physicsComp);
                    else
                        _physics.SetLinearVelocity(uid, targetVelocity * tankComp.BackMoveModifier, body: physicsComp);
                }

                var coords = xform.Coordinates;

                // --- Обработка вращения ---
                if (tankComp.IsRotating)
                {
                    if (tankComp.NeedMoveForRotating && tankComp.SoftMoveDir > tankComp.SlowdownSpeed * -1.5f && tankComp.SoftMoveDir < tankComp.SlowdownSpeed * 1.5f)
                        continue;
                    // Устанавливаем угловую скорость
                    if (tankComp.SoftMoveDir >= 0)
                        _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(uid, xform, MetaData(uid)), coords, Transform(uid).LocalRotation - tankComp.RotationDirection * tankComp.TurnRate);
                    else
                        _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(uid, xform, MetaData(uid)), coords, Transform(uid).LocalRotation + tankComp.RotationDirection * tankComp.TurnRate);
                }
                else
                {
                }
            }
        }
    }
}
