using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Imperial.Medieval.Skills;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Sprint;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private Random _allergyRandom = new();

    private void InitializeSymptoms()
    {
        SubscribeLocalEvent<VomitSicknessComponent, ComponentInit>(OnSickInit);
        SubscribeLocalEvent<ReagentAllergyComponent, ComponentInit>(OnReagentAllergyInit);
        SubscribeLocalEvent<ReagentAllergyComponent, ReagentMetabolizingEvent>(OnReagentMetabolizing);

        SubscribeLocalEvent<EntityAllergyComponent, ComponentInit>(OnEntityAllergyInit);
        SubscribeLocalEvent<EntityAllergyComponent, CanBreatheEvent>(OnAllergyCanBreathe);

        SubscribeLocalEvent<AsthmaComponent, CanBreatheEvent>(OnAsthmaCanBreathe);
        SubscribeLocalEvent<LungsCancerComponent, CanBreatheEvent>(OnCancerCanBreathe);
        SubscribeLocalEvent<PlagueBlockBreathingComponent, CanBreatheEvent>(OnBlockerCanBreathe);
        SubscribeLocalEvent<PlagueBlockSpeechComponent, SpeakAttemptEvent>(OnBlockerSpeackAttempt);

        SubscribeLocalEvent<WeakSkinComponent, DamageModifyEvent>(OnDamageModify);

        SubscribeLocalEvent<LoweredSkillsComponent, MapInitEvent>(OnLoweredSkillsMapInit);
        SubscribeLocalEvent<LoweredSkillsComponent, ComponentShutdown>(OnLoweredSkillsShutdown);

        SubscribeLocalEvent<MedievalPlagueInfectedComponent, AddSymptomEffectsEvent>(OnAddEffects);
        SubscribeLocalEvent<MedievalPlagueInfectedComponent, PlagueAddComponentsEvent>(OnAddComponents);
    }

    private void OnSickInit(EntityUid uid, VomitSicknessComponent component, ComponentInit args)
    {
        component.StartTime = _timing.CurTime;
        component.EndTime = _timing.CurTime + TimeSpan.FromSeconds(component.Duration);
    }

    private void OnReagentAllergyInit(EntityUid uid, ReagentAllergyComponent component, ComponentInit args)
    {
        if (component.RandomReagents.Count <= 0)
            return;

        var list = new List<string>(component.RandomReagents);
        _allergyRandom.Shuffle(list);
        for (var i = 0; i < list.Count && component.Reagents.Count < component.RandomCount; i++)
        {
            component.Reagents.Add(list[i]);
        }
    }

    private void OnReagentMetabolizing(EntityUid uid, ReagentAllergyComponent component, ref ReagentMetabolizingEvent args)
    {
        if (!component.Reagents.Contains(args.Reagent))
            return;

        _damageable.TryChangeDamage(uid, component.Damage, true, false);
    }


    private void OnEntityAllergyInit(EntityUid uid, EntityAllergyComponent component, ComponentInit args)
    {
        if (component.RandomIds.Count <= 0)
            return;

        var list = new List<string>(component.RandomIds);
        _allergyRandom.Shuffle(list);
        for (var i = 0; i < list.Count && component.Ids.Count < component.RandomCount; i++)
        {
            component.Ids.Add(list[i]);
        }
    }

    private void OnAllergyCanBreathe(EntityUid uid, EntityAllergyComponent component, ref CanBreatheEvent args)
    {
        if (_lookup.GetEntitiesInRange<AllergySpreaderComponent>(Transform(uid).Coordinates, component.Distance).Count <= 0)
            return;

        args.Cancelled = true;
    }

    private void OnAsthmaCanBreathe(EntityUid uid, AsthmaComponent component, ref CanBreatheEvent args)
    {
        if (!TryComp<MedievalSprintComponent>(uid, out var sprint))
            return;

        if (!sprint.Sprinting)
            return;

        args.Cancelled = true;
        _damageable.TryChangeDamage(uid, component.Damage, true, false);
    }

    private void OnDamageModify(EntityUid uid, WeakSkinComponent component, DamageModifyEvent args)
    {
        foreach (var item in component.TypeModifiers)
        {
            if (args.Damage.DamageDict.ContainsKey(item.Key))
                args.Damage.DamageDict[item.Key] *= item.Value;
        }
    }

    private void OnLoweredSkillsMapInit(EntityUid uid, LoweredSkillsComponent component, MapInitEvent args)
    {
        if (!TryComp<SkillsComponent>(uid, out var skills))
        {
            RemComp(uid, component);
            return;
        }

        var list = new Dictionary<string, int>();
        foreach (var item in skills.Levels)
        {
            list.Add(item.Key, Math.Clamp(item.Value - 2, 1, 20));
        }

        component.OriginalLevels = new(skills.Levels);
        _skills.SetSkills(uid, list);
    }

    private void OnLoweredSkillsShutdown(EntityUid uid, LoweredSkillsComponent component, ComponentShutdown args)
    {
        if (!TryComp<SkillsComponent>(uid, out var skills) || component.OriginalLevels.Count <= 0)
            return;

        _skills.SetSkills(uid, component.OriginalLevels);
    }

    private void OnCancerCanBreathe(EntityUid uid, LungsCancerComponent component, ref CanBreatheEvent args)
    {
        if (!component.Active)
            return;

        args.Cancelled = true;
    }

    private void OnBlockerCanBreathe(EntityUid uid, PlagueBlockBreathingComponent component, ref CanBreatheEvent args)
    {
        if (component.EndTime < _timing.CurTime)
        {
            RemComp<PlagueBlockBreathingComponent>(uid);
            return;
        }

        args.Cancelled = true;
    }

    private void OnBlockerSpeackAttempt(EntityUid uid, PlagueBlockSpeechComponent component, SpeakAttemptEvent args)
    {
        var chance = component.Chance;
        if (TryComp<SkillsComponent>(uid, out var skills))
            chance -= (skills.Levels[SkillsSystem.VitalityId] - 10) * 0.02f;

        if (!_random.Prob(chance))
            return;

        args.Cancel();
        _damageable.TryChangeDamage(uid, component.Damage);
        _popup.PopupEntity(Loc.GetString("plague-pantomime-speech-blocked-popup"), uid, uid, PopupType.MediumCaution);
    }

    private void OnAddEffects(EntityUid uid, MedievalPlagueInfectedComponent comp, AddSymptomEffectsEvent args)
    {
        var time = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(5, 25));
        var effects = args.Incubation ? comp.IncubationEffects : comp.Effects;

        if (effects.TryGetValue(args.Id, out var old) && old.Priority > args.Effect.Priority)
            return;

        if (effects.ContainsKey(args.Id))
            effects.Remove(args.Id);

        var effect = args.Effect.CreateInstance();
        effect.NextEffect = time;

        effects.Add(args.Id, effect);
    }

    private void OnAddComponents(EntityUid uid, MedievalPlagueInfectedComponent comp, PlagueAddComponentsEvent args)
    {
        EntityManager.AddComponents(uid, args.Components);
        foreach (var item in args.Components)
        {
            var type = item.Value.Component.GetType();
            (args.Incubation ? comp.IncubationComponents : comp.PlagueComponents).Add(EntityManager.GetComponent(uid, type));
        }
    }

    private void DoEffects(EntityUid uid, MedievalPlagueInfectedComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        foreach (var item in comp.Incubation ? comp.IncubationEffects : comp.Effects)
        {
            if (item.Value.CanPerform(_timing))
                item.Value.DoEffects(uid, EntityManager);
        }
    }

    private void UpdateSickness()
    {
        var query = EntityQueryEnumerator<VomitSicknessComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.EndTime)
            {
                RemComp<VomitSicknessComponent>(uid);
                continue;
            }

            if (_timing.CurTime < comp.EndTime - TimeSpan.FromSeconds(comp.Duration / 2) || comp.Performed)
                continue;

            comp.Performed = true;
            if (comp.Level >= PlagueVomitLevel.Vomit)
                _vomit.Vomit(uid);
            if (comp.Level >= PlagueVomitLevel.Blood && TryComp<BloodstreamComponent>(uid, out var bloodstream))
            {
                Solution sol = new(bloodstream.BloodReagent, 20f);
                _puddle.TrySpillAt(Transform(uid).Coordinates, sol, out _, false);
            }

            RemComp<VomitSicknessComponent>(uid);
        }
    }

    private void UpdateClumsiness()
    {
        var query = EntityQueryEnumerator<MedievalPlagueClumsinessComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out var comp, out var input))
        {
            if (_timing.CurTime < comp.NextFall)
                continue;

            if (input.HeldMoveButtons == Shared.Movement.Systems.MoveButtons.None)
                continue;

            comp.NextFall = _timing.CurTime + TimeSpan.FromSeconds(1);

            if (_random.Prob(0.04f))
                _stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(1));
        }
    }

    private void UpdateDamagingClothing()
    {
        var query = EntityQueryEnumerator<DamageSelfByClothingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(15);

            var slotEnum = _inventory.GetSlotEnumerator(uid);
            while (slotEnum.MoveNext(out var container))
            {
                if (!container.ContainedEntity.HasValue)
                    continue;

                if (_tag.HasTag(container.ContainedEntity.Value, (ProtoId<TagPrototype>)"ClothingDamageWeakSkin"))
                    _damageable.TryChangeDamage(uid, comp.Damage);
            }
        }
    }

    private void UpdateLungCancer()
    {
        var query = EntityQueryEnumerator<LungsCancerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.EndTime)
            {
                comp.Active = false;
                continue;
            }

            if (_timing.CurTime < comp.NextEffect)
                continue;

            comp.NextEffect = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(comp.Delay.Min, comp.Delay.Max));
            comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(comp.Duration.Min, comp.Duration.Max));
            comp.Active = true;
        }
    }
}
