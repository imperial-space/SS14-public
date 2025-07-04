using Content.Server.MagicBarrier.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Server.Imperial.DayTime;
using Content.Server.GameTicking;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Content.Shared.Damage;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Mobs.Systems;
using Content.Server.Tips;
using Content.Server.MedievalFactionFlag.Components;
using Content.Server.SpikeTrap.Components;
using Content.Shared.Mobs;
using Content.Shared.Speech;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Content.Server.BadSmell.Components;
using Content.Shared.Nocturn.Components;
using Content.Shared.Interaction;

namespace Content.Server.MagicBarrier
{
    public sealed partial class MagicBarrierSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly DayTimeSystem _dayTime = default!;
        [Dependency] private readonly TipsSystem _tips = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MagicBarrierComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MagicScrollComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<MagicBarrierCurseComponent, BeforeDamageChangedEvent>(OnCurseDamage);
            SubscribeLocalEvent<MagicBarrierComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
            SubscribeLocalEvent<MedievalSpikeTargetComponent, MobStateChangedEvent>(OnDeath);
            SubscribeLocalEvent<MedievalSpikeTargetComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MedievalSpikeTargetComponent, ScreamActionEvent>(OnScreamAction);
        }


        public void OnUseInHand(EntityUid uid, MagicScrollComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, MagicScrollComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<MagicBarrierComponent>(target, out var barrier))
            {
                barrier.Stability += comp.Power;
                Audio.PlayPvs(new SoundPathSpecifier(barrier.EffectSoundOnScrollAdd), barrier.Owner);
                QueueDel(used);
            }
        }

        private void OnRoundEnd(RoundEndTextAppendEvent ev)
        {
            int punchingBagHits = 0;
            string punchingBagName = "никто";
            int screamerScreams = 0;
            string screamerName = "никто";
            int potionsCount = 0;
            string potionsName = "никто";
            int lockpickCount = 0;
            string lockpickerName = "никто";
            int craftsCount = 0;
            string crafterName = "никто";
            int diggsCount = 0;
            string diggerName = "никто";
            int alcoholDrinks = 0;
            string alcoholickName = "никто";
            foreach (var comp in EntityManager.EntityQuery<MedievalSpikeTargetComponent>())
            {
                if (comp.Potions > potionsCount)
                {
                    potionsCount = comp.Potions;
                    potionsName = MetaData(comp.Owner).EntityName;
                }
                if (comp.Lockpicks > lockpickCount)
                {
                    lockpickCount = comp.Lockpicks;
                    lockpickerName = MetaData(comp.Owner).EntityName;
                }
                if (comp.Diggs > diggsCount)
                {
                    diggsCount = comp.Diggs;
                    diggerName = MetaData(comp.Owner).EntityName;
                }
                if (comp.Alcohol / 2 > alcoholDrinks / 2)
                {
                    alcoholDrinks = comp.Alcohol / 2;
                    alcoholickName = MetaData(comp.Owner).EntityName;
                }
                if (comp.Crafts > craftsCount)
                {
                    craftsCount = comp.Crafts;
                    crafterName = MetaData(comp.Owner).EntityName;
                }
                if (comp.Screams > screamerScreams)
                {
                    screamerScreams = comp.Screams;
                    screamerName = MetaData(comp.Owner).EntityName;
                }
                if (!_mobState.IsAlive(comp.Owner))
                    continue;
                var xform = Transform(comp.Owner);
                var coords = xform.Coordinates;
                if (comp.HitCount > punchingBagHits)
                {
                    punchingBagHits = comp.HitCount;
                    punchingBagName = MetaData(comp.Owner).EntityName;
                }

                Spawn("MedievalSpawnerDarkSkeletonRandom", coords);
                //Spawn("MedievalSpawnerDarkSkeletonRandom", coords);
            }

            int worstSmell = 0;
            string worstSmellName = "никто";
            int bestSmell = 0;
            string bestSmellName = "никто";
            foreach (var smell in EntityManager.EntityQuery<BadSmellComponent>())
            {
                if (smell.WorstSmell > worstSmell)
                {
                    worstSmell = smell.WorstSmell;
                    worstSmellName = MetaData(smell.Owner).EntityName;
                }
                if (smell.BestSmell > bestSmell)
                {
                    bestSmell = smell.BestSmell;
                    bestSmellName = MetaData(smell.Owner).EntityName;
                }
            }

            _dayTime.ChangePreset("0", "bloody", true);

            int legion = 0;
            int insurgency = 0;
            foreach (var flag in EntityManager.EntityQuery<MedievalFactionFlagComponent>())
            {
                switch (flag.Faction)
                {
                    case "legion":
                        legion += 1;
                        break;
                    case "insurgency":
                        insurgency += 1;
                        break;
                }
            }

            int nocturnAnimals = 0;
            int nocturnHumans = 0;
            int nocturnAnimalsMost = 0;
            string nocturnAnimalsMostName = "никто";
            int nocturnHumansMost = 0;
            string nocturnHumansMostName = "никто";

            foreach (var comp in EntityManager.EntityQuery<NocturnComponent>())
            {
                nocturnAnimals += comp.DrinkAnimals;
                nocturnHumans += comp.DrinkHumans;
                if (comp.DrinkAnimals > nocturnAnimalsMost)
                {
                    nocturnAnimalsMost = comp.DrinkAnimals;
                    nocturnAnimalsMostName = MetaData(comp.Owner).EntityName;
                }
                if (comp.DrinkHumans > nocturnHumansMost)
                {
                    nocturnHumansMost = comp.DrinkHumans;
                    nocturnHumansMostName = MetaData(comp.Owner).EntityName;
                }
            }
            int nocturnTotal = nocturnAnimals + nocturnHumans;

            int traps = 0;
            int humansHurt = 0;
            int deaths = 0;
            string firstDeath = "никто";
            int openedDungeons = 0;
            string firstDungeonVisiter = "никто";
            int screams = 0;
            double zveresHeat = 0;
            int alcohol = 0;
            int ghostBoo = 0;
            int ghostBooPlayers = 0;
            int potionsTotal = 0;
            int lockpicksTotal = 0;
            int craftsTotal = 0;
            int diggsTotal = 0;
            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                traps = barrier.SpikeTrapActiveted;
                humansHurt = barrier.HumanHurt;
                deaths = barrier.HumanDeath;
                firstDeath = barrier.FirstDeath;
                openedDungeons = barrier.OpenedDungeons;
                firstDungeonVisiter = barrier.FirstDungeonVisiter;
                if (firstDungeonVisiter == "nobody")
                    firstDungeonVisiter = "никто";
                screams = barrier.Screams;
                zveresHeat = Math.Round(barrier.ZveresHeat, 2);
                alcohol = barrier.AlcoholDrink / 2;
                ghostBoo = barrier.GhostBoo;
                ghostBooPlayers = barrier.GhostBooPlayers;
                potionsTotal = barrier.TotalPotions;
                lockpicksTotal = barrier.TotalLockpicks;
                craftsTotal = barrier.TotalCrafts;
                diggsTotal = barrier.TotalDiggs;
            }
            ev.AddLine("Война: ");
            ev.AddLine("  [color=cyan]Легиону[/color] подконтрольно: " + legion + " точек в регионе");
            ev.AddLine("  [color=red]Мятежникам[/color] подконтрольно: " + insurgency + " точек в регионе");
            ev.AddLine(" ");
            ev.AddLine("Исследование: ");
            ev.AddLine("  [color=pink]Открыто древних склепов[/color] " + openedDungeons + ", первый из них открыл(а) [color=pink]" + firstDungeonVisiter + "[/color]!");
            ev.AddLine("  [color=lightgreen]Шипы в полу[/color] сработали: " + traps + " раз, нанеся " + traps * 23 + " урона");
            ev.AddLine(" ");
            ev.AddLine("Бой: ");
            ev.AddLine("  [color=red]Смертей[/color] за временную петлю: " + deaths);
            ev.AddLine("  [color=pink]Умер(ла) первым[/color]: " + firstDeath);
            ev.AddLine("  [color=orange]Ударов по людям[/color] за временную петлю: " + humansHurt);
            ev.AddLine("  [color=lightblue]Был(а) атакован(а) больше всех [/color](" + punchingBagHits + " раз!) и остался(ась) жив(а) [color=lightblue]" + punchingBagName + "[/color]");
            ev.AddLine(" ");
            ev.AddLine("Веселье: ");
            ev.AddLine("  [color=yellow]Криков[/color] " + screams + ", самый крикливый [color=yellow]" + screamerName + "[/color] (кричал(а) " + screamerScreams + " раз)");
            ev.AddLine("  [color=orange]Мунвульфы получили[/color] " + zveresHeat + " единиц урона ожогами за временную петлю");
            ev.AddLine("  [color=yellow]Алкоголя выпито[/color] " + alcohol + " унций, самый пьющий - [color=yellow]" + alcoholickName + "[/color], он(а) выпил(а) " + alcoholDrinks + " унций");
            ev.AddLine("  [color=pink]Призраки всколыхнули воздух[/color] " + ghostBoo + " раз, затронув " + ghostBooPlayers + " людей");
            ev.AddLine("  Самый грязный(ая) [color=pink]" + worstSmellName + "[/color], а самый чистый(ая) [color=cyan]" + bestSmellName + "[/color]");
            ev.AddLine(" ");
            ev.AddLine("Ремесло: ");
            ev.AddLine("  [color=lightgreen]Зелий сварено[/color] " + potionsTotal + ", больше всех приготовил(а) [color=lightgreen]" + potionsName + "[/color] (" + potionsCount + " бутыльков!)");
            ev.AddLine("  [color=yellow]Дверей взломано[/color] " + lockpicksTotal + ", больше всех взломал(а) [color=yellow]" + lockpickerName + "[/color] (" + lockpickCount + " дверей!)");
            ev.AddLine("  [color=red]Вещей создано[/color] " + craftsTotal + ", больше всех создал(а) [color=red]" + crafterName + "[/color] (" + craftsCount + " вещей!)");
            ev.AddLine("  [color=gray]Взмахов киркой[/color] " + diggsTotal + ", больше всех копал(а) [color=gray]" + diggerName + "[/color] (" + diggsCount + " взмахов!)");
            ev.AddLine(" ");
            ev.AddLine("Ноктюрны: ");
            ev.AddLine("  [color=red]Крови всего выпито [/color]" + nocturnTotal + " раз. У животных - " + nocturnAnimals + " раз, у людей - " + nocturnHumans + " раз");
            ev.AddLine("  [color=pink]Больше всего людской крови[/color] пил [color=pink]" + nocturnHumansMostName + "[/color] - " + nocturnHumansMost + " раз");
            ev.AddLine("  [color=lightgreen]Больше всего животной крови[/color] пил [color=lightgreen]" + nocturnAnimalsMostName + "[/color] - " + nocturnAnimalsMost + " раз");
        }

        private void OnScreamAction(EntityUid uid, MedievalSpikeTargetComponent comp, ScreamActionEvent args)
        {
            if (args.Handled)
                return;
            comp.Screams++;
            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                barrier.Screams++;
            }
        }

        private void OnDeath(EntityUid uid, MedievalSpikeTargetComponent comp, MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead)
                return;
            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                barrier.HumanDeath++;
                if (barrier.FirstDeath == "nobody")
                    barrier.FirstDeath = MetaData(comp.Owner).EntityName;
            }
        }
        private void OnDamage(EntityUid uid, MedievalSpikeTargetComponent comp, ref BeforeDamageChangedEvent args)
        {
            if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && HasComp<MedievalZveresHeatComponent>(uid) && heat > 0)
            {
                foreach (var barrierheat in EntityManager.EntityQuery<MagicBarrierComponent>())
                    barrierheat.ZveresHeat += heat.Float();
            }
            if (args.Damage.GetTotal() < 4)
                return;
            comp.HitCount++;
            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
                barrier.HumanHurt++;
        }

        public void OnStart(EntityUid uid, MagicBarrierComponent component, ComponentStartup args)
        {
            var necrobookspawners = EntityManager.EntityQuery<NecroBookSpawnComponent>().ToArray();
            if (necrobookspawners.Length == 0)
                return;

            var choosenSpawner = _random.Pick(necrobookspawners);
            var necrobookxform = Transform(choosenSpawner.Owner);
            var necrobookcoords = necrobookxform.Coordinates;
            Spawn("MedievalBookNecro1", necrobookcoords);

            var choosenSpawner2 = _random.Pick(necrobookspawners);
            var necrobookxform2 = Transform(choosenSpawner2.Owner);
            var necrobookcoords2 = necrobookxform2.Coordinates;
            Spawn("MedievalBookNecro2", necrobookcoords2);

            var choosenSpawner3 = _random.Pick(necrobookspawners);
            var necrobookxform3 = Transform(choosenSpawner3.Owner);
            var necrobookcoords3 = necrobookxform3.Coordinates;
            Spawn("MedievalBookNecro3", necrobookcoords3);

            var choosenSpawner4 = _random.Pick(necrobookspawners);
            var necrobookxform4 = Transform(choosenSpawner4.Owner);
            var necrobookcoords4 = necrobookxform4.Coordinates;
            Spawn("MedievalDungeonKey", necrobookcoords4);

            var choosenSpawner5 = _random.Pick(necrobookspawners);
            var necrobookxform5 = Transform(choosenSpawner5.Owner);
            var necrobookcoords5 = necrobookxform5.Coordinates;
            Spawn("MedievalDungeonKey", necrobookcoords5);

            var choosenSpawner6 = _random.Pick(necrobookspawners);
            var necrobookxform6 = Transform(choosenSpawner6.Owner);
            var necrobookcoords6 = necrobookxform6.Coordinates;
            Spawn("MedievalDungeonKey", necrobookcoords6);

            var choosenSpawner7 = _random.Pick(necrobookspawners);
            var necrobookxform7 = Transform(choosenSpawner7.Owner);
            var necrobookcoords7 = necrobookxform7.Coordinates;
            Spawn("MedievalDungeonKey", necrobookcoords7);

            var choosenSpawner8 = _random.Pick(necrobookspawners);
            var necrobookxform8 = Transform(choosenSpawner8.Owner);
            var necrobookcoords8 = necrobookxform8.Coordinates;
            Spawn("MedievalDungeonKey", necrobookcoords8);

        }

        private void OnCurseDamage(EntityUid uid, MagicBarrierCurseComponent component, ref BeforeDamageChangedEvent args)
        {
            var xform = Transform(component.Owner);
            var coords = xform.Coordinates;
            Spawn("ShardCrystalRed", coords);
            Spawn("ShockWaveEffect", coords);
            QueueDel(uid);
            _chat.DispatchGlobalAnnouncement("Проклятый нарост уничтожен, расход стабильности барьера снижен.", playSound: false, colorOverride: Color.LimeGreen, sender: "Барьер");
            foreach (var comp in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                comp.Lose = comp.Lose / (comp.Rate - 0.03f);
                comp.Stability += 7f;
            }
        }
        private void OnExamine(EntityUid uid, MagicBarrierComponent component, ExaminedEvent args)
        {
            args.PushMarkup("[color=red]Текущая стабильность барьера " + Math.Round(component.Stability, 2) + " из " + component.MaxStability + "[/color]");
            args.PushMarkup("[color=cyan]Текущий расход " + Math.Round(component.Lose, 2) + " стабильности в минуту[/color]");
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<MagicBarrierComponent>())
            {

                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;

                    if (comp.Stability <= 10f && comp.Stability > 5f)
                    {
                        _chat.DispatchGlobalAnnouncement("Низкий уровень стабильности барьера", playSound: false, colorOverride: Color.GreenYellow, sender: "Барьер");
                    }
                    if (comp.Stability <= 5f)
                    {
                        _chat.DispatchGlobalAnnouncement("Крайне Низкий уровень стабильности барьера", playSound: false, colorOverride: Color.IndianRed, sender: "Барьер");
                    }

                    if (comp.Stability > 0f)
                    {
                        comp.Stability -= comp.Lose;
                    }
                    else
                    {
                        _chat.DispatchGlobalAnnouncement("Барьер не сдержал темную силу.", playSound: false, colorOverride: Color.Red, sender: "Барьер");
                        _roundEndSystem.EndRound();
                        //QueueDel(comp.Owner);
                    }
                    if (comp.Stability > comp.MaxStability)
                    {
                        _chat.DispatchGlobalAnnouncement("Слишком высокий уровень стабильности барьера, сброс.", playSound: false, colorOverride: Color.SeaGreen, sender: "Барьер");
                        comp.Stability = comp.MaxStability;
                        Spawn("ShockWaveEffect", coords);
                    }

                    comp.Cycle += 1;
                    if (comp.Cycle % 10 == 0)
                    {
                        comp.Lose = comp.Lose * comp.Rate;
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                        var choosenSpawner = _random.Pick(cursespawners);
                        var cursexform = Transform(choosenSpawner.Owner);
                        var cursecoords = cursexform.Coordinates;
                        Spawn("MedievalBarrierCurse", cursecoords);
                        _chat.DispatchGlobalAnnouncement("Расход стабильности барьера увеличен, тьма наступает.", playSound: false, colorOverride: Color.DeepPink, sender: "Барьер");
                        Spawn("ShockWaveEffect", cursecoords);
                        Spawn("ShockWaveEffect", coords);
                    }

                    comp.StarfallCurrentPoints++;
                    if (comp.StarfallCurrentPoints >= comp.StarfallPointsCapCurrent)
                    {
                        comp.StarfallPointsCapCurrent = comp.StarfallPointsCapCurrent + _random.NextFloat(-comp.StarfallRandomise, comp.StarfallRandomise);
                        comp.StarfallCurrentPoints = 0;
                        var starfallspawners = EntityManager.EntityQuery<StarFallComponent>().ToArray();
                        bool found = false;
                        var choosenSpawner = _random.Pick(starfallspawners);
                        while (!found)
                        {
                            choosenSpawner = _random.Pick(starfallspawners);
                            if (choosenSpawner.Active)
                            {
                                found = true;
                                choosenSpawner.Active = false;
                                break;
                            }
                        }
                        var starfallxform = Transform(choosenSpawner.Owner);
                        var starfallcoords = starfallxform.Coordinates;
                        float randomise = _random.NextFloat(0f, 100f);
                        Spawn("ShockWaveEffect", starfallcoords);
                        string cordX = starfallcoords.X.ToString();
                        string cordY = starfallcoords.Y.ToString();
                        if (randomise > 35)
                        {
                            _chat.DispatchGlobalAnnouncement("Падающая звезда была замечена " + choosenSpawner.Side + ". Для магической карты: X = " + cordX + ", Y = " + cordY + ".", playSound: true, colorOverride: Color.Yellow, sender: "Событие");
                            Spawn("MedievalSteroidRoomMarker", starfallcoords);
                        }
                        else
                        {
                            _chat.DispatchGlobalAnnouncement("Аура проклятого каравана была обнаружена " + choosenSpawner.Side + ". Для магической карты: X = " + cordX + ", Y = " + cordY + ".", playSound: true, colorOverride: Color.Yellow, sender: "Событие");
                            Spawn("MedievalKaravanRoomMarker", starfallcoords);
                        }
                    }

                    if (comp.Cycle == 85)
                    {
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                        var choosenSpawner = _random.Pick(cursespawners);
                        var cursexform = Transform(choosenSpawner.Owner);
                        var cursecoords = cursexform.Coordinates;
                        Spawn("MedievalSpawnNecroSenderPreset", cursecoords);
                        _chat.DispatchGlobalAnnouncement("Посланник темного повелителя замечен на этих землях.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
                    }

                    if (comp.Cycle == 161)
                    {
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierNecroSpawnComponent>().ToArray();
                        var choosenSpawner = _random.Pick(cursespawners);
                        var cursexform = Transform(choosenSpawner.Owner);
                        var cursecoords = cursexform.Coordinates;
                        for (int i = 0; i < 100; i++)
                            Spawn("MedievalSpawnNecroFighterPreset", cursecoords);
                        Spawn("MedievalSpawnNecroLeaderPreset", cursecoords);
                        _chat.DispatchGlobalAnnouncement("Бойтесь, ОНИ идут... Объединение - единственный шанс на спасение.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");

                    }

                    if (comp.Cycle == 180)
                    {
                        _chat.DispatchGlobalAnnouncement("Барьер изветшал и рассыпался в пыль.", playSound: true, colorOverride: Color.Red, sender: "Барьер");
                        _roundEndSystem.EndRound();
                    }
                }
            }
        }
    }
    public sealed partial class AlcoholDrink : EntityEffect
    {
        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        {
            return "In round end greentext";
        }

        public override void Effect(EntityEffectBaseArgs args)
        {
            if (!(args is EntityEffectReagentArgs reagentArgs)) return;
            if (reagentArgs.EntityManager.TryGetComponent<MedievalSpikeTargetComponent>(args.TargetEntity, out var player))
                player.Alcohol++;
            foreach (var barrier in reagentArgs.EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                barrier.AlcoholDrink++;
            }
        }
    }
}
