using Content.Server.Body.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Drugs;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Server.Instruments;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Slippery;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Mood;

/// <summary>Откуда берутся мудлеты: состояния тела, увиденная смерть, объятия, музыка.</summary>
public sealed partial class ImperialMoodSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;

    private const string Nutrition = "nutrition";

    /// <summary>Видимость смерти: в TG это view(), здесь радиус осмотра без преград.</summary>
    private const float DeathViewRange = 7f;

    private const float MusicRange = 7f;

    private void InitializeTriggers()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyStateChanged);
        SubscribeLocalEvent<SlipperyComponent, SlipEvent>(OnSlip);
        SubscribeLocalEvent<InteractionPopupComponent, InteractionSuccessEvent>(OnInteractionSuccess);
    }

    /// <summary>Состояния, которые TG проверяет на каждом тике жизни.</summary>
    private void PollTriggers(Entity<ImperialMoodComponent> ent)
    {
        var uid = ent.Owner;

        UpdateNutrition(ent);

        // carbon/life.dm: удушье.
        Toggle(ent, "suffocation", "suffocation", TryComp<RespiratorComponent>(uid, out var respirator) &&
                                                     respirator.SuffocationCycles >= respirator.SuffocationCycleThreshold);

        // Горение.
        Toggle(ent, "on_fire", "on_fire", TryComp<FlammableComponent>(uid, out var flammable) && flammable.OnFire);

        // species.dm: переохлаждение.
        Toggle(ent, "cold", "cold", TryComp<TemperatureComponent>(uid, out var temperature) &&
                                    TryComp<TemperatureDamageComponent>(uid, out var damage) &&
                                    temperature.CurrentTemperature < damage.ColdDamageThreshold);

        // carbon.dm: в наручниках.
        Toggle(ent, "handcuffed", "handcuffed", TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.CuffedHandCount > 0);

        // jitteriness.dm и drugginess.dm.
        Toggle(ent, "jittery", "jittery", HasComp<JitteringComponent>(uid));
        Toggle(ent, "high", "high", _status.HasEffectComp<SeeingRainbowsStatusEffectComponent>(uid));

        // splat.dm: крем на лице.
        var creamed = TryComp<CreamPiedComponent>(uid, out var cream) && cream.CreamPied;
        if (creamed && !HasMood((uid, ent.Comp), "creampie"))
            AddMood((uid, ent.Comp), "creampie", "creampie");

        UpdateDrunk(ent);
        UpdateMusic(ent);
    }

    /// <summary>Мудлет держится, пока условие выполняется.</summary>
    private void Toggle(Entity<ImperialMoodComponent> ent, string category, ProtoId<ImperialMoodEventPrototype> id, bool active)
    {
        if (active)
        {
            if (!ent.Comp.Events.ContainsKey(category))
                AddMood((ent.Owner, ent.Comp), category, id);
        }
        else if (ent.Comp.Events.ContainsKey(category))
        {
            ClearMood((ent.Owner, ent.Comp), category);
        }
    }

    /// <summary>update_nutrition_moodlets по порогам голода SS14.</summary>
    private void UpdateNutrition(Entity<ImperialMoodComponent> ent)
    {
        if (!TryComp<HungerComponent>(ent, out var hunger))
        {
            ClearMood((ent.Owner, ent.Comp), Nutrition);
            return;
        }

        ProtoId<ImperialMoodEventPrototype>? id = _hunger.GetHungerThreshold(hunger) switch
        {
            HungerThreshold.Overfed => "wellfed",
            HungerThreshold.Okay => "fed",
            HungerThreshold.Peckish => "hungry",
            HungerThreshold.Starving => "hungry_very",
            HungerThreshold.Dead => "starving",
            _ => null,
        };

        if (id == null)
        {
            ClearMood((ent.Owner, ent.Comp), Nutrition);
            return;
        }

        if (GetMood((ent.Owner, ent.Comp), Nutrition)?.Event != id)
            AddMood((ent.Owner, ent.Comp), Nutrition, id.Value);
    }

    /// <summary>inebriated/drunk: чем пьянее, тем веселее (update_change).</summary>
    private void UpdateDrunk(Entity<ImperialMoodComponent> ent)
    {
        if (!_status.TryGetMaxTime<DrunkStatusEffectComponent>(ent, out var drunk) || drunk.EndEffectTime is not { } end)
        {
            // clear_effects: любителю выпить хорошо и после.
            if (ent.Comp.DrunkStage != 0 && HasPersonality(ent.Comp, "bibulous"))
                AddMood((ent.Owner, ent.Comp), "drunk_after", "drunk_after");

            ent.Comp.DrunkStage = 0;
            ClearMood((ent.Owner, ent.Comp), "drunk");
            return;
        }

        var drunkness = (end - _timing.CurTime).TotalSeconds;
        var (change, stage) = drunkness switch
        {
            < 30 => (3, 1),
            < 45 => (4, 2),
            < 60 => (5, 3),
            < 90 => (6, 4),
            _ => (3, 5),
        };

        if (ent.Comp.DrunkStage == stage)
            return;

        ent.Comp.DrunkStage = stage;
        ClearMood((ent.Owner, ent.Comp), "drunk");
        ClearMood((ent.Owner, ent.Comp), "drunk_after");
        AddMood((ent.Owner, ent.Comp), "drunk", "drunk", change: change,
            description: Loc.GetString($"imperial-mood-drunk-{stage}"));
    }

    /// <summary>good_music: рядом кто-то играет на инструменте.</summary>
    private void UpdateMusic(Entity<ImperialMoodComponent> ent)
    {
        foreach (var instrument in _lookup.GetEntitiesInRange<InstrumentComponent>(Transform(ent).Coordinates, MusicRange))
        {
            if (!instrument.Comp.Playing || !_examine.InRangeUnOccluded(ent, instrument, MusicRange))
                continue;

            AddMood((ent.Owner, ent.Comp), "goodmusic", "goodmusic");
            return;
        }
    }

    /// <summary>mob_movement.dm: поскользнулся.</summary>
    private void OnSlip(Entity<SlipperyComponent> ent, ref SlipEvent args)
    {
        AddMood(args.Slipped, "slipped", "slipped");

        // slip_observed: чудакам смешно смотреть, как кто-то падает.
        foreach (var viewer in _lookup.GetEntitiesInRange<ImperialMoodComponent>(Transform(args.Slipped).Coordinates, 5f))
        {
            if (viewer.Owner != args.Slipped && HasPersonality(viewer.Comp, "whimsical") &&
                _examine.InRangeUnOccluded(viewer, args.Slipped, 5f))
                AddMood(viewer.AsNullable(), "slip_observed", "whimsical_slip");
        }
    }

    /// <summary>help_shake_act и pet_bonus: обнять человека или погладить зверя.</summary>
    private void OnInteractionSuccess(Entity<InteractionPopupComponent> ent, ref InteractionSuccessEvent args)
    {
        if (args.User == ent.Owner)
            return;

        if (HasComp<HumanoidProfileComponent>(ent))
        {
            AddMood(ent.Owner, "hug", "hug");
            if (TryComp<ImperialMoodComponent>(ent, out var mood))
                BadTouch((ent.Owner, mood));
        }
        else if (HasComp<MobStateComponent>(ent))
            AddMood(args.User, $"pet_{ent.Owner}", "pet_animal", ent.Owner);
    }

    #region Смерть

    /// <summary>see_death: свидетели смерти расстраиваются, а иные — радуются.</summary>
    private void OnAnyStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var dead = args.Target;
        var query = _lookup.GetEntitiesInRange<ImperialMoodComponent>(Transform(dead).Coordinates, DeathViewRange);
        foreach (var witness in query)
        {
            if (witness.Owner == dead || _mobState.IsIncapacitated(witness) || !_examine.InRangeUnOccluded(witness, dead, DeathViewRange))
                continue;

            SeeDeath(witness, dead);
        }
    }

    private void SeeDeath(Entity<ImperialMoodComponent> witness, EntityUid dead)
    {
        const int baseChange = -8;
        const float baseTimeout = 300;
        var category = "see_death";

        string id;
        int priority;
        float change;
        string description;

        var pet = !HasComp<HumanoidProfileComponent>(dead);
        var xenoDead = IsAlien(dead);
        var timeout = baseTimeout;
        if (HasPersonality(witness.Comp, "callous") || pet && HasPersonality(witness.Comp, "animal_disliker"))
        {
            // dontcare: бессердечным и нелюбителям животных всё равно.
            id = "see_death_dontcare";
            priority = 40;
            change = 0;
            description = Loc.GetString("imperial-mood-see-death-dontcare", ("target", Descriptor(dead)));
        }
        else if (IsClown(witness))
        {
            // naive: клоуны не понимают смерти.
            id = "see_death_naive";
            priority = 100;
            change = 0;
            description = Loc.GetString("imperial-mood-see-death-naive", ("target", Descriptor(dead)));
        }
        else if (xenoDead && !IsAlien(witness))
        {
            id = "see_death_xeno";
            priority = 35;
            change = -baseChange * 0.5f;
            description = Loc.GetString("imperial-mood-see-death-xeno");
        }
        else if (pet)
        {
            id = "see_death_pet";
            priority = 30;
            change = baseChange;
            if (HasPersonality(witness.Comp, "animal_friend"))
                change *= 1.5f;
            else if (!HasPersonality(witness.Comp, "compassionate"))
                change *= 0.25f;
            description = Loc.GetString("imperial-mood-see-death-pet", ("target", Descriptor(dead)));
        }
        else
        {
            id = "see_death";
            priority = 0;
            change = baseChange;
            description = Loc.GetString("imperial-mood-see-death", ("target", Descriptor(dead)));
        }

        // compassionate: чужая смерть ранит сильнее и дольше.
        if (change < 0 && HasPersonality(witness.Comp, "compassionate"))
        {
            change *= 1.5f;
            timeout *= 1.5f;
        }

        // be_refreshed: новая смерть того же рода давит сильнее.
        var existing = GetMood((witness.Owner, witness.Comp), category);
        if (existing != null && existing.Event == id && change < 0)
        {
            existing.Change = (int) Math.Floor(existing.Change * 1.5f);
            existing.Expires = _timing.CurTime + TimeSpan.FromSeconds(existing.Timeout);
            UpdateMood(witness);
            return;
        }

        AddMood((witness.Owner, witness.Comp), category, id, dead, (int) Math.Ceiling(change), description, priority, timeout);
    }

    /// <summary>get_descriptor: должность члена экипажа, имя питомца или «кто-то».</summary>
    private string Descriptor(EntityUid dead)
    {
        if (!HasComp<HumanoidProfileComponent>(dead))
            return Name(dead);

        if (_minds.TryGetMind(dead, out var mind, out _) && _jobs.MindTryGetJobName(mind, out var job))
            return job.ToLowerInvariant();

        return Loc.GetString("imperial-mood-someone");
    }

    private bool IsClown(EntityUid uid)
    {
        return _minds.TryGetMind(uid, out var mind, out _) && _jobs.MindTryGetJobId(mind, out var job) && job == "Clown";
    }

    private bool IsAlien(EntityUid uid)
    {
        return Factory.TryGetRegistration("Xenomorph", out var reg) && HasComp(uid, reg.Type) ||
               Factory.TryGetRegistration("XenoLarva", out var larva) && HasComp(uid, larva.Type);
    }

    #endregion
}
