using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Server.GameObjects;
using Content.Shared.Imperial.Zlevels;
using Robust.Shared.Utility;
using Content.Server.MedievalDungeon.Components;
using Content.Shared.Interaction;
using Content.Server.Imperial.Zlevels;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.MedievalDungeon
{
    public sealed partial class DungeonSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly Laddersystem _ladder = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalDungeonKeyComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<MedievalDungeonExitComponent, StartCollideEvent>(OnCollide);

        }

        private void OnCollide(EntityUid uid, MedievalDungeonExitComponent component, ref StartCollideEvent args)
        {
            var human = args.OtherEntity;
            if (!TryComp<TransformComponent>(component.DungeonExit, out var pointTransform)) // проверка на то, есть ли выход
                return;
            if (TryComp<LadderComponent>(uid, out var ladder) && ladder.LadderDoorState != LadderDoorState.Opened) // проверка на то, что люк открыт
                return;
            _transformSystem.SetCoordinates(human, pointTransform.Coordinates);
            _transformSystem.AttachToGridOrMap(human);
        }
        public void OnUseInHand(EntityUid uid, MedievalDungeonKeyComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used); // открытие данжа
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used)
        {
            if (target == null)
                return;
            if (TryComp<MedievalDungeonSpawnComponent>(target, out var dungeon) && dungeon != null)
            {
                var ladder = EnsureComp<LadderComponent>(dungeon.Owner);
                if (ladder.Enabled)
                    return;
                _audio.PlayPvs(new SoundPathSpecifier(dungeon.EffectSoundOnOpen), dungeon.Owner);
                ladder.LadderID = "1dungeonFloor" + dungeon.DungeonGroup;
                ladder.Enabled = true;
                LoadDungeonFloors(dungeon.Owner, dungeon);
                _ladder.ChangeStateDoor(ladder, LadderDoorState.Opened, null, true);
                ladder.CanClosed = false;
                QueueDel(used);

                foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                {
                    barrier.OpenedDungeons++;
                    if (barrier.FirstDungeonVisiter == "nobody")
                        barrier.FirstDungeonVisiter = Name(user);
                }
            }
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);


        }
        private void LoadDungeonFloors(EntityUid uid, MedievalDungeonSpawnComponent component)
        {
            int cnt = 0; // грузим N данжей
            while (cnt < component.DungeonLevels)
            {
                cnt++;
                SpawnMap(uid, component.MedievalDungeon);
                AssignFloor(component, cnt); // расставляем переходы
            }
        }

        private MapId? SpawnMap(EntityUid uid, ResPath[] mappath)
        {
            var path = _random.Pick(mappath);
            var options = new DeserializationOptions
            {
                InitializeMaps = true,
            };

            _mapLoaderSystem.TryLoadMap(path, out var map, out var _, options);

            return map?.Comp.MapId;
        }

        private void AssignFloor(MedievalDungeonSpawnComponent component, int floor)
        {
            foreach (var marker in EntityManager.EntityQuery<MedievalDungeonEnterMarkerComponent>())
            {
                var xform = Transform(marker.Owner);
                var coords = xform.Coordinates;
                if (marker.Level == "none")
                {
                    if (marker.IsEnter)
                    {
                        var enterladder = Spawn(marker.EnterEntity, coords);
                        marker.Level = $"{floor}dungeonFloor" + component.DungeonGroup;
                        var ladder = EnsureComp<LadderComponent>(enterladder);
                        ladder.LadderID = marker.Level;
                        if (floor == 1)
                        {
                            _ladder.ChangeStateDoor(ladder, LadderDoorState.Opened, null, true);
                            ladder.CanClosed = false;
                        }
                    }
                    else
                    {
                        if (floor != component.DungeonLevels)
                        {
                            var exitladder = Spawn(marker.ExitEntity, coords);
                            marker.Level = $"{floor + 1}dungeonFloor" + component.DungeonGroup;
                            var ladder = EnsureComp<LadderComponent>(exitladder);
                            ladder.LadderID = marker.Level;
                        }
                        else
                        {
                            var exitladder = Spawn(marker.ExitEntity, coords);
                            marker.Level = "fuck" + component.DungeonGroup;
                            var ladder = EnsureComp<LadderComponent>(exitladder);
                            var exit = EnsureComp<MedievalDungeonExitComponent>(exitladder);
                            exit.DungeonExit = component.Owner;
                            ladder.LadderID = marker.Level;
                        }

                    }
                }

            }
        }

    }
}
