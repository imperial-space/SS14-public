using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Shared.Siege.Events;
using Content.Server.Prayer;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.ShiftFront;
using Content.Shared.Projectiles;
using Content.Shared.Movement.Systems;
using System.Linq;
using System.Collections.Generic;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Mind;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Server.Ghost;
using Content.Shared.FPV;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.SSDIndicator;
using Content.Server.Jittering;
using Content.Shared.Random.Helpers;
using Robust.Shared.Physics.Events;
using Content.Server.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Enums;
using Content.Server.Spawners.Components;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftFront2System : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly SharedContentEyeSystem _eye = default!;
        [Dependency] private readonly MindSystem _minds = default!;
        [Dependency] private readonly GhostSystem _ghost = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly JitteringSystem _jitter = default!;
        [Dependency] private readonly StunSystem _stun = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
        public EntityUid GetResourceConsole(EntityUid uid, ShiftConsoleBuildComponent comp)
        {
            var buildquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
            while (buildquery.MoveNext(out var resuid, out var rescomp))
            {
                if (rescomp.Faction == comp.Faction)
                    return resuid;
            }
            return uid;
        }

        public bool TryWasteResource(ShiftConsoleResourceComponent comp, int Polymer, int BioShlak, int NanoCarbon, ICommonSession session)
        {
            if (comp.Polymer >= Polymer && comp.BioShlak >= BioShlak && comp.NanoCarbon >= NanoCarbon)
            {
                comp.Polymer -= Polymer;
                comp.BioShlak -= BioShlak;
                comp.NanoCarbon -= NanoCarbon;
                _prayerSystem.SendSubtleMessage(session, session, $"Было потрачено {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона. Не допустите передвижения маячка во время строительство, чтобы оно завершилось успешно", "Строительство запущено");
                return true;
            }
            _prayerSystem.SendSubtleMessage(session, session, $"Для этой постройки необходимо {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона", "Недостаточно ресурсов");
            return false;
        }

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

        public bool CheckForStructures(EntityUid center)
        {
            var xform = Transform(center);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 1f))
            {
                if (HasComp<OccluderComponent>(target) || HasComp<ShiftStructureComponent>(target))
                    return false;
            }
            return true;
        }

        public static Vector2 CalculateTabletIconPosition(
            Vector2 entityCoords,         // Координаты сущности на карте
            Vector2 tabletCoords,         // Координаты планшета на карте
            Vector2 tabletSize,           // Размеры планшета (X, Y)
            Vector2 mapOffset,            // Смещение карты относительно центра (X, Y)
            Vector2 mapSize)              // Общий размер карты (X, Y)
        {
            // Вычисляем относительное положение сущности от центра карты

            Vector2 relativeEntityPos = entityCoords - mapOffset;
            //Logger.Debug($"RelativeEntityPost: {relativeEntityPos}");

            float normx = tabletSize.X / mapSize.X;
            float normy = tabletSize.Y / mapSize.Y;

            Vector2 relativeEntityPosnorm = new Vector2(
                relativeEntityPos.X * normx,
                relativeEntityPos.Y * normy);
            //Logger.Debug($"relativeEntityPosnorm: {relativeEntityPosnorm}");

            Vector2 ontabletPosition = relativeEntityPosnorm + tabletCoords;
            //Logger.Debug($"ontabletPosition: {ontabletPosition}");

            return ontabletPosition;
        }

        public void EndBuild(EntityUid uid, ShiftConsoleBuildComponent comp, bool cansel)
        {
            EnsureComp<SpeechComponent>(uid);
            if (cansel && comp.IsBuilding)
                _chat.TrySendInGameICMessage(uid, "Строительный маячок был сдвинут, постройка отменена, ресурсы не будут возвращены", InGameICChatType.Speak, false);
            if (!cansel && comp.IsBuilding)
                _chat.TrySendInGameICMessage(uid, "Постройка успешно завершена", InGameICChatType.Speak, false);
            comp.BuildingCode = "";
            comp.BuildingLight = null;
            comp.BuildingType = "";
            comp.CurrentBuildTimer = 0;
            comp.IsBuilding = false;
            comp.FutureTimer = 0;
        }

        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + TimeSpan.FromSeconds(1f);

                var buildquery = EntityQueryEnumerator<ShiftConsoleBuildComponent>();
                while (buildquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.CurrentBuildTimer > 0 && comp.IsBuilding)
                        comp.CurrentBuildTimer -= 1;
                    if (comp.CurrentBuildTimer == 0 && comp.IsBuilding)
                    {
                        if (!comp.BuildingLight.HasValue) continue;
                        var xform = Transform(comp.BuildingLight.Value);
                        var coords = xform.Coordinates;
                        QueueDel(comp.BuildingLight);
                        switch (comp.BuildingType)
                        {
                            case "казарма":
                                Spawn("ShiftFrontBarracks" + comp.Faction, coords);
                                break;
                            case "турель":
                                Spawn("WeaponTurretShiftFront" + comp.Faction, coords);
                                break;
                            case "вышка":
                                Spawn("ShiftFrontMedTower" + comp.Faction, coords);
                                break;
                            case "припасы":
                                Spawn("ShiftFrontSupplies" + comp.Faction, coords);
                                break;
                            case "ПВО система 'Купол'":
                                Spawn("ShiftFrontAntiAir" + comp.Faction + "V", coords);
                                break;
                            case "ремстанция":
                                Spawn("ShiftFrontMedTower" + comp.Faction + "V", coords);
                                break;
                            case "экстрактор":
                                foreach (var target in _lookup.GetEntitiesInRange(coords, 1f))
                                {
                                    if (TryComp<ShiftResPointComponent>(target, out var res))
                                    {
                                        var exform = Transform(target);
                                        var ecoords = exform.Coordinates;
                                        var ext = Spawn("ShiftFrontExtractor" + comp.Faction, ecoords);
                                        var extcomp = EnsureComp<ShiftExtractorComponent>(ext);
                                        _chat.DispatchGlobalAnnouncement($"Фракцией {comp.Faction} размещен экстрактор на {res.Type}", playSound: true, colorOverride: Color.Green, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/ex_buid.ogg"));
                                        extcomp.Type = res.Type;
                                        extcomp.Amount = res.Amount;
                                    }
                                }
                                break;
                            case "конвертер":
                                Spawn("ShiftConverter" + comp.Faction, coords);
                                break;
                            case "мортира":
                                Spawn("ShiftFrontMortar" + comp.Faction, coords);
                                break;
                            case "миномет":
                                Spawn("ShiftFrontMm" + comp.Faction, coords);
                                break;
                            case "лаборатория":
                                Spawn("ShiftFrontScience" + comp.Faction, coords);
                                break;
                            case "фабрикатор дронов":
                                Spawn("ShiftFrontDrone" + comp.Faction, coords);
                                break;
                            case "станция РЭБ":
                                Spawn("ShiftFrontREB" + comp.Faction, coords);
                                break;
                            case "хранилище":
                                Spawn("ShiftFrontStorage" + comp.Faction, coords);
                                break;
                            case "мина":
                                Spawn("AntitankLandMineExplosive", coords);
                                break;
                            case "МРАП":
                                Spawn("ShiftFrontMRAP" + comp.Faction, coords);
                                break;
                            case "МРАПМ":
                                Spawn("ShiftFrontMRAPM" + comp.Faction, coords);
                                break;
                            case "МРАП ПТУР":
                                Spawn("ShiftFrontMRAPPTUR" + comp.Faction, coords);
                                break;
                            case "МТЛБ":
                                Spawn("ShiftFrontMTLB" + comp.Faction, coords);
                                break;
                            case "МТЛБМ":
                                Spawn("ShiftFrontMTLBM" + comp.Faction, coords);
                                break;
                            case "БМП":
                                Spawn("ShiftFrontBMP" + comp.Faction, coords);
                                break;
                            case "БТР":
                                Spawn("ShiftFrontBTR" + comp.Faction, coords);
                                break;
                            case "танк":
                                Spawn("ShiftFrontTank" + comp.Faction, coords);
                                break;
                        }
                        EndBuild(uid, comp, false);
                        comp.IsBuilding = false;
                    }
                }

            }

        }
    }
}
