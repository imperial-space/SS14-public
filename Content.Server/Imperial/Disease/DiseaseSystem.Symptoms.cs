using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Shared.Body.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Disease;
using Content.Shared.Jittering;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// Симптомы продвинутых болезней 1 в 1 с SS13 (advance/symptoms/*.dm).
/// Каждый симптом срабатывает по своему таймеру и смотрит на стадию болезни и её характеристики.
/// </summary>
public sealed partial class DiseaseSystem
{
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> SlashType = "Slash";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> CellularType = "Cellular";
    private static readonly ProtoId<DamageTypePrototype> BloodlossType = "Bloodloss";

    private static readonly ProtoId<OrganCategoryPrototype> BrainCategory = "Brain";
    private static readonly ProtoId<OrganCategoryPrototype> EarsCategory = "Ears";
    private static readonly ProtoId<OrganCategoryPrototype> EyesCategory = "Eyes";


    private void InitializeSymptoms()
    {
        SubscribeLocalEvent<Content.Server.Chat.Systems.ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    /// <summary>Глухой не слышит чужую речь.</summary>
    private void OnExpandRecipients(Content.Server.Chat.Systems.ExpandICChatRecipientsEvent args)
    {
        var deaf = new List<Robust.Shared.Player.ICommonSession>();
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer || session.AttachedEntity is not { } listener || listener == args.Source)
                continue;

            if (HasComp<DiseaseDeafComponent>(listener))
                deaf.Add(session);
        }

        foreach (var session in deaf)
        {
            args.Recipients.Remove(session);
        }
    }

    /// <summary>advance/stage_act(): запуск симптомов и их срабатывание.</summary>
    private void ActivateSymptoms(Entity<DiseasedComponent> ent, DiseaseInstance disease, TimeSpan now)
    {
        if (!disease.Advanced || disease.Symptoms.Count == 0)
            return;

        if (!disease.Started)
        {
            disease.Started = true;
            foreach (var symptom in disease.Symptoms)
            {
                if (symptom.Neutered)
                    continue;

                StartSymptom(ent, disease, symptom);
                symptom.NextActivation = now + RollDelay(symptom);
            }

            OnStageChanged(ent, disease);
        }

        foreach (var symptom in disease.Symptoms)
        {
            if (symptom.Neutered || !_proto.TryIndex(symptom.Proto, out var proto))
                continue;

            if (proto.RequiredOrgan is { } organ && !HasOrgan(ent, organ))
                continue;

            // Своё лекарство гасит симптом и приближает излечение болезни.
            if (proto.Cure is { } cure)
            {
                if (HasReagent(ent, cure))
                {
                    symptom.Remedied = true;
                    continue;
                }

                symptom.Remedied = false;
            }

            if (now < symptom.NextActivation)
                continue;

            symptom.NextActivation = now + RollDelay(symptom);
            ActivateSymptom(ent, disease, symptom, proto);
        }
    }

    private TimeSpan RollDelay(SymptomInstance symptom)
    {
        var proto = _proto.Index(symptom.Proto);
        var min = symptom.DelayMin ?? proto.DelayMin;
        var max = symptom.DelayMax ?? proto.DelayMax;
        return TimeSpan.FromSeconds(_random.NextFloat(min, Math.Max(min, max)));
    }

    /// <summary>Start(): пороги характеристик включают дополнительные эффекты.</summary>
    private void StartSymptom(Entity<DiseasedComponent> ent, DiseaseInstance disease, SymptomInstance symptom)
    {
        symptom.Power = 1;
        symptom.Unlocked.Clear();
        symptom.DelayMin = null;
        symptom.DelayMax = null;
        symptom.SuppressWarning = false;
        symptom.MessageChance = -1;

        var resistance = disease.Resistance;
        var stealth = disease.Stealth;
        var speed = disease.StageRate;
        var transmit = disease.Transmittable;

        switch (symptom.Proto.Id)
        {
            case "SymptomCough":
                if (stealth >= 4) symptom.SuppressWarning = true;
                if (transmit >= 7) symptom.Unlocked.Add("range");
                if (resistance >= 11) symptom.Power = 1.5f;
                if (resistance >= 15) symptom.Power = 2;
                if (speed >= 6) symptom.DelayMax = 10;
                break;

            case "SymptomFever":
                if (resistance >= 5) { symptom.Power = 1.5f; symptom.Unlocked.Add("unsafe"); }
                if (resistance >= 10) symptom.Power = 2.5f;
                break;

            case "SymptomChills":
                if (speed >= 5) { symptom.Power = 1.5f; symptom.Unlocked.Add("unsafe"); }
                if (speed >= 10) symptom.Power = 2.5f;
                break;

            case "SymptomItching":
                if (transmit >= 6) { symptom.DelayMin = 1; symptom.DelayMax = 4; }
                if (speed >= 7) symptom.Unlocked.Add("scratch");
                break;

            case "SymptomSneeze":
                if (transmit >= 9) symptom.Unlocked.Add("range");
                if (stealth >= 4) symptom.SuppressWarning = true;
                if (speed >= 17) symptom.Unlocked.Add("launch");
                break;

            case "SymptomHeadache":
                if (stealth >= 4) symptom.MessageChance = 50;
                if (speed >= 6) symptom.Power = 2;
                if (speed >= 9) { symptom.DelayMin = 30; symptom.DelayMax = 60; symptom.Power = 3; }
                break;

            case "SymptomConfusion":
                if (speed >= 6) symptom.Unlocked.Add("illiterate");
                if (resistance >= 6) symptom.Unlocked.Add("brain");
                if (transmit >= 6) symptom.Power = 1.5f;
                if (stealth >= 4) symptom.SuppressWarning = true;
                break;

            case "SymptomDizziness":
                if (stealth >= 4) symptom.SuppressWarning = true;
                if (transmit >= 6) symptom.Power = 2;
                break;

            case "SymptomDeafness":
                if (stealth >= 4) symptom.SuppressWarning = true;
                if (resistance >= 9) symptom.Unlocked.Add("permanent");
                break;

            case "SymptomChoking":
                if (speed >= 8) { symptom.DelayMin = 7; symptom.DelayMax = 24; }
                if (stealth >= 4) symptom.SuppressWarning = true;
                break;

            case "SymptomAsphyxiation":
                if (speed >= 8) symptom.Unlocked.Add("paralysis");
                if (transmit >= 8) symptom.Power = 2;
                break;

            case "SymptomFire":
                if (speed >= 4) symptom.Power = 1.5f;
                if (speed >= 8) symptom.Power = 2;
                if (stealth >= 4) symptom.SuppressWarning = true;
                if (transmit >= 8) symptom.Unlocked.Add("infective");
                break;

            case "SymptomAlkali":
                if (resistance >= 9) { symptom.Power = 2; symptom.DelayMin = 50; symptom.DelayMax = 140; }
                if (speed >= 8) symptom.Unlocked.Add("explosion");
                if (transmit >= 8) symptom.Unlocked.Add("chems");
                break;

            case "SymptomFleshEating":
                if (resistance >= 7) symptom.Unlocked.Add("bleed");
                if (transmit >= 8) symptom.Unlocked.Add("pain");
                break;

            case "SymptomFleshDeath":
                if (stealth >= 5) symptom.SuppressWarning = true;
                if (speed >= 7) symptom.Unlocked.Add("chems");
                break;

            case "SymptomHallucigen":
                if (stealth >= 4) { symptom.Unlocked.Add("fake"); symptom.MessageChance = 50; }
                if (speed >= 7) symptom.Power = 2;
                break;

            case "SymptomNarcolepsy":
                if (transmit >= 4) symptom.Unlocked.Add("yawn");
                if (speed >= 10) { symptom.DelayMin = 20; symptom.DelayMax = 45; }
                break;

            case "SymptomVomiting":
                if (resistance >= 9) symptom.Unlocked.Add("blood");
                if (transmit >= 9) symptom.Unlocked.Add("projectile");
                if (stealth >= 4) symptom.SuppressWarning = true;
                break;

            case "SymptomVisionLoss":
                if (resistance >= 12) symptom.Unlocked.Add("detach");
                if (stealth >= 4) symptom.SuppressWarning = true;
                break;

            case "SymptomVoiceChange":
                if (transmit >= 14) symptom.Unlocked.Add("aphasia");
                if (speed >= 7) { symptom.DelayMin = 30; symptom.DelayMax = 60; }
                if (stealth >= 3) symptom.SuppressWarning = true;
                break;

            case "SymptomBleeding":
                if (speed >= 9) symptom.Unlocked.Add("easybleed");
                if (stealth >= 3) symptom.SuppressWarning = true;
                break;

            case "SymptomWeightLoss":
                if (stealth >= 4) symptom.SuppressWarning = true;
                break;

            case "SymptomGeneticMutation":
                if (stealth >= 5) symptom.Unlocked.Add("negative");
                if (speed >= 10) { symptom.DelayMin = 20; symptom.DelayMax = 40; }
                if (resistance >= 8) symptom.Unlocked.Add("proof");
                if (resistance >= 14) symptom.Unlocked.Add("noreset");
                break;

            case "SymptomThermoregulation":
                if (resistance >= 8) symptom.Power = 2;
                break;

            case "SymptomOxygen":
                if (resistance >= 8) symptom.Unlocked.Add("blood");
                break;

            case "SymptomMindRestoration":
                if (resistance >= 6) symptom.Unlocked.Add("minor");
                if (resistance >= 9) symptom.Unlocked.Add("severe");
                if (transmit >= 8) symptom.Unlocked.Add("alcohol");
                break;

            case "SymptomHealStarlight":
                if (transmit >= 6) symptom.Unlocked.Add("nopenalty");
                if (speed >= 6) symptom.Power = 2;
                break;

            case "SymptomHealChem":
                if (speed >= 6) symptom.Unlocked.Add("food");
                if (resistance >= 7) symptom.Power = 2;
                break;

            case "SymptomHealMetabolism":
                if (speed >= 10) symptom.Unlocked.Add("triple");
                if (stealth >= 3) symptom.Unlocked.Add("lesshunger");
                break;

            case "SymptomHealDarkness":
                if (speed >= 8) symptom.Power = 2;
                break;

            case "SymptomHealComa":
                if (speed >= 7) symptom.Power = 1.5f;
                if (resistance >= 4) symptom.Unlocked.Add("stabilize");
                if (stealth >= 2) symptom.Unlocked.Add("deathgasp");
                break;

            case "SymptomHealWater":
                if (resistance >= 5) symptom.Unlocked.Add("slow");
                if (speed >= 7) symptom.Power = 2;
                break;

            case "SymptomHealPlasma":
                if (transmit >= 6) symptom.Unlocked.Add("temperature");
                if (speed >= 7) symptom.Power = 2;
                break;

            case "SymptomHealRadiation":
                if (resistance >= 7) symptom.Power = 2;
                break;

            case "SymptomAggressiveHealing":
                symptom.Power = Math.Max(1, disease.SeverityValue);
                break;
        }
    }

    /// <summary>on_stage_change(): постоянные эффекты, зависящие от стадии.</summary>
    private void OnStageChanged(Entity<DiseasedComponent> ent, DiseaseInstance disease)
    {
        if (!disease.Advanced)
            return;

        foreach (var symptom in disease.Symptoms)
        {
            if (symptom.Neutered)
                continue;

            switch (symptom.Proto.Id)
            {
                case "SymptomFever":
                    SetTemperatureChange(ent, disease, symptom, 6);
                    break;
                case "SymptomChills":
                    SetTemperatureChange(ent, disease, symptom, -6);
                    break;
                case "SymptomBleeding":
                    // На поздних стадиях кровь перестаёт сворачиваться.
                    if (disease.Stage >= 4)
                        EnsureComp<DiseaseBleedingComponent>(ent);
                    else
                        RemComp<DiseaseBleedingComponent>(ent);
                    break;
                case "SymptomDeafness":
                    if (disease.Stage < 5 || !symptom.Unlocked.Contains("permanent"))
                        RemComp<DiseaseDeafComponent>(ent);
                    break;
                case "SymptomOxygen":
                    if (disease.Stage >= 4)
                        EnsureComp<DiseaseNoBreathComponent>(ent);
                    else
                        RemComp<DiseaseNoBreathComponent>(ent);
                    break;
            }
        }
    }

    /// <summary>End(): снять всё, что симптом навесил на носителя.</summary>
    private void EndSymptoms(EntityUid uid, DiseaseInstance disease)
    {
        if (!disease.Advanced)
            return;

        foreach (var symptom in disease.Symptoms)
        {
            switch (symptom.Proto.Id)
            {
                case "SymptomFever":
                case "SymptomChills":
                    break;
                case "SymptomBleeding":
                    RemComp<DiseaseBleedingComponent>(uid);
                    break;
                case "SymptomDeafness":
                    RemComp<DiseaseDeafComponent>(uid);
                    break;
                case "SymptomOxygen":
                    RemComp<DiseaseNoBreathComponent>(uid);
                    break;
            }
        }
    }

    /// <summary>Activate(): основное действие симптома.</summary>
    private void ActivateSymptom(Entity<DiseasedComponent> ent, DiseaseInstance disease, SymptomInstance symptom, DiseaseSymptomPrototype proto)
    {
        var uid = ent.Owner;
        var stage = disease.Stage;
        var power = symptom.Power;
        var chance = symptom.MessageChance >= 0 ? symptom.MessageChance : proto.MessageChance;

        switch (symptom.Proto.Id)
        {
            // ─── Низкоуровневые ───────────────────────────────────────────
            case "SymptomCough":
                if (stage <= 3)
                {
                    Warn(uid, "disease-symptom-cough-mild", chance, symptom);
                    break;
                }

                TryEmote(uid, "Cough");
                AirborneSpread(uid, disease, symptom.Unlocked.Contains("range") ? 2 : 1);
                if (power >= 1.5f)
                    DropHeldItem(uid);
                if (power >= 2 && _random.Prob(0.3f))
                {
                    Danger(uid, "disease-symptom-cough-fit");
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(2));
                }
                break;

            case "SymptomSneeze":
                if (stage <= 2)
                {
                    Warn(uid, "disease-symptom-sneeze-mild", chance, symptom);
                    break;
                }

                TryEmote(uid, "Sneeze");
                AirborneSpread(uid, disease, symptom.Unlocked.Contains("range") ? 3 : 2, true, true);
                if (symptom.Unlocked.Contains("launch"))
                {
                    // Чихание такой силы отбрасывает носителя назад.
                    var dir = -Transform(uid).LocalRotation.ToWorldVec();
                    _throwing.TryThrow(uid, dir * 6, 10f, uid);
                }
                break;

            case "SymptomItching":
                if (symptom.Unlocked.Contains("scratch"))
                {
                    Warn(uid, "disease-symptom-itching-scratch", 100, symptom);
                    Damage(uid, SlashType, 1);
                }
                else
                {
                    Warn(uid, "disease-symptom-itching", 100, symptom);
                }
                break;

            case "SymptomHeadache":
                if (power < 2 && (_random.Prob(chance / 100f) || stage >= 4))
                    Warn(uid, "disease-symptom-headache", 100, symptom);
                if (power >= 2 && stage >= 4)
                {
                    Warn(uid, "disease-symptom-headache-bad", 100, symptom);
                    _stamina.TakeStaminaDamage(uid, 25);
                }
                if (power >= 3 && stage >= 5)
                {
                    Danger(uid, "disease-symptom-headache-cluster");
                    _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(3.5));
                }
                break;

            // ─── Температура ─────────────────────────────────────────────
            case "SymptomFever":
                Warn(uid, symptom.Unlocked.Contains("unsafe") && stage >= 4 ? "disease-symptom-fever-bad" : "disease-symptom-fever",
                    100, symptom);
                SetTemperatureChange(ent, disease, symptom, 6);
                break;

            case "SymptomChills":
                Warn(uid, symptom.Unlocked.Contains("unsafe") && stage >= 4 ? "disease-symptom-chills-bad" : "disease-symptom-chills",
                    100, symptom);
                SetTemperatureChange(ent, disease, symptom, -6);
                break;

            case "SymptomThermoregulation":
                // Возвращает температуру тела к норме.
                if (TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(uid, out var thermo))
                {
                    var normal = 310.15f;
                    var delta = (normal - thermo.CurrentTemperature) * 0.2f * power;
                    _temperature.ChangeHeat(uid, delta * thermo.SpecificHeat, true, thermo);
                }
                break;

            // ─── Дыхание ─────────────────────────────────────────────────
            case "SymptomChoking":
                if (stage <= 2)
                {
                    Warn(uid, "disease-symptom-choking-mild", chance, symptom);
                    break;
                }

                if (stage <= 4)
                {
                    Warn(uid, symptom.SuppressWarning ? "disease-symptom-choking-fake" : "disease-symptom-choking", 100, symptom);
                    Damage(uid, AsphyxiationType, _random.Next(6, 14));
                }
                else
                {
                    Danger(uid, "disease-symptom-choking-severe");
                    Damage(uid, AsphyxiationType, _random.Next(10, 19));
                }

                TryEmote(uid, "Gasp");
                break;

            case "SymptomAsphyxiation":
                if (stage == 3 || stage == 4)
                {
                    Warn(uid, "disease-symptom-asphyxiation", 100, symptom);
                    Damage(uid, AsphyxiationType, _random.Next(10, 16) * power);
                    TryEmote(uid, "Gasp");
                }
                else if (stage >= 5)
                {
                    Danger(uid, "disease-symptom-asphyxiation-severe");
                    Damage(uid, AsphyxiationType, _random.Next(15, 22) * power);
                    if (symptom.Unlocked.Contains("paralysis"))
                        InjectReagent(uid, "Pancuronium", 3);
                    TryEmote(uid, "Gasp");
                }
                break;

            case "SymptomOxygen":
                // Носителю не нужен воздух; на высоких стадиях восстанавливается и кровь.
                if (stage >= 4)
                {
                    EnsureComp<DiseaseNoBreathComponent>(uid);
                    Damage(uid, AsphyxiationType, -5);
                    if (symptom.Unlocked.Contains("blood"))
                        Damage(uid, BloodlossType, -3);
                }
                break;

            // ─── Голова ──────────────────────────────────────────────────
            case "SymptomConfusion":
                if (stage <= 4)
                {
                    Warn(uid, "disease-symptom-confusion-mild", chance, symptom);
                    break;
                }

                Danger(uid, "disease-symptom-confusion");
                _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(16 * power));
                if (symptom.Unlocked.Contains("brain"))
                    DamageOrgan(uid, BrainCategory, 3 * power);
                break;

            case "SymptomDizziness":
                if (stage <= 4)
                {
                    Warn(uid, "disease-symptom-dizzy-mild", chance, symptom);
                    break;
                }

                Danger(uid, "disease-symptom-dizzy");
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(60), true);
                if (power >= 2)
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(80));
                break;

            case "SymptomHallucigen":
                if (stage <= 2)
                    Warn(uid, symptom.Unlocked.Contains("fake") ? "disease-symptom-fake-healthy" : "disease-symptom-hallucigen-mild", chance, symptom);
                else if (stage <= 4)
                    Warn(uid, symptom.Unlocked.Contains("fake") ? "disease-symptom-fake-healthy" : "disease-symptom-hallucigen", chance, symptom);
                else
                {
                    Warn(uid, symptom.Unlocked.Contains("fake") ? "disease-symptom-fake-healthy" : "disease-symptom-hallucigen-severe", chance, symptom);
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(90 * power));
                }
                break;

            case "SymptomNarcolepsy":
                switch (stage)
                {
                    case 1:
                        Warn(uid, "disease-symptom-narcolepsy-1", 50, symptom);
                        break;
                    case 2:
                        Warn(uid, "disease-symptom-narcolepsy-2", 50, symptom);
                        break;
                    case 3:
                        Warn(uid, "disease-symptom-narcolepsy-3", 50, symptom);
                        AddDrowsiness(uid, 10);
                        break;
                    case 4:
                        Warn(uid, "disease-symptom-narcolepsy-4", 50, symptom);
                        AddDrowsiness(uid, 20);
                        if (symptom.Unlocked.Contains("yawn"))
                        {
                            TryEmote(uid, "Yawn");
                            AirborneSpread(uid, disease, 6);
                        }
                        break;
                    default:
                        Warn(uid, "disease-symptom-narcolepsy-5", 50, symptom);
                        AddDrowsiness(uid, 80);
                        if (symptom.Unlocked.Contains("yawn"))
                        {
                            TryEmote(uid, "Yawn");
                            AirborneSpread(uid, disease, 6);
                        }
                        break;
                }
                break;

            case "SymptomMindRestoration":
                // Чинит мозг и выводит алкоголь.
                DamageOrgan(uid, BrainCategory, -3);
                if (symptom.Unlocked.Contains("alcohol"))
                    PurgeReagent(uid, "Ethanol", 5);
                break;

            // ─── Органы чувств ───────────────────────────────────────────
            case "SymptomDeafness":
                if (stage is 3 or 4)
                {
                    Warn(uid, "disease-symptom-deafness-mild", chance, symptom);
                    break;
                }

                if (stage >= 5)
                {
                    if (symptom.Unlocked.Contains("permanent"))
                    {
                        if (!HasComp<DiseaseDeafComponent>(uid))
                        {
                            Danger(uid, "disease-symptom-deafness-permanent");
                            TryEmote(uid, "Scream");
                            EnsureComp<DiseaseDeafComponent>(uid);
                        }
                    }
                    else
                    {
                        Danger(uid, "disease-symptom-deafness");
                        EnsureComp<DiseaseDeafComponent>(uid);
                        _deafTimers[uid] = _timing.CurTime + TimeSpan.FromSeconds(30);
                    }
                }
                break;

            case "SymptomVisionLoss":
                if (stage <= 3)
                {
                    Warn(uid, "disease-symptom-vision-mild", chance, symptom);
                    break;
                }

                Danger(uid, "disease-symptom-vision");
                _blindable.AdjustEyeDamage(uid, 2);
                if (symptom.Unlocked.Contains("detach") && stage >= 5)
                {
                    DamageOrgan(uid, EyesCategory, 10);
                    _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectTemporaryBlindness", TimeSpan.FromSeconds(20));
                }
                break;

            case "SymptomSensoryRestoration":
                // Чинит глаза и уши.
                _blindable.AdjustEyeDamage(uid, -2);
                DamageOrgan(uid, EyesCategory, -2);
                DamageOrgan(uid, EarsCategory, -2);
                break;

            case "SymptomVoiceChange":
                if (symptom.Unlocked.Contains("aphasia") && stage >= 4)
                {
                    Danger(uid, "disease-symptom-voice-aphasia");
                    _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectScrambled", TimeSpan.FromSeconds(60));
                }
                else if (!symptom.SuppressWarning)
                {
                    Warn(uid, "disease-symptom-voice", 100, symptom);
                }
                break;

            // ─── Тело ────────────────────────────────────────────────────
            case "SymptomVomiting":
                if (stage <= 2)
                {
                    Warn(uid, "disease-symptom-vomit-mild", chance, symptom);
                    break;
                }

                Danger(uid, "disease-symptom-vomit");
                _vomit.Vomit(uid);
                if (symptom.Unlocked.Contains("blood"))
                    Damage(uid, BloodlossType, 5);
                break;

            case "SymptomWeightLoss":
                Warn(uid, "disease-symptom-weight-loss", symptom.SuppressWarning ? 10 : 50, symptom);
                _hunger.ModifyHunger(uid, -15);
                break;

            case "SymptomBeard":
                Warn(uid, "disease-symptom-beard", 100, symptom);
                break;

            case "SymptomShedding":
                Warn(uid, "disease-symptom-shedding", 100, symptom);
                break;

            case "SymptomPolyvitiligo":
                Warn(uid, "disease-symptom-polyvitiligo", 100, symptom);
                break;

            case "SymptomDisfiguration":
                Warn(uid, stage >= 5 ? "disease-symptom-disfiguration-severe" : "disease-symptom-disfiguration", 100, symptom);
                break;

            case "SymptomYouth":
                Warn(uid, "disease-symptom-youth", 10, symptom);
                break;

            case "SymptomBleeding":
                if (!symptom.SuppressWarning)
                {
                    if (stage is >= 2 and < 4)
                        Warn(uid, "disease-symptom-bleeding-mild", 100, symptom);
                    else if (stage >= 4)
                        Warn(uid, "disease-symptom-bleeding", 100, symptom);
                }

                if (stage >= 4)
                    Damage(uid, BloodlossType, 2);
                break;

            case "SymptomFleshEating":
                if (stage is 2 or 3)
                {
                    Warn(uid, "disease-symptom-flesh-eating-mild", chance, symptom);
                    break;
                }

                if (stage >= 4)
                {
                    Danger(uid, "disease-symptom-flesh-eating");
                    var damage = _random.Next(15, 26) * power;
                    Damage(uid, BluntType, damage);
                    if (symptom.Unlocked.Contains("pain"))
                        _stamina.TakeStaminaDamage(uid, damage * 2);
                    if (symptom.Unlocked.Contains("bleed"))
                        Damage(uid, BloodlossType, 5 * power);
                }
                break;

            case "SymptomFleshDeath":
                if (stage is 2 or 3)
                {
                    Warn(uid, "disease-symptom-flesh-death-mild", chance, symptom);
                    break;
                }

                if (stage >= 4)
                {
                    Warn(uid, "disease-symptom-flesh-death", chance / 2, symptom);
                    Damage(uid, BluntType, _random.Next(6, 11));
                    if (symptom.Unlocked.Contains("chems"))
                    {
                        InjectReagent(uid, "Heparin", 2);
                        InjectReagent(uid, "Lipolicide", 2);
                    }
                }
                break;

            // ─── Огонь ───────────────────────────────────────────────────
            case "SymptomFire":
                if (stage <= 2)
                    break;

                if (stage == 3)
                {
                    Warn(uid, "disease-symptom-fire-warn", chance, symptom);
                    break;
                }

                {
                    var advanced = stage > 4;
                    _flammable.AdjustFireStacks(uid, (advanced ? 3 : 1) * power, ignite: true);
                    Damage(uid, HeatType, (advanced ? 5 : 3) * power);
                    Danger(uid, advanced ? "disease-symptom-fire-severe" : "disease-symptom-fire");
                    TryEmote(uid, "Scream");
                    if (symptom.Unlocked.Contains("infective"))
                        AirborneSpread(uid, disease, advanced ? 4 : 2);
                }
                break;

            case "SymptomAlkali":
                if (stage == 3)
                {
                    Warn(uid, "disease-symptom-alkali-warn", chance, symptom);
                    break;
                }

                if (stage >= 4)
                {
                    var stacks = (stage >= 5 ? 8 : 6) * power;
                    _flammable.AdjustFireStacks(uid, stacks, ignite: true);
                    Damage(uid, HeatType, stage >= 5 ? stacks : stacks / 2);
                    Danger(uid, stage >= 5 ? "disease-symptom-alkali-severe" : "disease-symptom-alkali");
                    TryEmote(uid, "Scream");
                    if (symptom.Unlocked.Contains("chems"))
                        InjectReagent(uid, "ChlorineTrifluoride", 2 * power);
                }
                break;

            // ─── Генетика ────────────────────────────────────────────────
            case "SymptomGeneticMutation":
                if (stage >= 4)
                {
                    Warn(uid, "disease-symptom-genetic", 100, symptom);
                    RaiseLocalEvent(uid, new DiseaseActivateMutationEvent(symptom.Unlocked.Contains("negative")));
                }
                break;

            // ─── Приспособления ──────────────────────────────────────────
            case "SymptomViralAdaptation":
            case "SymptomViralEvolution":
            case "SymptomUndeadAdaptation":
            case "SymptomInorganicAdaptation":
                // Эти симптомы меняют только характеристики болезни.
                break;

            // ─── Лечащие ─────────────────────────────────────────────────
            default:
                if (symptom.Proto.Id.StartsWith("SymptomHeal") || symptom.Proto.Id == "SymptomAggressiveHealing")
                    ActivateHeal(ent, disease, symptom, stage);
                break;
        }
    }

    /// <summary>heal/Activate(): лечащие симптомы работают только на 4–5 стадии.</summary>
    private void ActivateHeal(Entity<DiseasedComponent> ent, DiseaseInstance disease, SymptomInstance symptom, int stage)
    {
        var uid = ent.Owner;
        if (symptom.Proto.Id != "SymptomAggressiveHealing" && stage < 4)
            return;

        var power = symptom.Power;

        switch (symptom.Proto.Id)
        {
            case "SymptomHealStarlight":
                // Лечит под звёздным светом, лучше всего — от яда.
                if (!IsNearSpace(uid))
                    return;
                Damage(uid, PoisonType, -4 * power);
                HealBrute(uid, power);
                break;

            case "SymptomHealChem":
                // Разлагает любые чужие химикаты в крови.
                var purged = PurgeAllReagents(uid, power);
                if (symptom.Unlocked.Contains("food"))
                    _hunger.ModifyHunger(uid, 0.3f * purged);
                break;

            case "SymptomHealMetabolism":
                _hunger.ModifyHunger(uid, symptom.Unlocked.Contains("lesshunger") ? -4 : -9);
                PurgeAllReagents(uid, symptom.Unlocked.Contains("triple") ? 3 : 2);
                break;

            case "SymptomHealDarkness":
                if (!IsInDarkness(uid))
                    return;
                HealBrute(uid, 2 * power);
                break;

            case "SymptomHealComa":
                // Лечит, пока носитель без сознания.
                if (_mobState.IsCritical(uid))
                    HealBrute(uid, power * 0.9f);
                else if (HasComp<SleepingComponent>(uid))
                    HealBrute(uid, power * 0.25f);
                break;

            case "SymptomHealWater":
                if (GetReagentAmount(uid, "Water") <= 0)
                    return;
                PurgeReagent(uid, "Water", symptom.Unlocked.Contains("slow") ? 0.5f : 2f);
                HealBrute(uid, power);
                break;

            case "SymptomHealPlasma":
                if (GetReagentAmount(uid, "Plasma") <= 0)
                    return;
                PurgeReagent(uid, "Plasma", 2);
                HealBrute(uid, power);
                if (symptom.Unlocked.Contains("temperature")
                    && TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(uid, out var temp))
                {
                    _temperature.ChangeHeat(uid, (310.15f - temp.CurrentTemperature) * 0.3f * temp.SpecificHeat, true, temp);
                }
                break;

            case "SymptomHealRadiation":
                HealBrute(uid, power);
                Damage(uid, PoisonType, -1 * power);
                break;

            case "SymptomAggressiveHealing":
                // Лечит тем сильнее, чем опаснее сама болезнь.
                HealBrute(uid, power);
                break;
        }
    }

    #region Вспомогательное для симптомов

    private readonly Dictionary<EntityUid, TimeSpan> _deafTimers = new();

    /// <summary>add_body_temperature_change(): лихорадка и озноб смещают температуру тела.</summary>
    private void SetTemperatureChange(Entity<DiseasedComponent> ent, DiseaseInstance disease, SymptomInstance symptom, float direction)
    {
        if (!TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(ent, out var temp))
            return;

        var target = 310.15f + direction * symptom.Power * disease.Stage;
        var delta = (target - temp.CurrentTemperature) * 0.25f;
        _temperature.ChangeHeat(ent, delta * temp.SpecificHeat, true, temp);
    }

    private void Warn(EntityUid uid, LocId message, float chance, SymptomInstance symptom)
    {
        if (symptom.SuppressWarning || !_random.Prob(Math.Clamp(chance / 100f, 0f, 1f)))
            return;

        _popup.PopupEntity(Loc.GetString(message), uid, uid);
    }

    private void Danger(EntityUid uid, LocId message)
    {
        _popup.PopupEntity(Loc.GetString(message), uid, uid, PopupType.LargeCaution);
    }

    private void Damage(EntityUid uid, ProtoId<DamageTypePrototype> type, float amount)
    {
        if (amount == 0)
            return;

        _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { [type.Id] = amount } }, true, false);
    }

    private void HealBrute(EntityUid uid, float amount)
    {
        if (!_proto.TryIndex(BruteGroup, out var group))
            return;

        _damageable.TryChangeDamage(uid, new DamageSpecifier(group, FixedPoint2.New(-amount)), true, false);
    }

    private void DamageOrgan(EntityUid uid, ProtoId<OrganCategoryPrototype> category, float amount)
    {
        if (!TryComp<BodyComponent>(uid, out var body))
            return;

        foreach (var organ in GetOrgans(uid, body))
        {
            if (organ.Comp.Category != category)
                continue;

            _damageable.TryChangeDamage(organ.Owner, new DamageSpecifier { DamageDict = { [BluntType.Id] = amount } }, true, false);
            return;
        }
    }

    private void InjectReagent(EntityUid uid, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (!_proto.HasIndex(reagent))
            return;

        var solution = new Solution();
        solution.AddReagent(reagent, FixedPoint2.New(amount));
        _bloodstream.TryAddToBloodstream(uid, solution);
    }

    private float PurgeReagent(EntityUid uid, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream)
            || !_solutions.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
            return 0;

        var removed = blood.RemoveReagent(reagent, FixedPoint2.New(amount));
        return (float) removed;
    }

    private float PurgeAllReagents(EntityUid uid, float amount)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream)
            || !_solutions.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
            return 0;

        var total = 0f;
        foreach (var reagent in blood.Contents.ToArray())
        {
            if (reagent.Reagent.Prototype == "Blood")
                continue;

            total += (float) blood.RemoveReagent(reagent.Reagent, FixedPoint2.New(amount));
        }

        return total;
    }

    private void AddDrowsiness(EntityUid uid, float seconds)
    {
        _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectDrowsiness", TimeSpan.FromSeconds(seconds));
    }

    private void TryEmote(EntityUid uid, string emote)
    {
        _chat.TryEmoteWithChat(uid, emote, ignoreActionBlocker: true);
    }

    private void DropHeldItem(EntityUid uid)
    {
        if (!TryComp<Content.Shared.Hands.Components.HandsComponent>(uid, out var hands))
            return;

        if (_hands.GetActiveItem((uid, hands)) is { } item)
            _hands.TryDrop((uid, hands), item, checkActionBlocker: false);
    }

    /// <summary>Рядом ли открытый космос — для «Звёздного конденсата».</summary>
    private bool IsNearSpace(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(grid, out var gridComp))
            return true;

        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                if (!_map.TryGetTileRef(grid, gridComp, center + new Vector2i(x, y), out var tile) || _turf.IsSpace(tile))
                    return true;
            }
        }

        return false;
    }

    [Dependency] private readonly Content.Shared.Hands.EntitySystems.SharedHandsSystem _hands = default!;
    [Dependency] private readonly Content.Shared.Chat.SharedChatSystem _chat = default!;
    [Dependency] private readonly Content.Shared.Throwing.ThrowingSystem _throwing = default!;

    /// <summary>Временная глухота проходит сама.</summary>
    private void UpdateDeafness(TimeSpan now)
    {
        foreach (var (uid, until) in _deafTimers.ToArray())
        {
            if (now < until && Exists(uid))
                continue;

            _deafTimers.Remove(uid);
            RemComp<DiseaseDeafComponent>(uid);
        }
    }

    #endregion
}

/// <summary>Симптом «Активатор спящей ДНК» просит генетику включить случайную мутацию.</summary>
public sealed class DiseaseActivateMutationEvent : EntityEventArgs
{
    public bool NegativeOnly { get; }

    public DiseaseActivateMutationEvent(bool negativeOnly)
    {
        NegativeOnly = negativeOnly;
    }
}
