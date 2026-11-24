using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Imperial.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ElectrovaeChargeReaction : IGasReactionEffect
{
    public ReactionResult React(
        GasMixture mixture,
        IGasMixtureHolder? holder,
        AtmosphereSystem atmosphereSystem,
        float heatScale)
    {
        if (holder is not TileAtmosphere tile)
            return ReactionResult.NoReaction;

        var initialMoles = mixture.GetMoles(Gas.Electrovae);
        var efficiency = CalculateTemperatureEfficiency(mixture.Temperature);
        if (efficiency < Atmospherics.ElectrovaeChargeMinimumEfficiency)
            return ReactionResult.NoReaction;

        var chargedMoles = Math.Min(initialMoles * efficiency * Atmospherics.ElectrovaeChargeRateMultiplier, initialMoles);
        if (chargedMoles <= 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Electrovae, -chargedMoles);
        mixture.AdjustMoles(Gas.ChargedElectrovae, chargedMoles);

        var intensity = Math.Min(
            mixture.GetMoles(Gas.ChargedElectrovae) / Atmospherics.ChargedElectrovaeIntensityDivisor,
            1f);
        atmosphereSystem.ChargedElectrovaeExpose(tile.GridIndex, tile, intensity);
        atmosphereSystem.AddHeat(mixture, -chargedMoles * Atmospherics.ElectrovaeChargeEnergyPerMole);

        return ReactionResult.Reacting;
    }

    private static float CalculateTemperatureEfficiency(float temperature)
    {
        if (temperature >= Atmospherics.ElectrovaeChargeMaxTemperature)
            return 1f;

        var normalized = (temperature - Atmospherics.ElectrovaeChargeMinTemperature) /
                         (Atmospherics.ElectrovaeChargeMaxTemperature - Atmospherics.ElectrovaeChargeMinTemperature);
        return MathF.Pow(normalized, Atmospherics.ElectrovaeChargeTemperatureExponent);
    }
}
