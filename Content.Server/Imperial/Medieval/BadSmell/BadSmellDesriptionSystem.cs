using Content.Server.BadSmell.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;
using Content.Shared.Inventory;
using Content.Shared.BadSmell;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Content.Shared.Maps;
using Content.Shared.Clothing.Components;

namespace Content.Server.BadSmell
{
    public sealed partial class BadSmellSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly MapSystem _map = default!;
        [Dependency] private readonly ITileDefinitionManager _tile = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;

        private const float CheckWashInterval = 20f; // Проверять "мытье" раз в 20 секунд

        //private readonly Dictionary<EntityUid, float> _cachedWashValues = new(); // Кэш значений "мытья"
        private readonly Dictionary<EntityUid, TimeSpan> _nextSoundPlayTime = new(); // Таймеры для звуков
        private readonly Dictionary<EntityUid, float> _previousSmellLevels = new(); // Кэшируем предыдущий уровень запаха для сущности

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BadSmellComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<BadSmelClearComponent, ComponentStartup>(OnClear);
            SubscribeLocalEvent<BadSmellComponent, ComponentStartup>(OnStart);

            //Подписываемся на добавление/удаление компонента, чтобы начать/остановить обработку
            SubscribeLocalEvent<BadSmellComponent, ComponentShutdown>(OnBadSmellRemoved);
            SubscribeLocalEvent<ClothingComponent, InventoryRelayedEvent<CleaningActionEvent>>(Cleaning);

            //Пример: событие, которое может менять "грязность" (замените на актуальное событие вашей игры)
            //SubscribeLocalEvent<CleaningActionEvent>(OnCleaningAction);
        }

        private void OnBadSmellRemoved(EntityUid uid, BadSmellComponent component, ComponentShutdown args)
        {
            //_cachedWashValues.Remove(uid);
            _nextSoundPlayTime.Remove(uid);
            _alerts.ClearAlert(uid, component.SmellAlert); // Убираем алерт при удалении компонента
        }
        private void Cleaning(EntityUid uid, ClothingComponent component, ref InventoryRelayedEvent<CleaningActionEvent> args)
        {
            args.Args.CleaningAmount *= 0.75f;
        }


        //  Пример обработки события "очистки" (замените CleaningActionEvent на реальное событие)
        /* private void OnCleaningAction(CleaningActionEvent ev, EntityUid uid, BadSmellComponent component)
         {
             // Обновляем запах на основании действия очистки
             component.SmellLevel -= ev.CleaningAmount;
             if (component.SmellLevel < 0)
                 component.SmellLevel = 0;

             //Запускаем UpdateSmell для этой сущности
             UpdateSmell(uid, component);
         }*/


        private void OnStart(EntityUid uid, BadSmellComponent component, ComponentStartup args)
        {
            _nextSoundPlayTime[uid] = _timing.CurTime; // Инициализируем таймер звука
            var alertLevel = (short)Math.Clamp(Math.Round(component.SmellLevel / component.MaxSmellLevel * 4.1f), 0, 4);
            _alerts.ShowAlert(component.Owner, component.SmellAlert, alertLevel);
            if (TryComp<BadSmellRaceModifierComponent>(uid, out var race))
                component.GrowTemp *= race.Modifier;
        }

        private void OnClear(EntityUid uid, BadSmelClearComponent component, ComponentStartup args)
        {
            if (TryComp<BadSmellComponent>(uid, out var feel))
                feel.GrowTemp = feel.GrowTemp / 2f;
        }
        private void OnExamine(EntityUid uid, BadSmellComponent component, ExaminedEvent args)
        {
            if (TryComp<BadSmellFeelComponent>(args.Examiner, out var feel) && !feel.DescEnabled)
            {
                args.PushMarkup("[color=gray]Вы не чувствуете запахи[/color]");
                return;
            }
            if (component.SmellLevel > 60f && component.SmellLevel <= 80f)
                args.PushMarkup("[color=sandybrown]Воняет помоями[/color]");
            if (component.SmellLevel > 80f)
                args.PushMarkup("[color=orange]Невероятно воняет помоями[/color]");
            if (component.SmellLevel < 25f)
                args.PushMarkup("[color=green]Пахнет свежестью[/color]");
        }
        TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        TimeSpan ReloadTime = TimeSpan.FromSeconds(25f);

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var curTime = _timing.CurTime;

            if (curTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + ReloadTime;

                foreach (var comp in EntityManager.EntityQuery<BadSmellComponent>())
                {
                    UpdateSmell(comp.Owner, comp, curTime);
                }
            }

        }

        //Выделяем логику обновления запаха в отдельный метод
        private void UpdateSmell(EntityUid uid, BadSmellComponent comp, TimeSpan curTime)
        {

            var xform = Transform(uid);
            var coords = xform.Coordinates;
            var clearvalue = CheckWash(coords);
            if (clearvalue > 0)
            {
                var ev = new CleaningActionEvent(comp.WashTemp);
                RaiseLocalEvent(uid, ev);
                comp.SmellLevel = Math.Min(Math.Max(comp.SmellLevel - ev.CleaningAmount, clearvalue), comp.SmellLevel);
            }
            else
            {
                var grow = comp.GrowTemp;
                if (xform.GridUid != null && TryComp<MapGridComponent>(xform.GridUid, out var grid))
                    grow *= ((ContentTileDefinition)_tile[_map.GetTileRef(xform.GridUid.Value, grid, coords).Tile.TypeId]).BadSmellModifier;

                comp.SmellLevel += grow;
            }
            if (comp.SmellLevel > comp.MaxSmellLevel)
            {
                comp.SmellLevel = comp.MaxSmellLevel;
                if (_mobState.IsAlive(comp.Owner))
                    comp.WorstSmell++;
            }
            if (comp.SmellLevel < 0)
                comp.SmellLevel = 0;
            if (comp.SmellLevel < 40f && _mobState.IsAlive(comp.Owner))
                comp.BestSmell++;

            var alertLevel = (short)Math.Clamp(Math.Round(comp.SmellLevel / comp.MaxSmellLevel * 4.1f), 0, 4);
            // Обновляем алерт только если уровень запаха изменился
            if (!_previousSmellLevels.TryGetValue(uid, out var previousLevel) || Math.Abs(previousLevel - comp.SmellLevel) > 0.01f)
            {
                _alerts.ShowAlert(comp.Owner, comp.SmellAlert, alertLevel);
                _previousSmellLevels[uid] = comp.SmellLevel;  // Обновляем закэшированный уровень
            }



            // Звуки проигрываем с ограничением по времени
            if (_random.Prob(comp.SmellLevel / 120f) && comp.SmellLevel > 55 && curTime > _nextSoundPlayTime.GetValueOrDefault(uid, TimeSpan.Zero))
            {
                _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSound), comp.Owner, AudioParams.Default.WithVolume(20f));
                _nextSoundPlayTime[uid] = curTime + TimeSpan.FromSeconds(5f); // Задержка между звуками
            }
            if (comp.SmellLevel > 60)
            {
                foreach (var entity in _lookup.GetEntitiesInRange<BadSmellFeelComponent>(coords, 3f, flags: LookupFlags.Dynamic))
                {
                    if (TryComp<BadSmellFeelComponent>(entity, out var feel) && feel.Enabled && entity.Owner != comp.Owner)
                    {

                        if (_random.Prob(comp.SmellLevel / 200f))
                            _popup.PopupEntity("Вы чувствуете резкий запах вони", feel.Owner, feel.Owner, PopupType.LargeCaution);

                    }
                }
            }
            _appearance.SetData(uid, BadSmellVisuals.Dirt, Math.Min(Math.Floor(comp.SmellLevel / 20f), 4));
        }

        public float CheckWash(EntityCoordinates coords)
        {

            foreach (var entity in _lookup.GetEntitiesInRange(coords, 0.3f))
            {
                if (TryComp<BadSmelWashComponent>(entity, out var wash))
                {
                    return wash.MaxWash;
                }

            }
            return 0f;
        }


    }
}
