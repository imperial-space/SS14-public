using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.BadSmell.Components;
using System.Linq;
using Content.Server.SpikeTrap.Components;
using Content.Shared.Nocturn.Components;
using Content.Server.MedievalFactionFlag.Components;
using Content.Server.Imperial.DayTime;
using Content.Shared.Damage;
using Content.Shared.Speech;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class RoundStatCounterRuleSystem : GameRuleSystem<RoundStatCounterRuleComponent>
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DayTimeSystem _dayTime = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AffectRoundStatsComponent, ScreamActionEvent>(OnScreamAction);
        SubscribeLocalEvent<AffectRoundStatsComponent, MobStateChangedEvent>(OnDeath);
        SubscribeLocalEvent<AffectRoundStatsComponent, BeforeDamageChangedEvent>(OnDamage);
    }

    private void OnScreamAction(EntityUid uid, AffectRoundStatsComponent comp, ScreamActionEvent args)
    {
        if (args.Handled)
            return;

        comp.Screams++;
        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
        {
            barrier.Screams++;
        }
    }

    private void OnDeath(EntityUid uid, AffectRoundStatsComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
        {
            barrier.HumanDeath++;
            if (barrier.FirstDeath == "nobody")
                barrier.FirstDeath = Name(uid);
        }
    }

    private void OnDamage(EntityUid uid, AffectRoundStatsComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && HasComp<MedievalZveresHeatComponent>(uid) && heat > 0)
        {
            foreach (var barrierheat in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                barrierheat.ZveresHeat += heat.Float();
        }
        if (args.Damage.GetTotal() < 4)
            return;
        comp.HitCount++;
        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
            barrier.HumanHurt++;
    }

    protected override void AppendRoundEndText(EntityUid uid,
        RoundStatCounterRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

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

        var targets = EntityManager.AllEntities<AffectRoundStatsComponent>();
        if (targets.Count() > 0)
        {
            potionsCount = targets.MaxBy(x => x.Comp.Potions).Comp.Potions;
            potionsName = Name(targets.MaxBy(x => x.Comp.Potions).Owner);

            lockpickCount = targets.MaxBy(x => x.Comp.Lockpicks).Comp.Lockpicks;
            potionsName = Name(targets.MaxBy(x => x.Comp.Lockpicks).Owner);

            diggsCount = targets.MaxBy(x => x.Comp.Diggs).Comp.Diggs;
            diggerName = Name(targets.MaxBy(x => x.Comp.Diggs).Owner);

            alcoholDrinks = targets.MaxBy(x => x.Comp.Alcohol / 2).Comp.Alcohol / 2;
            alcoholickName = Name(targets.MaxBy(x => x.Comp.Alcohol / 2).Owner);

            craftsCount = targets.MaxBy(x => x.Comp.Crafts).Comp.Crafts;
            crafterName = Name(targets.MaxBy(x => x.Comp.Crafts).Owner);

            screamerScreams = targets.MaxBy(x => x.Comp.Screams).Comp.Screams;
            screamerName = Name(targets.MaxBy(x => x.Comp.Screams).Owner);

            punchingBagHits = targets.MaxBy(x => x.Comp.HitCount).Comp.HitCount;
            punchingBagName = Name(targets.MaxBy(x => x.Comp.HitCount).Owner);
        }

        foreach (var (target, comp) in targets)
        {
            if (!_mobState.IsAlive(target))
                continue;

            var xform = Transform(target);
            var coords = xform.Coordinates;

            Spawn("MedievalSpawnerDarkSkeletonRandom", coords);
        }

        int worstSmell = 0;
        string worstSmellName = "никто";
        int bestSmell = 0;
        string bestSmellName = "никто";

        var smell = EntityManager.AllEntities<BadSmellComponent>();
        if (smell.Count() > 0)
        {
            worstSmell = smell.MaxBy(x => x.Comp.WorstSmell).Comp.WorstSmell;
            worstSmellName = Name(smell.MaxBy(x => x.Comp.WorstSmell).Owner);

            bestSmell = smell.MaxBy(x => x.Comp.BestSmell).Comp.BestSmell;
            bestSmellName = Name(smell.MaxBy(x => x.Comp.BestSmell).Owner);
        }

        _dayTime.ChangePreset("0", "bloody", true);

        int legion = 0;
        int insurgency = 0;
        foreach (var flag in EntityManager.EntityQuery<MedievalFactionFlagComponent>())
        {
            switch (flag.Faction)
            {
                case "legion":
                    legion++;
                    break;
                case "insurgency":
                    insurgency++;
                    break;
            }
        }

        int nocturnAnimals = 0;
        int nocturnHumans = 0;
        int nocturnAnimalsMost = 0;
        string nocturnAnimalsMostName = "никто";
        int nocturnHumansMost = 0;
        string nocturnHumansMostName = "никто";

        var nocturns = EntityManager.AllEntities<NocturnComponent>();
        if (nocturns.Count() > 0)
        {
            nocturnAnimalsMost = nocturns.MaxBy(x => x.Comp.DrinkAnimals).Comp.DrinkAnimals;
            nocturnAnimalsMostName = Name(nocturns.MaxBy(x => x.Comp.DrinkAnimals).Owner);

            nocturnHumansMost = nocturns.MaxBy(x => x.Comp.DrinkHumans).Comp.DrinkHumans;
            nocturnHumansMostName = Name(nocturns.MaxBy(x => x.Comp.DrinkHumans).Owner);

            nocturnAnimals += nocturns.Select(x => x.Comp.DrinkAnimals).Sum();
            nocturnHumans += nocturns.Select(x => x.Comp.DrinkHumans).Sum();
        }


        int nocturnTotal = nocturnAnimals + nocturnHumans;

        int traps = component.SpikeTrapActiveted;
        int humansHurt = component.HumanHurt;
        int deaths = component.HumanDeath;
        string firstDeath = component.FirstDeath;
        int openedDungeons = component.OpenedDungeons;
        string firstDungeonVisiter = component.FirstDungeonVisiter;
        int screams = component.Screams;
        double zveresHeat = Math.Round(component.ZveresHeat, 2);
        int alcohol = component.AlcoholDrink;
        int ghostBoo = component.GhostBoo;
        int ghostBooPlayers = component.GhostBooPlayers;
        int potionsTotal = component.TotalPotions;
        int lockpicksTotal = component.TotalLockpicks;
        int craftsTotal = component.TotalCrafts;
        int diggsTotal = component.TotalDiggs;

        args.AddLine("Война: ");
        args.AddLine("  [color=cyan]Легиону[/color] подконтрольно: " + legion + " точек в регионе");
        args.AddLine("  [color=red]Мятежникам[/color] подконтрольно: " + insurgency + " точек в регионе");
        args.AddLine(" ");
        args.AddLine("Исследование: ");
        args.AddLine("  [color=pink]Открыто древних склепов[/color] " + openedDungeons + ", первый из них открыл(а) [color=pink]" + firstDungeonVisiter + "[/color]!");
        args.AddLine("  [color=lightgreen]Шипы в полу[/color] сработали: " + traps + " раз, нанеся " + traps * 23 + " урона");
        args.AddLine(" ");
        args.AddLine("Бой: ");
        args.AddLine("  [color=red]Смертей[/color] за временную петлю: " + deaths);
        args.AddLine("  [color=pink]Умер(ла) первым[/color]: " + firstDeath);
        args.AddLine("  [color=orange]Ударов по людям[/color] за временную петлю: " + humansHurt);
        args.AddLine("  [color=lightblue]Был(а) атакован(а) больше всех [/color](" + punchingBagHits + " раз!) и остался(ась) жив(а) [color=lightblue]" + punchingBagName + "[/color]");
        args.AddLine(" ");
        args.AddLine("Веселье: ");
        args.AddLine("  [color=yellow]Криков[/color] " + screams + ", самый крикливый [color=yellow]" + screamerName + "[/color] (кричал(а) " + screamerScreams + " раз)");
        args.AddLine("  [color=orange]Мунвульфы получили[/color] " + zveresHeat + " единиц урона ожогами за временную петлю");
        args.AddLine("  [color=yellow]Алкоголя выпито[/color] " + alcohol + " унций, самый пьющий - [color=yellow]" + alcoholickName + "[/color], он(а) выпил(а) " + alcoholDrinks + " унций");
        args.AddLine("  [color=pink]Призраки всколыхнули воздух[/color] " + ghostBoo + " раз, затронув " + ghostBooPlayers + " людей");
        args.AddLine("  Самый грязный(ая) [color=pink]" + worstSmellName + "[/color], а самый чистый(ая) [color=cyan]" + bestSmellName + "[/color]");
        args.AddLine(" ");
        args.AddLine("Ремесло: ");
        args.AddLine("  [color=lightgreen]Зелий сварено[/color] " + potionsTotal + ", больше всех приготовил(а) [color=lightgreen]" + potionsName + "[/color] (" + potionsCount + " бутыльков!)");
        args.AddLine("  [color=yellow]Дверей взломано[/color] " + lockpicksTotal + ", больше всех взломал(а) [color=yellow]" + lockpickerName + "[/color] (" + lockpickCount + " дверей!)");
        args.AddLine("  [color=red]Вещей создано[/color] " + craftsTotal + ", больше всех создал(а) [color=red]" + crafterName + "[/color] (" + craftsCount + " вещей!)");
        args.AddLine("  [color=gray]Взмахов киркой[/color] " + diggsTotal + ", больше всех копал(а) [color=gray]" + diggerName + "[/color] (" + diggsCount + " взмахов!)");
        args.AddLine(" ");
        args.AddLine("Ноктюрны: ");
        args.AddLine("  [color=red]Крови всего выпито [/color]" + nocturnTotal + " раз. У животных - " + nocturnAnimals + " раз, у людей - " + nocturnHumans + " раз");
        args.AddLine("  [color=pink]Больше всего людской крови[/color] пил [color=pink]" + nocturnHumansMostName + "[/color] - " + nocturnHumansMost + " раз");
        args.AddLine("  [color=lightgreen]Больше всего животной крови[/color] пил [color=lightgreen]" + nocturnAnimalsMostName + "[/color] - " + nocturnAnimalsMost + " раз");
    }
}
