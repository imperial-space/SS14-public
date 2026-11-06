using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>mob/living/diseases: болезни, которыми болеет существо.</summary>
[RegisterComponent]
public sealed partial class DiseasedComponent : Component
{
    [ViewVariables]
    public List<DiseaseInstance> Diseases = new();

    [DataField]
    public TimeSpan ActivateSymptomCoughDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ActivateSymptomHeadacheDuration = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan ActivateSymptomDizzinessTime = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan ActivateSymptomDizzinessBoozePowerDelay = TimeSpan.FromSeconds(80);

    [DataField]
    public TimeSpan DeafTimersDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ActivateSymptomVisionLossDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan ActivateSymptomVoiceChangeDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float StageActChance = 0.5f;

    [DataField]
    public float ActivateSymptomCoughChance = 0.3f;

    [DataField]
    public float ActivateSymptomSneezeBaseThrowSpeed = 10f;

    [DataField]
    public float ActivateSymptomItchingChance = 100f;

    [DataField]
    public float ActivateSymptomHeadacheChance = 100f;

    [DataField]
    public float ActivateSymptomFeverChance = 100f;

    [DataField]
    public float ActivateSymptomChillsChance = 100f;

    [DataField]
    public float ActivateSymptomChokingChance = 100f;

    [DataField]
    public float ActivateSymptomAsphyxiationChance = 100f;

    [DataField]
    public float ActivateSymptomAsphyxiationAmount = 3f;

    [DataField]
    public float ActivateSymptomOxygenAmount = -5f;

    [DataField]
    public float ActivateSymptomOxygenAmount2 = -3f;

    [DataField]
    public float ActivateSymptomChance = 50f;

    [DataField]
    public TimeSpan ActivateSymptomSeconds = TimeSpan.FromSeconds(10f);

    [DataField]
    public float ActivateSymptomSeconds2 = 20f;

    [DataField]
    public float ActivateSymptomRange = 6f;

    [DataField]
    public float ActivateSymptomNarcolepsyChance = 50f;

    [DataField]
    public TimeSpan ActivateSymptomNarcolepsySeconds = TimeSpan.FromSeconds(80f);

    [DataField]
    public float ActivateSymptomNarcolepsyRange = 6f;

    [DataField]
    public float ActivateSymptomMindRestorationAmount = -3f;

    [DataField]
    public float ActivateSymptomMindRestorationAmount2 = 5f;

    [DataField]
    public int ActivateSymptomVisionLossAmount = 2;

    [DataField]
    public float ActivateSymptomVisionLossAmount2 = 10f;

    [DataField]
    public int ActivateSymptomSensoryRestorationAmount = -2;

    [DataField]
    public float ActivateSymptomSensoryRestorationAmount2 = -2f;

    [DataField]
    public float ActivateSymptomVoiceChangeChance = 100f;

    [DataField]
    public float ActivateSymptomVomitingAmount = 5f;

    [DataField]
    public float ActivateSymptomWeightLossAmount = -15f;

    [DataField]
    public float ActivateSymptomBeardChance = 100f;

    [DataField]
    public float ActivateSymptomSheddingChance = 100f;

    [DataField]
    public float ActivateSymptomPolyvitiligoChance = 100f;

    [DataField]
    public float ActivateSymptomDisfigurationChance = 100f;

    [DataField]
    public float ActivateSymptomYouthChance = 10f;

    [DataField]
    public float ActivateSymptomBleedingChance = 100f;

    [DataField]
    public float ActivateSymptomBleedingAmount = 2f;

    [DataField]
    public float ActivateSymptomFleshDeathAmount = 2f;

    [DataField]
    public float ActivateSymptomGeneticMutationChance = 100f;

    [DataField]
    public float ActivateHealSymptomHealPlasmaAmount = 2f;
}
