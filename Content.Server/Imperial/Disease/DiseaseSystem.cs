using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Disease;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// Болезни и вирусы 1 в 1 с SS13 (code/datums/diseases/_disease.dm, _MobProcs.dm).
/// </summary>
public sealed partial class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>Тик Life(): SSmobs обрабатывает существ раз в 2 секунды.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    private const float SecondsPerTick = 2f;

    /// <summary>VIRUS_SYMPTOM_LIMIT.</summary>
    public const int SymptomLimit = 6;

    /// <summary>DISEASE_LIMIT: больше одной продвинутой болезни разом не держится.</summary>
    private const int AdvanceDiseaseLimit = 1;

    // DISEASE_CYCLES_*
    private const float CyclesPositive = 15;
    private const float CyclesNonThreat = 180;
    private const float CyclesMinor = 150;
    private const float CyclesMedium = 120;
    private const float CyclesDangerous = 90;
    private const float CyclesHarmful = 60;
    private const float CyclesBiohazard = 40;

    private const float RecoveryConstant = 0;
    private const float RecoveryScaling = 2;
    private const float PeakedRecoveryMultiplier = 1.2f;
    private const float SlowdownRecoveryBonus = 2;
    private const float SlowdownRecoveryBonusDuration = 120;
    private const float MalnutritionRecoveryPenalty = 3;
    private const float SatietyRecoveryMultiplier = 3;
    private const float GoodSleepingRecoveryBonus = 0.6f;
    private const float SleepingRecoveryMultiplier = 6;
    private const float FinalCureChanceMultiplier = 6;
    private const int SymptomOffsetDuration = 200;

    /// <summary>Реагент, замедляющий болезнь вдвое (TRAIT_VIRUS_RESISTANCE).</summary>
    private static readonly ProtoId<ReagentPrototype> Spaceacillin = "Spaceacillin";

    private TimeSpan _nextTick;

    /// <summary>Снимок болеющих на тик, чтобы заражение не ломало обход.</summary>
    private readonly List<Entity<DiseasedComponent>> _tickBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseasedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DiseasedComponent, ComponentShutdown>(OnDiseasedShutdown);

        InitializeSpread();
        InitializeSymptoms();
        InitializeAdvance();
        InitializeMachines();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateDraws(now);
        UpdateInjections(now);

        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        UpdateDeafness(now);

        // Заражение по ходу обхода добавляет болеющих, поэтому сначала снимаем список.
        _tickBuffer.Clear();
        var query = EntityQueryEnumerator<DiseasedComponent>();
        while (query.MoveNext(out var uid, out var diseased))
        {
            _tickBuffer.Add((uid, diseased));
        }

        foreach (var (uid, diseased) in _tickBuffer)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            // Течение болезни в SS13 идёт из Life() существа.
            for (var i = diseased.Diseases.Count - 1; i >= 0; i--)
            {
                if (i >= diseased.Diseases.Count)
                    continue;

                var disease = diseased.Diseases[i];
                TrySpreadOnBreath(uid, disease);

                if (!StageAct((uid, diseased), disease, now))
                    continue;

                ActivateSymptoms((uid, diseased), disease, now);
            }

            if (diseased.Diseases.Count == 0)
                RemCompDeferred<DiseasedComponent>(uid);
        }
    }

    #region Заражение

    /// <summary>CanContractDisease(): можно ли вообще подхватить эту болезнь.</summary>
    public bool CanContract(EntityUid uid, DiseaseInstance disease)
    {
        if (!HasComp<BodyComponent>(uid))
            return false;

        if (_mobState.IsDead(uid) && !disease.ProcessDead)
            return false;

        if (TryComp<DiseaseImmunityComponent>(uid, out var immunity) && immunity.Resistances.Contains(disease.GetId()))
            return false;

        if (HasDisease(uid, disease))
            return false;

        if (HasComp<DiseaseVirusImmuneComponent>(uid) && !disease.BypassesImmunity)
            return false;

        if (disease.RequiredOrgan is { } organ && !HasOrgan(uid, organ))
            return false;

        return true;
    }

    public bool HasDisease(EntityUid uid, DiseaseInstance disease)
    {
        if (!TryComp<DiseasedComponent>(uid, out var diseased))
            return false;

        var id = disease.GetId();
        foreach (var existing in diseased.Diseases)
        {
            if (existing.GetId() == id)
                return true;
        }

        return false;
    }

    /// <summary>ForceContractDisease(): заразить, не глядя на одежду и защиту.</summary>
    public bool TryInfect(EntityUid uid, DiseaseInstance disease, bool makeCopy = true)
    {
        if (!CanContract(uid, disease))
            return false;

        // advance/try_infect(): новая болезнь вытесняет более слабую, но иммунитета от этого не будет.
        if (disease.Advanced)
        {
            var diseasedComp = CompOrNull<DiseasedComponent>(uid);
            var advanced = diseasedComp?.Diseases.Where(d => d.Advanced).ToList() ?? new List<DiseaseInstance>();
            var replaceCount = advanced.Count + 1 - AdvanceDiseaseLimit;
            if (replaceCount > 0)
            {
                advanced.Sort((a, b) => a.Resistance.CompareTo(b.Resistance));
                for (var i = 0; i < replaceCount && i < advanced.Count; i++)
                {
                    var competitor = advanced[i];
                    if (competitor.BypassesImmunity)
                        continue;

                    if (disease.BypassesImmunity || disease.Transmittable > competitor.Resistance)
                        Cure(uid, competitor, false);
                    else
                        return false;
                }
            }
        }

        Infect(uid, disease, makeCopy);
        return true;
    }

    /// <summary>infect(): добавить болезнь без проверок.</summary>
    public void Infect(EntityUid uid, DiseaseInstance disease, bool makeCopy = true)
    {
        var instance = makeCopy ? disease.Copy() : disease;
        var diseased = EnsureComp<DiseasedComponent>(uid);
        diseased.Diseases.Add(instance);
        instance.NextTick = _timing.CurTime + TickInterval;

        if (instance.IncubationUntil == null && instance.Advanced && (instance.Visibility & DiseaseVisibility.Scanner) == 0)
            instance.IncubationUntil = null;
    }

    /// <summary>cure(): вылечить, при необходимости выдав иммунитет.</summary>
    public void Cure(EntityUid uid, DiseaseInstance disease, bool addResistance = true)
    {
        if (disease.Severity == DiseaseSeverity.Uncurable)
            return;

        EndSymptoms(uid, disease);

        if (addResistance && (disease.Flags & DiseaseFlags.CanResist) != 0)
        {
            var immunity = EnsureComp<DiseaseImmunityComponent>(uid);
            immunity.Resistances.Add(disease.GetId());
        }

        if (TryComp<DiseasedComponent>(uid, out var diseased))
        {
            diseased.Diseases.Remove(disease);
            if (diseased.Diseases.Count == 0)
                RemCompDeferred<DiseasedComponent>(uid);
        }
    }

    private void OnDiseasedShutdown(Entity<DiseasedComponent> ent, ref ComponentShutdown args)
    {
        foreach (var disease in ent.Comp.Diseases.ToArray())
        {
            EndSymptoms(ent, disease);
        }

        ent.Comp.Diseases.Clear();
    }

    #endregion

    #region Течение болезни

    /// <summary>stage_act(): стадии, лечение и естественное выздоровление.</summary>
    private bool StageAct(Entity<DiseasedComponent> ent, DiseaseInstance disease, TimeSpan now)
    {
        if (_mobState.IsDead(ent) && !disease.ProcessDead)
            return false;

        if (disease.RequiredOrgan is { } organ && !HasOrgan(ent, organ))
        {
            Cure(ent, disease, false);
            return false;
        }

        // Спейсациллин вдвое замедляет развитие болезни.
        var slowdown = HasReagent(ent, Spaceacillin) ? 0.5f : 1f;
        var badImmune = 1f;
        var sleeping = HasComp<SleepingComponent>(ent);
        var recoveryProb = 0f;

        var cureStatus = HasCure(ent, disease);
        if (cureStatus > 0)
        {
            var cureMod = disease.CureChance / badImmune;
            if (disease.Advanced)
                cureMod = cureMod * 2 * cureStatus;

            if ((disease.Flags & DiseaseFlags.Chronic) != 0 && Prob(cureMod))
            {
                UpdateStage(ent, disease, 1);
                _popup.PopupEntity(Loc.GetString("disease-chronic-alleviated"), ent, ent);
                return false;
            }

            if ((disease.Flags & DiseaseFlags.Curable) != 0 && Prob(cureMod))
            {
                if ((disease.Flags & DiseaseFlags.IncrementalCure) != 0)
                {
                    if (!UpdateStage(ent, disease, disease.Stage - 1))
                        return false;
                }
                else
                {
                    Cure(ent, disease);
                    return false;
                }
            }
        }

        if (disease.Stage == disease.MaxStages)
            disease.StagePeaked = true;

        if (Prob(disease.StageProbability * slowdown * badImmune))
            UpdateStage(ent, disease, Math.Min(disease.Stage + 1, disease.MaxStages));

        if ((disease.Flags & DiseaseFlags.Chronic) == 0 && (disease.Flags & DiseaseFlags.Curable) != 0 && !disease.BypassesImmunity)
        {
            var malnourished = IsMalnourished(ent);
            float cyclesToBeat;
            switch (disease.Severity)
            {
                case DiseaseSeverity.Positive:
                    if (slowdown < 1 || malnourished)
                    {
                        cyclesToBeat = Math.Max(RecoveryScaling, CyclesPositive);
                    }
                    else
                    {
                        // Полезная болезнь сама не проходит, пока носитель сыт и не лечится.
                        return !disease.Carrier;
                    }
                    break;
                case DiseaseSeverity.NonThreat:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesNonThreat);
                    break;
                case DiseaseSeverity.Minor:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesMinor);
                    break;
                case DiseaseSeverity.Medium:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesMedium);
                    break;
                case DiseaseSeverity.Dangerous:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesDangerous);
                    break;
                case DiseaseSeverity.Harmful:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesHarmful);
                    break;
                case DiseaseSeverity.Biohazard:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesBiohazard);
                    break;
                default:
                    cyclesToBeat = Math.Max(RecoveryScaling, CyclesNonThreat);
                    break;
            }

            // Каждый прожитый цикл приближает выздоровление, на высоких стадиях — сильнее.
            disease.PeakedCycles += (float) disease.Stage / disease.MaxStages;
            recoveryProb += RecoveryConstant + disease.PeakedCycles / (cyclesToBeat / RecoveryScaling);

            if (disease.StagePeaked)
                recoveryProb *= PeakedRecoveryMultiplier;

            if (slowdown < 1)
            {
                recoveryProb += Math.Clamp((1 - slowdown) * (SlowdownRecoveryBonus * 2)
                    * ((SlowdownRecoveryBonusDuration - disease.ChemicalOffsets) / SlowdownRecoveryBonusDuration), 0, SlowdownRecoveryBonus);
                disease.ChemicalOffsets = Math.Min(disease.ChemicalOffsets + 1, (int) SlowdownRecoveryBonusDuration);
            }

            if (malnourished)
                recoveryProb -= MalnutritionRecoveryPenalty;
            else if (IsWellFed(ent))
                recoveryProb += SatietyRecoveryMultiplier;

            if (!malnourished && sleeping)
            {
                // Хороший сон лечит: темнота, тишина и кровать помогают.
                if (IsInDarkness(ent))
                    recoveryProb += GoodSleepingRecoveryBonus;

                if (HasComp<DiseaseDeafComponent>(ent))
                    recoveryProb += GoodSleepingRecoveryBonus;

                if (IsOnBed(ent))
                    recoveryProb += GoodSleepingRecoveryBonus;

                recoveryProb *= SleepingRecoveryMultiplier;
            }

            recoveryProb = Math.Clamp(recoveryProb / badImmune, 0, 100);

            if (recoveryProb > 0)
            {
                var failureChance = 1 - GetRecoveryFailureChance(disease) / 100f;
                if (Prob(recoveryProb * failureChance))
                {
                    if (disease.Stage == 1 && _random.Prob(Math.Clamp(disease.CureChance * FinalCureChanceMultiplier / 100f, 0f, 1f)))
                    {
                        if (malnourished)
                        {
                            // Истощённый носитель иммунитета не получает, если не переболел до пика.
                            if (!disease.StagePeaked)
                            {
                                Cure(ent, disease, false);
                                return false;
                            }

                            Cure(ent, disease, _random.Prob(0.5f));
                            return false;
                        }

                        Cure(ent, disease);
                        return false;
                    }

                    UpdateStage(ent, disease, Math.Max(disease.Stage - 1, 1));
                }
            }

            // Сон и спейсациллин какое-то время гасят проявление симптомов.
            if (sleeping || slowdown != 1)
            {
                var offsetChance = 100 - Math.Min(100f * disease.SymptomOffsets / SymptomOffsetDuration,
                    100 - disease.CureChance * FinalCureChanceMultiplier);
                if (_random.Prob(Math.Clamp(offsetChance / 100f, 0f, 1f)))
                {
                    disease.SymptomOffsets = Math.Min(disease.SymptomOffsets + 1, SymptomOffsetDuration);
                    return false;
                }
            }
        }

        return !disease.Carrier;
    }

    /// <summary>update_stage().</summary>
    private bool UpdateStage(Entity<DiseasedComponent> ent, DiseaseInstance disease, int newStage)
    {
        disease.Stage = newStage;
        if (newStage == disease.MaxStages)
            disease.StagePeaked = true;

        if (disease.Stage <= 0)
        {
            Cure(ent, disease);
            return false;
        }

        OnStageChanged(ent, disease);
        return true;
    }

    /// <summary>has_cure(): для обычной болезни — есть ли реагенты, для продвинутой — доля подавленных симптомов.</summary>
    private float HasCure(EntityUid uid, DiseaseInstance disease)
    {
        if ((disease.Flags & (DiseaseFlags.Curable | DiseaseFlags.Chronic)) == 0)
            return 0;

        if (disease.Advanced)
        {
            var remedied = 0;
            var active = 0;
            foreach (var symptom in disease.Symptoms)
            {
                if (symptom.Remedied)
                    remedied++;
                else if (!symptom.Neutered)
                    active++;
            }

            if (remedied + active == 0)
                return 0;

            return (float) remedied / (remedied + active);
        }

        var found = 0;
        foreach (var cure in disease.Cures)
        {
            if (HasReagent(uid, cure))
                found++;
        }

        if (found == 0 || disease.NeedsAllCures && found < disease.Cures.Count)
            return 0;

        return 1;
    }

    /// <summary>get_recovery_failure_chance(): стойкая болезнь сопротивляется выздоровлению.</summary>
    private float GetRecoveryFailureChance(DiseaseInstance disease)
    {
        return disease.Advanced ? Math.Clamp(disease.Resistance * 1.5f, 0, 50) : 0;
    }

    #endregion

    #region Вспомогательное

    /// <summary>SPT_PROB: шанс в процентах за секунду, пересчитанный на тик.</summary>
    private bool Prob(float probPerSecond)
    {
        if (probPerSecond <= 0)
            return false;

        var chance = 1 - MathF.Pow(1 - Math.Clamp(probPerSecond / 100f, 0f, 1f), SecondsPerTick);
        return _random.Prob(chance);
    }

    public bool HasOrgan(EntityUid uid, ProtoId<OrganCategoryPrototype> category)
    {
        if (!TryComp<BodyComponent>(uid, out var body))
            return false;

        foreach (var organ in GetOrgans(uid, body))
        {
            if (organ.Comp.Category == category)
                return true;
        }

        return false;
    }

    private IEnumerable<Entity<OrganComponent>> GetOrgans(EntityUid uid, BodyComponent? body = null)
    {
        if (!Resolve(uid, ref body, false))
            yield break;

        foreach (var organ in body.Organs?.ContainedEntities ?? new List<EntityUid>())
        {
            if (TryComp<OrganComponent>(organ, out var comp))
                yield return (organ, comp);
        }
    }

    public bool HasReagent(EntityUid uid, ProtoId<ReagentPrototype> reagent, float minimum = 0)
    {
        return GetReagentAmount(uid, reagent) > minimum;
    }

    public float GetReagentAmount(EntityUid uid, ProtoId<ReagentPrototype> reagent)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return 0;

        var total = 0f;
        if (_solutions.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
            total += (float) blood.GetTotalPrototypeQuantity(reagent);
        if (_solutions.ResolveSolution(uid, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution, out var metabolites))
            total += (float) metabolites.GetTotalPrototypeQuantity(reagent);

        return total;
    }

    private bool IsMalnourished(EntityUid uid)
    {
        return TryComp<HungerComponent>(uid, out var hunger)
               && hunger.CurrentThreshold <= HungerThreshold.Starving;
    }

    private bool IsWellFed(EntityUid uid)
    {
        return TryComp<HungerComponent>(uid, out var hunger) && hunger.CurrentThreshold == HungerThreshold.Overfed;
    }

    /// <summary>get_lumcount() &lt;= LIGHTING_TILE_IS_DARK.</summary>
    private bool IsInDarkness(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        foreach (var light in _lookup.GetEntitiesInRange<Content.Shared.Light.Components.PoweredLightComponent>(
                     _transform.GetMapCoordinates(uid), 5))
        {
            if (light.Comp.On)
                return false;
        }

        _ = tile;
        return true;
    }

    /// <summary>Кровать, стол, простыня или подушка под спящим.</summary>
    private bool IsOnBed(EntityUid uid)
    {
        foreach (var bed in _lookup.GetEntitiesInRange<Content.Shared.Bed.Components.HealOnBuckleComponent>(
                     _transform.GetMapCoordinates(uid), 0.4f))
        {
            _ = bed;
            return true;
        }

        return false;
    }

    private void OnExamined(Entity<DiseasedComponent> ent, ref ExaminedEvent args)
    {
        // Болезни видны только через анализатор, обычный осмотр ничего не показывает.
    }

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;

    #endregion
}
