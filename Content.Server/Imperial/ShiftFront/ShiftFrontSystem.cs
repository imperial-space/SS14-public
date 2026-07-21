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
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftFrontSystem : EntitySystem
    {
        private TimeSpan _lastAFKCheckTime = TimeSpan.Zero;
        private readonly TimeSpan _afkCheckInterval = TimeSpan.FromSeconds(30);
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
            SubscribeLocalEvent<ShiftConsoleBuildComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ShiftCommandComponent, ExaminedEvent>(OnExamineTeam);
            SubscribeLocalEvent<ShiftBuildLightComponent, ExaminedEvent>(OnExamineLight);
            SubscribeLocalEvent<ShiftConsoleResourceComponent, ExaminedEvent>(OnExamineResource);
            SubscribeLocalEvent<ShiftBuildLightComponent, ComponentStartup>(GenerateBuildingCode);
            SubscribeLocalEvent<ShiftConsoleBuildComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftPlayerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetPlayerAlternativeVerbs);
            SubscribeLocalEvent<ShiftBuildLightComponent, MoveEvent>(OnChangeParent);
            SubscribeLocalEvent<ShiftPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<ShiftPlayerComponent, ComponentStartup>(OnPlayerStart);
            SubscribeLocalEvent<ShiftShowOnMapComponent, ComponentStartup>(OnShowOnMapStart);
            SubscribeLocalEvent<ShiftShowOnMapComponent, ComponentShutdown>(OnShowOnMapEnd);
            SubscribeLocalEvent<ShiftBarracksComponent, ComponentStartup>(OnBarracksStart);
            SubscribeLocalEvent<ShiftSuppliesComponent, ComponentStartup>(OnSuppliesStart);
            SubscribeLocalEvent<ShiftStorageComponent, ComponentStartup>(OnStorageStart);
            SubscribeLocalEvent<ShiftAirDefableComponent, ComponentStartup>(OnAirDefableStart);
            SubscribeLocalEvent<ShiftExtractorComponent, DamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<ShiftStructureComponent, ExaminedEvent>(OnExamineStructure);
            SubscribeLocalEvent<ShiftConsoleAnalisComponent, ExaminedEvent>(OnExamineAnalis);
            SubscribeLocalEvent<ShiftResourceExtractComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<ShiftPlayerComponent, CommanderBoostUpEvent>(OnCommanderBoost);
            SubscribeLocalEvent<ShiftPlayerComponent, PlayerDetachedEvent>(OnPlayerDetached);
            SubscribeLocalEvent<ShiftPlayerComponent, PlayerAttachedEvent>(OnPlayerAttached);
            SubscribeLocalEvent<ShiftTeamRespWaitComponent, PlayerDetachedEvent>(OnPlayerDetachedW);
            SubscribeLocalEvent<ShiftWrenchComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<ShiftBuildLightComponent, GetVerbsEvent<AlternativeVerb>>(OnGetBuildVerbs);
            SubscribeLocalEvent<ShiftFrontRequestComponent, ExaminedEvent>(OnExamineRequest);
            SubscribeLocalEvent<ShiftFrontRequestComponent, AfterInteractEvent>(OnPaperUsed);
            SubscribeLocalEvent<ShiftFrontRequestConsoleComponent, MapInitEvent>(OnRequestConsoleInit);
            SubscribeLocalEvent<ShiftFrontGunComponent, GunShotEvent>(OnShoot);
            SubscribeLocalEvent<ShiftShowOnMapComponent, DamageChangedEvent>(OnDamageMap);
            SubscribeLocalEvent<ShiftTankTurretComponent, GunShotEvent>(OnShootTurret);
            SubscribeLocalEvent<ShiftTankHullComponent, DamageChangedEvent>(OnDamageTank);
            SubscribeLocalEvent<ShiftCommandComponent, GetVerbsEvent<AlternativeVerb>>(OnGetComVerbs);
            SubscribeLocalEvent<ShiftPlayerComponent, EntityUnpausedEvent>(OnPlayerUnpaused);
            SubscribeLocalEvent<ShiftPlayerComponent, InteractionAttemptEvent>(OnInteractionAttempt);
            SubscribeLocalEvent<ShiftPlayerComponent, AttackAttemptEvent>(OnAttackAttempt);
            SubscribeLocalEvent<ShiftPlayerComponent, SpeakAttemptEvent>(OnSpeakAttempt);
            SubscribeLocalEvent<ShiftPlayerComponent, MoveInputEvent>(OnMoveInput);
        }
        private void OnPlayerUnpaused(EntityUid uid, ShiftPlayerComponent comp, EntityUnpausedEvent args)
        {
            comp.LastActivityTime += args.PausedTime;
        }

        private void OnInteractionAttempt(EntityUid uid, ShiftPlayerComponent comp, InteractionAttemptEvent args)
        {
            UpdateActivity(uid, comp);
        }

        private void OnAttackAttempt(EntityUid uid, ShiftPlayerComponent comp, AttackAttemptEvent args)
        {
            UpdateActivity(uid, comp);
        }

        private void OnSpeakAttempt(EntityUid uid, ShiftPlayerComponent comp, SpeakAttemptEvent args)
        {
            UpdateActivity(uid, comp);
        }

        private void OnMoveInput(EntityUid uid, ShiftPlayerComponent comp, MoveInputEvent args)
        {
            UpdateActivity(uid, comp);
        }

        private void UpdateActivity(EntityUid uid, ShiftPlayerComponent comp)
        {
            comp.LastActivityTime = _timing.CurTime;
            comp.IsAFK = false;


            if (TryGetCommandForPlayer(uid, comp.Faction, out var command))
            {

            }
        }
        private void OnExamineTeam(EntityUid uid, ShiftCommandComponent comp, ExaminedEvent args)
        {
            args.PushMarkup($"Всего игроков: [color=cyan]{comp.Players.Count}[/color]", 11);
            args.PushMarkup($"Ждут возрождение: [color=gray]{comp.RespawnQueue.Count}[/color]", 10);
            if (comp.Players.Count < 25)
                foreach (var player in comp.Players)
                {
                    args.PushMarkup($"[color=cyan]{player.Name}[/color]", 9);
                }
            else
                args.PushMarkup($"Игроков слишком много, чтобы их всех отобразить", 9);
        }

        public int? GetSessionPosition(List<ICommonSession> sessions, ICommonSession targetSession)
        {
            int index = sessions.IndexOf(targetSession);
            return index >= 0 ? index : null;
        }

        private void OnGetComVerbs(EntityUid uid, ShiftCommandComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract)
                return;

            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session))
                return;

            // Проверяем, состоит ли игрок уже в какой-то команде
            if (!TryComp<ActorComponent>(ev.User, out var actor)) return;
            if (!_minds.TryGetMind(actor.PlayerSession, out var mindId, out var _)) return;
            if (actor.PlayerSession.AttachedEntity is null) return;

            var isInAnyTeam = false;

            if (isInAnyTeam)
                Logger.Debug($"Ис ин эни тим труе");

            Logger.Debug(mindId.ToString());

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {

                    LeaveCurrentTeam(session);
                    JoinCommand(uid, comp, actor.PlayerSession);
                },
                Text = isInAnyTeam ? "Сменить команду" : "Выбрать эту команду",
                Priority = 10,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "beacon_code")
            });

            // Добавляем отдельный глагол для выхода из команды
            //if (isInAnyTeam)
            //{
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {

                    LeaveCurrentTeam(session);

                },
                Text = "Покинуть команду",
                Priority = 5,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "beacon_code")
            });
            //}
        }

        private void LeaveCurrentTeam(ICommonSession session)
        {
            Logger.Debug($"Запущен метод о ливе текущей команды");

            var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
            while (dquery.MoveNext(out var couid, out var command))
            {
                command.Players.Remove(session);
                if (command.RespawnQueue.Contains(session))
                    command.RespawnQueue.Remove(session);
            }
        }

        private void JoinCommand(EntityUid uid, ShiftCommandComponent comp, ICommonSession session)
        {

            if (session.AttachedEntity is null) return;
            var user = session.AttachedEntity.Value;

            Logger.Debug("Начали попытку джоина в команду");
            // Проверяем, что у игрока есть Mind (сознание)
            if (!TryComp<MindContainerComponent>(user, out var mindContainer) || mindContainer.Mind == null)
                return;

            var mind = mindContainer.Mind.Value;

            // Если игрок уже в этой команде - выходим
            if (comp.Players.Contains(session))
                return;

            // Получаем все команды (ShiftCommandComponent) на сервере
            var allCommands = EntityQuery<ShiftCommandComponent>().ToList();

            // Находим минимальное количество игроков в командах (кроме текущей)
            var otherTeamsPlayerCounts = allCommands
                .Where(c => c.Faction != comp.Faction)
                .Select(c => c.Players.Count)
                .ToList();

            // Если есть другие команды, проверяем баланс
            if (otherTeamsPlayerCounts.Any())
            {
                var minPlayersInOtherTeams = otherTeamsPlayerCounts.Min();
                var currentTeamCount = comp.Players.Count;

                // Если в текущей команде уже больше игроков, чем минимальное в других - запрещаем вступление
                if (currentTeamCount > minPlayersInOtherTeams)
                {
                    _chat.TrySendInGameICMessage(user, $"Невозможно присоединиться: перевес в команде {comp.Faction}!", InGameICChatType.Speak, false);
                    Logger.Debug("Дизбаланс наху");
                    return;
                }
            }
            if (!comp.RespawnQueue.Contains(session))
                comp.RespawnQueue.Add(session);
            comp.Players.Add(session);

            // Уведомление
            _chat.TrySendInGameICMessage(user, $"Вы присоединились к команде {comp.Faction}!", InGameICChatType.Speak, false);
            EnsureComp<TimedDespawnComponent>(user, out var despawn);

            despawn.Lifetime = 0.5f;

            // Формируем строку с раскладом сил
            var teamInfo = string.Join(", ", allCommands.Select(t => $"{t.Faction} - {t.Players.Count}"));

            _chat.DispatchGlobalAnnouncement(
                $"Игрок {session.Name} присоединился к команде {comp.Faction}. Расклад сил: {teamInfo}",
                playSound: false,
                colorOverride: Color.Green,
                sender: "Орбитальное наблюдение",
                announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/ex_buid.ogg")
            );
        }



        private void OnDamageTank(EntityUid uid, ShiftTankHullComponent comp, DamageChangedEvent args)
        {
            if (comp.InsideEntryEntity == null) return;
            if (args.DamageDelta == null) return;
            if (args.DamageDelta.GetTotal() <= 0f) return;
            if (args.DamageDelta.GetTotal() < 110)
                Audio.PlayPvs(comp.SoundHit, comp.InsideEntryEntity.Value);
            else
            {
                Audio.PlayPvs(comp.SoundHitLarge, comp.InsideEntryEntity.Value);
                Spawn("MedievalExplodeApNew", Transform(comp.InsideEntryEntity.Value).Coordinates);
            }
        }
        private void OnShootTurret(EntityUid uid, ShiftTankTurretComponent comp, GunShotEvent args)
        {
            if (comp.LinkedTank == null) return;
            if (!TryComp<ShiftTankHullComponent>(comp.LinkedTank, out var hull)) return;
            if (hull.InsideGunnerEntity == null) return;
            if (!TryComp<GunComponent>(uid, out var gun)) return;
            Audio.PlayPvs(gun.SoundGunshot, hull.InsideGunnerEntity.Value);
        }
        private void OnDamageMap(EntityUid uid, ShiftShowOnMapComponent comp, DamageChangedEvent args)
        {
            if (!uid.IsValid() || !Exists(uid) || HasComp<TransformComponent>(uid))
                return;


            if (TryComp<DamageableComponent>(uid, out var damageable) && args.DamageIncreased && args.DamageDelta != null)
            {
                if (damageable.TotalDamage > 1000f)
                    QueueDel(uid);
                if (damageable.TotalDamage > 100f && !HasComp<ShiftStructureComponent>(uid))
                {
                    if (args.DamageDelta.GetTotal() > 0f)
                        foreach (var entity in comp.LinkedMipples)
                        {
                            Spawn("ShiftFrontMapSuffEffect", Transform(entity).Coordinates);
                        }
                }
                else
                {
                    if (args.DamageDelta.GetTotal() > 5f)
                        foreach (var entity in comp.LinkedMipples)
                        {
                            Spawn("ShiftFrontMapDamageEffect", Transform(entity).Coordinates);
                        }
                }

            }
        }
        private void OnShoot(EntityUid uid, ShiftFrontGunComponent comp, GunShotEvent args)
        {
            if (TryComp<ShiftShowOnMapComponent>(args.User, out var showComp))
            {
                foreach (var entity in showComp.LinkedMipples)
                {
                    Spawn("ShiftFrontMapShootEffect", Transform(entity).Coordinates);
                }
            }
            if (TryComp<ShiftShowOnMapComponent>(uid, out var showComp2))
            {
                foreach (var entity2 in showComp2.LinkedMipples)
                {
                    Spawn("ShiftFrontMapShootEffect", Transform(entity2).Coordinates);
                }
            }

        }
        private void OnRequestConsoleInit(EntityUid uid, ShiftFrontRequestConsoleComponent comp, MapInitEvent args)
        {
            // Устанавливаем фракцию консоли при инициализации
            if (TryComp<ShiftStructureComponent>(uid, out var structure))
            {
                comp.Faction = structure.Faction;
            }
        }

        private void OnGetBuildVerbs(EntityUid uid, ShiftBuildLightComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract)
                return;

            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session))
                return;

            if (!TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer))
                return;

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => RequestConstruction(comp.BuildingCode, ev.User, session),
                Text = "Запросить строительство",
                Priority = 10,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "beacon_code")
            });
        }

        private void RequestConstruction(string lightUid, EntityUid requesterUid, ICommonSession session)
        {
            if (!TryComp<ShiftPlayerComponent>(requesterUid, out var playerComp))
                return;

            _quickDialog.OpenDialog(session, "Тип постройки", "Выберите тип постройки:", (string buildingType) =>
            {
                CreateRequest(requesterUid, buildingType, lightUid, playerComp.Faction);
            });
        }


        private void CreateRequest(EntityUid requesterUid, string buildingType, string lightUid, string faction)
        {
            var query = EntityQueryEnumerator<ShiftFrontRequestConsoleComponent>();
            while (query.MoveNext(out var consoleUid, out var consoleComp))
            {
                // Проверяем совпадение фракций
                if (consoleComp.Faction != faction)
                    continue;

                var paper = Spawn("ShiftFrontPaper", _transform.GetMapCoordinates(consoleUid));
                var requestComp = EnsureComp<ShiftFrontRequestComponent>(paper);

                requestComp.RequesterName = MetaData(requesterUid).EntityName;
                requestComp.RequestTime = _timing.CurTime;
                requestComp.BuildingTypeId = buildingType;
                requestComp.RequesterUid = lightUid;
                requestComp.Faction = faction;
                _chat.TrySendInGameICMessage(consoleUid, $"Пришел запрос на {buildingType} от {requestComp.RequesterName}", InGameICChatType.Speak, false);


                _metaData.SetEntityName(paper, $"[{faction}] Запрос: {buildingType}");
                consoleComp.ActiveRequests.Add(paper);

            }
        }
        private void OnExamineRequest(EntityUid uid, ShiftFrontRequestComponent comp, ExaminedEvent args)
        {
            var timePassed = _timing.CurTime - comp.RequestTime;
            string timeText = timePassed.TotalMinutes >= 1
                ? $"{(int)timePassed.TotalMinutes} минут назад"
                : $"{(int)timePassed.TotalSeconds} секунд назад";

            args.PushMarkup($"Фракция: {comp.Faction}");
            args.PushMarkup($"Запрос: [color=yellow]{comp.BuildingTypeId}[/color]");
            args.PushMarkup($"Код маячка: [color=green]{comp.RequesterUid}[/color]");
            args.PushMarkup($"Солдат: [color=cyan]{comp.RequesterName}[/color]");
            args.PushMarkup($"Время: [color=gray]{timeText}[/color]");
        }

        private void OnPaperUsed(EntityUid uid, ShiftFrontRequestComponent comp, AfterInteractEvent args)
        {
            if (args.Target == null || !HasComp<ShiftFrontTrashComponent>(args.Target))
                return;
            QueueDel(uid);

        }


        private void OnExamineAnalis(EntityUid uid, ShiftConsoleAnalisComponent comp, ExaminedEvent args)
        {
            if (!comp.Soljers)
            {
                int Turret = 0;
                int MedTower = 0;
                int AntiAir = 0;
                int Supplies = 0;
                int Barracks = 0;
                int Extractor = 0;
                int Converter = 0;
                int DroneFactory = 0;
                int Storage = 0;
                int Science = 0;
                int REB = 0;
                var redquery = EntityQueryEnumerator<ShiftStructureComponent>();
                while (redquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != comp.Faction) continue;
                    switch (recomp.Structure)
                    {
                        case "Turret":
                            Turret++;
                            break;
                        case "MedTower":
                            MedTower++;
                            break;
                        case "AntiAir":
                            AntiAir++;
                            break;
                        case "Supplies":
                            Supplies++;
                            break;
                        case "Barracks":
                            Barracks++;
                            break;
                        case "Extractor":
                            Extractor++;
                            break;
                        case "Converter":
                            Converter++;
                            break;
                        case "DroneFactory":
                            DroneFactory++;
                            break;
                        case "Storage":
                            Storage++;
                            break;
                        case "Science":
                            Science++;
                            break;
                        case "REB":
                            Science++;
                            break;
                    }
                }
                args.PushMarkup($"Текущие постройки:", 10);
                if (Turret > 0) args.PushMarkup($"  Турели: [color=green]{Turret}[/color]", 8);
                if (MedTower > 0) args.PushMarkup($"  Мед. башни: [color=green]{MedTower}[/color]", 8);
                if (AntiAir > 0) args.PushMarkup($"  ПВО системы: [color=green]{AntiAir}[/color]", 8);
                if (Storage > 0) args.PushMarkup($"  Хранилища: [color=green]{Storage}[/color]", 8);
                if (Supplies > 0) args.PushMarkup($"  Заводы: [color=green]{Supplies}[/color]", 7);
                if (Barracks > 0) args.PushMarkup($"  Клон-станции: [color=green]{Barracks}[/color]", 9);
                if (Extractor > 0) args.PushMarkup($"  Экстракторы: [color=green]{Extractor}[/color]", 9);
                if (Converter > 0) args.PushMarkup($"  Конвертеры: [color=green]{Converter}[/color]", 7);
                if (DroneFactory > 0) args.PushMarkup($"  Фабрики дронов: [color=green]{DroneFactory}[/color]", 7);
                if (Science > 0) args.PushMarkup($"  Лаборатории: [color=green]{Science}[/color]", 7);
                if (REB > 0) args.PushMarkup($"  Станции РЭБ: [color=green]{REB}[/color]", 7);
            }
            else
            {
                int News = 0;
                int Scouts = 0;
                int Assault = 0;
                int Med = 0;
                int Eng = 0;
                int Sniper = 0;
                int HMG = 0;
                int Assasin = 0;
                int NewsAlive = 0;
                int ScoutsAlive = 0;
                int AssaultAlive = 0;
                int MedAlive = 0;
                int EngAlive = 0;
                int SniperAlive = 0;
                int HMGAlive = 0;
                int AssasinAlive = 0;

                var pquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (pquery.MoveNext(out var puid, out var pcomp))
                {
                    if (pcomp.Faction != comp.Faction) continue;
                    switch (pcomp.Name)
                    {
                        case "Scout":
                            Scouts++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd0) && !ssd0.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) ScoutsAlive++;
                            break;
                        case "New":
                            News++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd) && !ssd.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) NewsAlive++;
                            break;
                        case "Assault":
                            Assault++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd2) && !ssd2.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) AssaultAlive++;
                            break;
                        case "Med":
                            Med++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd3) && !ssd3.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) MedAlive++;
                            break;
                        case "Eng":
                            Eng++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd4) && !ssd4.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) EngAlive++;
                            break;
                        case "Sniper":
                            Sniper++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd5) && !ssd5.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) SniperAlive++;
                            break;
                        case "HMG":
                            HMG++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd6) && !ssd6.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) HMGAlive++;
                            break;
                        case "Assasin":
                            Assasin++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd7) && !ssd7.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) AssasinAlive++;
                            break;
                    }
                }
                args.PushMarkup($"Текущие войска:", 10);
                if (News > 0) args.PushMarkup($"  Рекруты: [color=yellow]{News}[/color], активно [color=green]{ScoutsAlive}[/color]", 9);
                if (Scouts > 0) args.PushMarkup($"  Скауты: [color=yellow]{Scouts}[/color], активно [color=green]{ScoutsAlive}[/color]", 9);
                if (Med > 0) args.PushMarkup($"  Медики: [color=yellow]{Med}[/color], активно [color=green]{MedAlive}[/color]", 7);
                if (Eng > 0) args.PushMarkup($"  Инженеры: [color=yellow]{Eng}[/color], активно [color=green]{EngAlive}[/color]", 7);
                if (Assault > 0) args.PushMarkup($"  Штурмовики: [color=yellow]{Assault}[/color], активно [color=green]{AssaultAlive}[/color]", 8);
                if (Sniper > 0) args.PushMarkup($"  Снайперы: [color=yellow]{Sniper}[/color], активно [color=green]{SniperAlive}[/color]", 6);
                if (HMG > 0) args.PushMarkup($"  Пулеметчики: [color=yellow]{HMG}[/color], активно [color=green]{HMGAlive}[/color]", 5);
                if (Assasin > 0) args.PushMarkup($"  Ассасины: [color=yellow]{Assasin}[/color], активно [color=green]{AssasinAlive}[/color]", 4);
            }
        }
        private void OnExamineStructure(EntityUid uid, ShiftStructureComponent comp, ExaminedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > 5)
                args.PushMarkup($"У этой постройки [color=red]{Math.Round(dam.TotalDamage.Float(), 2)}[/color] единиц  повреждений, бейте красным гаечным ключом для ремонта", 5);
        }

        private void OnMeleeHit(EntityUid uid, ShiftWrenchComponent comp, MeleeHitEvent args)
        {
            foreach (EntityUid hitEntity in args.HitEntities)
            {
                if (TryComp<ShiftStructureComponent>(hitEntity, out var hitEntityComp))
                    _damageableSystem.TryChangeDamage(hitEntity, -comp.RegenDamage, true);
            }
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
        public void OnCommanderBoost(EntityUid uid, ShiftPlayerComponent comp, CommanderBoostUpEvent args)
        {
            args.Handled = true;
            _chat.TrySendInGameICMessage(uid, "ДЕРЖАТЬ ПОЗИЦИЮ!!", InGameICChatType.Speak, false);
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 7f))
            {
                if (TryComp<ShiftPlayerComponent>(target, out var player) && player.Faction == comp.Faction)
                {
                    if (player.Suppression < player.SuppressionMax)
                    {
                        player.Suppression = player.SuppressionMax;
                        float zoom = 1f * (player.Suppression / 100f);
                        zoom = Math.Clamp(zoom, 0.4f, 1f);
                        player.Suppression = Math.Clamp(player.Suppression, player.SuppressionMin, player.SuppressionMax);
                        _eye.SetZoom(target, new Vector2(zoom, zoom));
                        _eye.SetMaxZoom(target, new Vector2(zoom, zoom));
                    }
                }
            }
        }
        public void OnAirDefableStart(EntityUid uid, ShiftAirDefableComponent comp, ComponentStartup args)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, comp.Radius))
            {
                if (TryComp<ShiftAntiAirComponent>(target, out var aa))
                {
                    var desp = EnsureComp<SpawnOnDespawnComponent>(uid);
                    desp.Prototype = "AntiAirDropEffect";
                    return;
                }
            }

        }
        public void OnStorageStart(EntityUid uid, ShiftStorageComponent comp, ComponentStartup args)
        {
            var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
            while (redquery.MoveNext(out var reuid, out var recomp))
            {
                if (recomp.Faction != comp.Faction) continue;
                recomp.PolymerLimit += comp.PolymerBonus;
                recomp.BioShlakLimit += comp.BioShlakBonus;
                recomp.NanoCarbonLimit += comp.NanoCarbonBonus;
            }
        }

        public void OnSuppliesStart(EntityUid uid, ShiftSuppliesComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontFactorySpeedUp", comp.Faction))
                comp.OverallGenTime -= 10;
            if (CheckResearch("ShiftFrontFactorySpeedUp2", comp.Faction))
                comp.OverallGenTime -= 15;
        }
        public void OnBarracksStart(EntityUid uid, ShiftBarracksComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontClonSpeedUp", comp.Faction))
                comp.Boost += 7;
            if (CheckResearch("ShiftFrontClonSpeedUp2", comp.Faction))
                comp.Boost += 7;
        }

        public void OnPlayerStart(EntityUid uid, ShiftPlayerComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontPsycho", comp.Faction))
                comp.SuppressionMax += 10;
            if (CheckResearch("ShiftFrontPsycho2", comp.Faction))
                comp.SuppressionMax += 15;
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            if (!comp.Newbie)
            {
                if (CheckResearch("ShiftFrontBack1", comp.Faction))
                    Spawn("BackPackTier1", coords);
                else if (CheckResearch("ShiftFrontBack2", comp.Faction))
                    Spawn("BackPackTier2", coords);
                else if (CheckResearch("ShiftFrontBack3", comp.Faction))
                    Spawn("BackPackTier3", coords);
                else if (CheckResearch("ShiftFrontBack4", comp.Faction))
                    Spawn("BackPackTier4", coords);
                else if (CheckResearch("ShiftFrontBack5", comp.Faction))
                    Spawn("BackPackTier5", coords);
            }


            if (comp.Ninja && HasComp<StatusIconComponent>(uid)) RemComp<StatusIconComponent>(uid);

        }
        public void OnShowOnMapEnd(EntityUid uid, ShiftShowOnMapComponent comp, ComponentShutdown args)
        {
            foreach (var mipple in comp.LinkedMipples)
            {
                if (!mipple.IsValid() || !Exists(mipple))
                    continue;
                if (comp.DeathEffectProto != "") Spawn(comp.DeathEffectProto, Transform(mipple).Coordinates);
                EnsureComp<TimedDespawnComponent>(mipple, out var despawn);
                despawn.Lifetime = 0.05f;
            }
        }

        public void OnShowOnMapStart(EntityUid uid, ShiftShowOnMapComponent comp, ComponentStartup args)
        {
            foreach (var mipple in comp.LinkedMipples)
            {
                if (!mipple.IsValid() || !Exists(mipple))
                    continue;
                EnsureComp<TimedDespawnComponent>(mipple, out var despawn);
                despawn.Lifetime = 0.05f;
            }
            comp.LinkedMipples.Clear();

            if (comp.MippleProto != "")
            {
                var dquery = EntityQueryEnumerator<ShiftMapComponent>();
                while (dquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != comp.Faction && comp.Faction != "" && recomp.Faction != "") continue;
                    var LinkedMipple = Spawn(comp.MippleProto, Transform(reuid).Coordinates);
                    _metaData.SetEntityName(LinkedMipple, EnsureComp<MetaDataComponent>(uid).EntityName);
                    EnsureComp<ShiftMippleComponent>(LinkedMipple, out var mipplecomp);
                    mipplecomp.LinkedMap = reuid;
                    mipplecomp.LinkedPlayer = uid;
                    var nc = CalculateTabletIconPosition(new Vector2(Transform(uid).Coordinates.X, Transform(uid).Coordinates.Y),
                        new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                        new Vector2(recomp.entX, recomp.entY),
                        new Vector2(recomp.offsetX, recomp.offsetY),
                        new Vector2(recomp.mapX, recomp.mapY));
                    _transform.SetWorldPosition(LinkedMipple, nc);
                    comp.LinkedMipples.Add(LinkedMipple);
                }
            }
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
        public void OnUseInHand(EntityUid uid, ShiftResourceExtractComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, ShiftResourceExtractComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<ShiftExtractorComponent>(target, out var ext))
            {
                Audio.PlayPvs(new SoundPathSpecifier(ext.EffectSoundOnConsume), ext.Owner);
                var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
                while (redquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != ext.Faction) continue;
                    recomp.Polymer += comp.Polymer;
                    recomp.BioShlak += comp.BioShlak;
                    recomp.NanoCarbon += comp.NanoCarbon;
                    _chat.TrySendInGameICMessage(reuid, $"Ядро клона принесло {comp.Polymer} полимера и {comp.BioShlak} биошлака", InGameICChatType.Speak, false);
                }
                QueueDel(used);
            }
        }

        private void OnGetPlayerAlternativeVerbs(EntityUid uid, ShiftPlayerComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (!TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) || !shiftPlayer.Leader) return;
            if (ev.Target == ev.User) return;
            if (comp.Faction == shiftPlayer.Faction)
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!_minds.TryGetMind(uid, out var mindId, out var mind) || mind.IsVisitingEntity)
                            return;
                        _ghost.OnGhostAttempt(mindId, false, mind: mind);
                    },
                    Text = "Выгнать с роли",
                    Priority = 15,
                });
            }
        }

        private void OnMobStateChanged(Entity<ShiftPlayerComponent> ent, ref MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Critical && ent.Comp.Leader)
                _chat.DispatchGlobalAnnouncement($"Командир фракции {ent.Comp.Faction} был введен в критическое состояние", playSound: true, colorOverride: Color.Red, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/lead_crit.ogg"));
            if (args.NewMobState != MobState.Dead)
                return;
            if (ent.Comp.Leader)
            {
                _chat.DispatchGlobalAnnouncement($"Командир фракции {ent.Comp.Faction} был ликвидирован", playSound: true, colorOverride: Color.DeepPink, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/lead_dead.ogg"));
                var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
                while (dquery.MoveNext(out var couid, out var command))
                {
                    if (ent.Comp.Faction == command.Faction)
                    {
                        EnsureComp<TimedDespawnComponent>(couid, out var despawn);
                        despawn.Lifetime = 0.3f;
                    }
                }
            }
            var xform = Transform(ent);
            var coords = xform.Coordinates;
            QueueDel(ent);
            if (!ent.Comp.Newbie)
                Spawn("AnomalyCoreFleshShiftFront", coords);
        }
        // Обновляем существующие методы
        private void OnPlayerDetached(Entity<ShiftPlayerComponent> ent, ref PlayerDetachedEvent args)
        {
            var session = args.Player;
            var dquery = EntityQueryEnumerator<ShiftCommandComponent>();

            while (dquery.MoveNext(out var couid, out var command))
            {
                if (ent.Comp.Faction == command.Faction)
                {
                    if (!command.RespawnQueue.Contains(session))
                        command.RespawnQueue.Add(session);

                    // Начинаем отсчет AFK при отключении
                    ent.Comp.LastActivityTime = _timing.CurTime;
                }
            }
        }

        private void OnPlayerAttached(Entity<ShiftPlayerComponent> ent, ref PlayerAttachedEvent args)
        {
            var session = args.Player;
            var dquery = EntityQueryEnumerator<ShiftCommandComponent>();

            while (dquery.MoveNext(out var couid, out var command))
            {
                //if (ent.Comp.Faction == command.Faction)
                //{
                if (command.RespawnQueue.Contains(session))
                    command.RespawnQueue.Remove(session);

                // Сбрасываем AFK при подключении
                UpdateActivity(ent, ent.Comp);
                //}
            }
        }

        private void OnPlayerDetachedW(Entity<ShiftTeamRespWaitComponent> ent, ref PlayerDetachedEvent args)
        {
            var session = args.Player;
            var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
            while (dquery.MoveNext(out var couid, out var command))
            {
                if (ent.Comp.Faction == command.Faction && !command.RespawnQueue.Contains(session))
                    command.RespawnQueue.Add(session);
            }
        }

        private void OnDamage(EntityUid uid, ShiftExtractorComponent comp, DamageChangedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var damageable) && damageable.TotalDamage > 300f && !comp.Digged)
            {
                comp.Digged = true;
                _damageableSystem.TryChangeDamage(uid, comp.Damage, true);
                _chat.DispatchGlobalAnnouncement($"Экстрактор фракции {comp.Faction}, добывающий {comp.Type} был уничтожен", playSound: true, colorOverride: Color.Yellow, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/ex_destroy.ogg"));
            }
        }

        private void OnExamineResource(EntityUid uid, ShiftConsoleResourceComponent comp, ExaminedEvent args)
        {
            var exquery = EntityQueryEnumerator<ShiftExtractorComponent>();
            int incomeP = 0;
            int incomeB = 0;
            int incomeN = 0;

            while (exquery.MoveNext(out var exuid, out var excomp))
            {
                if (excomp.Faction == comp.Faction)
                {
                    switch (excomp.Type)
                    {
                        case "Polymer":
                            incomeP += excomp.Amount;
                            break;
                        case "BioShlak":
                            incomeB += excomp.Amount;
                            break;
                        case "NanoCarbon":
                            incomeN += excomp.Amount;
                            break;
                    }
                }
            }
            args.PushMarkup($"Полимер: [color=yellow]{comp.Polymer}[/color], лимит: [color=red]{comp.PolymerLimit}[/color], ген: [color=orange]{incomeP}/30с[/color]", 5);
            args.PushMarkup($"Био-шлак: [color=green]{comp.BioShlak}[/color], лимит: [color=red]{comp.BioShlakLimit}[/color], ген: [color=orange]{incomeB}/30с[/color]", 4);
            args.PushMarkup($"Нано-карбон: [color=cyan]{comp.NanoCarbon}[/color], лимит: [color=red]{comp.NanoCarbonLimit}[/color], ген: [color=orange]{incomeN}/30с[/color]", 3);
        }

        private void OnChangeParent(EntityUid uid, ShiftBuildLightComponent comp, ref MoveEvent args)
        {
            var newParent = args.NewPosition.EntityId;

            if (!args.ParentChanged)
                return;

            var buildquery = EntityQueryEnumerator<ShiftConsoleBuildComponent>();
            while (buildquery.MoveNext(out var buildUid, out var buildComp))
            {
                if (buildComp.BuildingLight == uid && buildComp.IsBuilding)
                {
                    EndBuild(buildUid, buildComp, true);
                    QueueDel(uid);
                }
            }
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

        public void GenerateBuildingCode(EntityUid uid, ShiftBuildLightComponent comp, ComponentStartup args)
        {
            var query = EntityQueryEnumerator<ShiftBuildLightComponent>();
            string code = _random.Next(100, 999).ToString();
            List<string> was = new();

            while (query.MoveNext(out var otherUid, out var othercomp))
            {
                if (otherUid == uid)
                    continue;
                was.Add(othercomp.BuildingCode);
            }
            int attempts = 30;
            while (was.Contains(code) && attempts > 0)
            {
                attempts--;
                code = _random.Next(100, 999).ToString();
            }
            comp.BuildingCode = code;
        }

        private void OnExamineLight(EntityUid uid, ShiftBuildLightComponent comp, ExaminedEvent args)
        {
            args.PushMarkup($"Код маячка - [color=cyan]{comp.BuildingCode}[/color]");
        }
        private void OnExamine(EntityUid uid, ShiftConsoleBuildComponent comp, ExaminedEvent args)
        {
            if (comp.IsBuilding)
                args.PushMarkup($"До завершения строительства [color=red]{comp.BuildingType}[/color] осталось [color=yellow]{comp.CurrentBuildTimer}[/color] секунд");
            if (comp.BuildingType == "")
                args.PushMarkup("Строительство [color=green]не выбрано[/color]");
            else
                args.PushMarkup($"Строительство выбрано - [color=green]{comp.BuildingType}[/color]");
            if (!string.IsNullOrEmpty(comp.BuildingCode))
                args.PushMarkup($"Код строительного маячка: [color=cyan]{comp.BuildingCode}[/color]");
            //_ui.SetUiState(uid, ShiftConsoleUiKey.Key, ЮИ НЕ РАБОТАЕТ АПСТРИМ ЕБАЛ
            //    new ShiftConsoleResourceBoundUserInterfaceState
            //    {
            //        ResourceName = "абоба",
            //        ResourceCount = 112121//  switch
            //        // {
            //        //     "абоба" => "абоба",
            //        //     "BioShlak" => recomp.BioShlak,
            //        //     "NanoCarbon" => recomp.NanoCarbon,
            //        //     _ => throw new NotImplementedException()
            //        // }
            //    });
            Logger.Info("мы послали абобу на клиент");
        }

        private void OnGetAlternativeVerbs(EntityUid uid, ShiftConsoleBuildComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract)
                return;

            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session))
                return;

            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader)
                return;

            if (comp.CurrentBuildTimer > 0) return;

            // Основные постройки
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "казарма"),
                Text = "Клон-станция",
                Priority = 13,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "barracks")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "турель"),
                Text = "Турель",
                Priority = 12,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "turret")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "вышка"),
                Text = "Мед. вышка",
                Priority = 11,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "healtower")
            });

            // Постройки, требующие исследования
            if (CheckResearch("ShiftFrontSupplies", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "припасы"),
                    Text = "Припасы",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }
            if (CheckResearch("ShiftFrontAntiAir", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "ПВО система 'Купол'"),
                    Text = "ПВО система 'Купол'",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontMedTowerV", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "ремстанция"),
                    Text = "Ремстанция",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "экстрактор"),
                Text = "Экстрактор",
                Priority = 9,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "minerblue")
            });

            if (CheckResearch("ShiftFrontConverter", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "конвертер"),
                    Text = "Конвертер",
                    Priority = 8,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "recycler")
                });
            }

            if (CheckResearch("ShiftFrontMortar", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "мортира"),
                    Text = "Мортира",
                    Priority = 7,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontMm", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "миномет"),
                    Text = "Миномет",
                    Priority = 7,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontScience", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "лаборатория"),
                    Text = "Лаборатория",
                    Priority = 6,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "laboratory")
                });
            }

            if (CheckResearch("ShiftFrontDrone", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "фабрикатор дронов"),
                    Text = "Фабрикатор дронов",
                    Priority = 5,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontREB", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "станция РЭБ"),
                    Text = "Станция РЭБ",
                    Priority = 5,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "rebblue")
                });
            }

            if (CheckResearch("ShiftFrontStorage", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "хранилище"),
                    Text = "Хранилище",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "barracks")
                });
            }

            if (CheckResearch("ShiftFrontATLM", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "мина"),
                    Text = "Противотанковая мина",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/landmine.rsi"), "landmine")
                });
            }

            if (CheckResearch("ShiftFrontMRAP", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МРАП"),
                    Text = "МРАП",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontMRAPM", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МРАПМ"),
                    Text = "МРАПМ",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }
            if (CheckResearch("ShiftFrontMRAPPTUR", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МРАП ПТУР"),
                    Text = "МРАП ПТУР",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontMTLB", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МТЛБ"),
                    Text = "МТЛБ",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontMTLBM", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МТЛБМ"),
                    Text = "МТЛБМ",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontBMP", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "БМП"),
                    Text = "БМП",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontBTR", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "БТР"),
                    Text = "БТР",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontTank", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "танк"),
                    Text = "Танк",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }
        }

        public void SelectBuildType(EntityUid uid, ShiftConsoleBuildComponent comp, ICommonSession session, string type)
        {
            comp.BuildingType = type;
            comp.FutureTimer = GetBuildTimeForType(type); // Добавляем метод для получения времени постройки

            // Сразу открываем диалог ввода кода
            _quickDialog.OpenDialog(session, "Код маячка", "Введите код строительного маячка:", (string message) =>
            {
                var found = false;
                var buildQuery = EntityQueryEnumerator<ShiftBuildLightComponent>();

                while (buildQuery.MoveNext(out var lightUid, out var lightComp))
                {
                    if (lightComp.BuildingCode == message)
                    {
                        found = true;
                        comp.BuildingLight = lightUid;
                        _prayerSystem.SendSubtleMessage(session, session, "Код принят, начинаем строительство", "Успешно");

                        // Пытаемся сразу начать строительство
                        TryBuild(uid, comp, session, lightUid);
                        break;
                    }
                }

                if (!found)
                {
                    _prayerSystem.SendSubtleMessage(session, session, "Неверный код маячка", "Ошибка");
                }
            });
        }

        private int GetBuildTimeForType(string type)
        {
            return type switch
            {
                "казарма" => 60,
                "турель" => 25,
                "вышка" => 35,
                "припасы" => 25,
                "ПВО система 'Купол'" => 30,
                "ремстанция" => 22,
                "экстрактор" => 7,
                "конвертер" => 40,
                "мортира" => 90,
                "миномет" => 40,
                "лаборатория" => 40,
                "фабрикатор дронов" => 40,
                "станция РЭБ" => 10,
                "хранилище" => 20,
                "мина" => 10,
                "МРАП" => 20,
                "МРАПМ" => 30,
                "МРАП ПТУР" => 35,
                "МТЛБ" => 40,
                "МТЛБМ" => 55,
                "БМП" => 90,
                "БТР" => 75,
                "танк" => 180,
                _ => 30
            };
        }
        public void TryBuild(EntityUid uid, ShiftConsoleBuildComponent comp, ICommonSession session, EntityUid light)
        {
            // Проверка размещения маячка
            if (!HasComp<MapComponent>(Transform(light).ParentUid))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Строительный маячок должен находиться на земле",
                    "Положите маячок");
                return;
            }

            // Проверка расстояния до других структур
            if (!CheckForStructures(light))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Постройка не может быть расположена слишком близко к другой структуре",
                    "Соблюдайте интервал");
                return;
            }

            // Получаем консоль ресурсов
            if (!TryComp<ShiftConsoleResourceComponent>(GetResourceConsole(uid, comp), out var rescomp))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Необходима консоль размещения ресурсов",
                    "Нет консоли ресурсов");
                return;
            }

            // Определяем стоимость постройки
            var (polymerCost, bioCost, nanoCost) = comp.BuildingType switch
            {
                "казарма" => (150, 0, 0),
                "турель" => (60, 0, 0),
                "вышка" => (75, 100, 0),
                "припасы" => (115, 65, 0),
                "ПВО система 'Купол'" => (145, 15, 10),
                "ремстанция" => (85, 15, 15),
                "экстрактор" => (35, 0, 0),
                "конвертер" => (75, 75, 5),
                "мортира" => (750, 1000, 300),
                "миномет" => (220, 50, 25),
                "лаборатория" => (115, 150, 35),
                "фабрикатор дронов" => (180, 50, 15),
                "станция РЭБ" => (35, 15, 0),
                "хранилище" => (70, 0, 0),
                "мина" => (10, 10, 0),
                "МРАП" => (45, 15, 0),
                "МРАПМ" => (85, 25, 10),
                "МРАП ПТУР" => (100, 30, 10),
                "МТЛБ" => (145, 45, 0),
                "МТЛБМ" => (145, 45, 15),
                "БТР" => (265, 75, 25),
                "БМП" => (455, 100, 40),
                "танк" => (545, 175, 100),
                _ => (0, 0, 0)
            };

            // Проверяем и списываем ресурсы
            if (rescomp.Polymer < polymerCost || rescomp.BioShlak < bioCost || rescomp.NanoCarbon < nanoCost)
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    $"Необходимо: {polymerCost} полимера, {bioCost} биошлака, {nanoCost} нанокарбона",
                    "Недостаточно ресурсов");
                return;
            }

            // Списание ресурсов
            rescomp.Polymer -= polymerCost;
            rescomp.BioShlak -= bioCost;
            rescomp.NanoCarbon -= nanoCost;

            // Начинаем строительство
            comp.IsBuilding = true;
            comp.CurrentBuildTimer = comp.FutureTimer;
            comp.BuildingLight = light;

            // Сообщение об успешном начале строительства
            _prayerSystem.SendSubtleMessage(session, session,
                $"Строительство {comp.BuildingType} начато! Не двигайте маячок.",
                "Строительство");

            // Логирование для отладки
            Logger.Info($"Started building {comp.BuildingType} at {Transform(light).Coordinates}");
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


        private void CheckAFKPlayers()
        {
            var query = EntityQueryEnumerator<ShiftPlayerComponent>();
            var now = _timing.CurTime;

            while (query.MoveNext(out var uid, out var comp))
            {
                // Проверяем время бездействия
                var timeInactive = now - comp.LastActivityTime;

                if (timeInactive >= comp.AFKThreshold && !comp.IsAFK)
                {
                    // Помечаем как AFK
                    comp.IsAFK = true;

                    // Исключаем из команды
                    RemoveFromTeamForAFK(uid, comp, timeInactive);
                }
                else if (timeInactive < comp.AFKThreshold && comp.IsAFK)
                {
                    // Игрок вернулся
                    comp.IsAFK = false;
                    Logger.Info($"Игрок {MetaData(uid).EntityName} вернулся из AFK");
                }
            }
        }

        private void RemoveFromTeamForAFK(EntityUid playerUid, ShiftPlayerComponent playerComp, TimeSpan afkTime)
        {
            if (string.IsNullOrEmpty(playerComp.Faction))
                return;

            var minutes = (int)afkTime.TotalMinutes;

            // Ищем команду игрока
            var commandQuery = EntityQueryEnumerator<ShiftCommandComponent>();
            while (commandQuery.MoveNext(out var commandUid, out var commandComp))
            {
                if (commandComp.Faction != playerComp.Faction)
                    continue;

                // Находим сессию игрока
                if (!_sharedPlayerManager.TryGetSessionByEntity(playerUid, out var session))
                    continue;

                if (!commandComp.Players.Contains(session))
                    continue;

                // Удаляем из команды
                commandComp.Players.Remove(session);
                if (commandComp.RespawnQueue.Contains(session))
                    commandComp.RespawnQueue.Remove(session);

                // Уведомления
                _chat.DispatchGlobalAnnouncement(
                    $"Игрок {session.Name} исключен из команды {playerComp.Faction} за AFK ({minutes} минут)",
                    playSound: false,
                    colorOverride: Color.Orange,
                    sender: "Система контроля активности"
                );

                // Локальное сообщение игроку (если он вернется)
                if (TryComp<ActorComponent>(playerUid, out var actor))
                {
                    _prayerSystem.SendSubtleMessage(
                        actor.PlayerSession,
                        actor.PlayerSession,
                        $"Вы были исключены из команды {playerComp.Faction} за AFK ({minutes} минут)",
                        "Исключение из команды"
                    );
                }

                // Сбрасываем фракцию у игрока
                playerComp.Faction = string.Empty;

                Logger.Info($"Игрок {session.Name} исключен из команды {playerComp.Faction} за AFK ({minutes} минут)");
                break;
            }
        }

        private bool TryGetCommandForPlayer(EntityUid playerUid, string faction, out ShiftCommandComponent? commandComp)
        {
            commandComp = null;

            if (string.IsNullOrEmpty(faction))
                return false;

            var query = EntityQueryEnumerator<ShiftCommandComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Faction == faction)
                {
                    commandComp = comp;
                    return true;
                }
            }

            return false;
        }

        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);


            if (_timing.CurTime - _lastAFKCheckTime > _afkCheckInterval)
            {
                CheckAFKPlayers();
                _lastAFKCheckTime = _timing.CurTime;
            }




            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + TimeSpan.FromSeconds(1f);
                var smquery = EntityQueryEnumerator<ShiftShowOnMapComponent>();
                while (smquery.MoveNext(out var uid, out var comp))
                {
                    //if (!comp.Dynamic) continue;
                    foreach (var LinkedMipple in comp.LinkedMipples)
                    {
                        if (!LinkedMipple.IsValid()) continue;
                        EnsureComp<ShiftMippleComponent>(LinkedMipple, out var mipplecomp);
                        if (!mipplecomp.LinkedMap.HasValue) continue;
                        EnsureComp<ShiftMapComponent>(mipplecomp.LinkedMap.Value, out var recomp);
                        var reuid = mipplecomp.LinkedMap.Value;

                        var nc = CalculateTabletIconPosition(new Vector2(Transform(uid).Coordinates.X, Transform(uid).Coordinates.Y),
                            new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                            new Vector2(recomp.entX, recomp.entY),
                            new Vector2(recomp.offsetX, recomp.offsetY),
                            new Vector2(recomp.mapX, recomp.mapY));
                        if (TryComp<ShiftPlayerComponent>(uid, out var p) && p.Vehicle != null)
                            nc = CalculateTabletIconPosition(new Vector2(Transform(p.Vehicle.Value).Coordinates.X, Transform(p.Vehicle.Value).Coordinates.Y),
                                                        new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                                                        new Vector2(recomp.entX, recomp.entY),
                                                        new Vector2(recomp.offsetX, recomp.offsetY),
                                                        new Vector2(recomp.mapX, recomp.mapY));
                        _transform.SetWorldPosition(LinkedMipple, nc);
                        _metaData.SetEntityName(LinkedMipple, EnsureComp<MetaDataComponent>(uid).EntityName);
                    }
                }

                var spquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (spquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.Suppression < comp.SuppressionMax)
                    {
                        comp.Suppression += comp.SuppressionRecovery;
                        float zoom = 1f * (comp.Suppression / 100f);
                        zoom = Math.Clamp(zoom, 0.4f, 1f);
                        comp.Suppression = Math.Clamp(comp.Suppression, comp.SuppressionMin, comp.SuppressionMax);
                        _eye.SetZoom(uid, new Vector2(zoom, zoom));
                        _eye.SetMaxZoom(uid, new Vector2(zoom, zoom));
                        if (comp.Suppression < 50 && _random.Prob(0.2f))
                        {
                            _jitter.DoJitter(uid, TimeSpan.FromSeconds(2f), true, amplitude: 3f);
                            _chat.TrySendInGameICMessage(uid, _random.Pick(comp.SuppressionPhrases), InGameICChatType.Speak, false);
                        }
                    }
                }


            }

        }
    }
}
