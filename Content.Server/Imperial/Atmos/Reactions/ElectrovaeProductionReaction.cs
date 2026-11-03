using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Imperial.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ElectrovaeProductionReaction : IGasReactionEffect
{
    public ReactionResult React(
        GasMixture mixture,
        IGasMixtureHolder? holder,
        AtmosphereSystem atmosphereSystem,
        float heatScale)
    {
        var initialNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var initialNitrousOxide = mixture.GetMoles(Gas.NitrousOxide);
        var initialWaterVapor = mixture.GetMoles(Gas.WaterVapor);
        var initialCarbonDioxide = mixture.GetMoles(Gas.CarbonDioxide);
        var totalMoles = mixture.TotalMoles;

        if (totalMoles <= 0f)
            return ReactionResult.NoReaction;

        var catalystFactor = Math.Clamp(
            initialNitrogen / totalMoles / Atmospherics.ElectrovaeProductionNitrogenRatio,
            0f,
            1f);
        var inhibitorFactor = Math.Clamp(
            1f - initialCarbonDioxide / totalMoles / Atmospherics.ElectrovaeProductionCarbonDioxideInhibitorSensitivity,
            0f,
            1f);

        var possibleSets = Math.Min(
            initialWaterVapor / Atmospherics.ElectrovaeProductionWaterVaporRatio,
            initialNitrousOxide / Atmospherics.ElectrovaeProductionNitrousOxideRatio);
        possibleSets *= catalystFactor * inhibitorFactor;

        var temperatureEfficiency = CalculateTemperatureEfficiency(mixture.Temperature);
        if (catalystFactor * inhibitorFactor * temperatureEfficiency < Atmospherics.ElectrovaeProductionMinimumEfficiency)
            return ReactionResult.NoReaction;

        var reactionSets = possibleSets * temperatureEfficiency;
        if (reactionSets <= 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.NitrousOxide, -reactionSets * Atmospherics.ElectrovaeProductionNitrousOxideRatio);
        mixture.AdjustMoles(Gas.WaterVapor, -reactionSets * Atmospherics.ElectrovaeProductionWaterVaporRatio);
        mixture.AdjustMoles(Gas.Electrovae, reactionSets);
        mixture.AdjustMoles(Gas.Oxygen, reactionSets);

        return ReactionResult.Reacting;
    }

    private static float CalculateTemperatureEfficiency(float temperature)
    {
        if (temperature >= Atmospherics.ElectrovaeProductionMaxTemperature)
            return 1f;

        var normalized = (temperature - Atmospherics.ElectrovaeProductionMinTemperature) /
                         (Atmospherics.ElectrovaeProductionMaxTemperature - Atmospherics.ElectrovaeProductionMinTemperature);
        return Math.Clamp(MathF.Pow(normalized, Atmospherics.ElectrovaeProductionTemperatureExponent), 0f, 1f);
    }
}
