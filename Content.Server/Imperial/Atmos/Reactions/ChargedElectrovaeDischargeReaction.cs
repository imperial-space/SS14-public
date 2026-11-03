using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Imperial.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ChargedElectrovaeDischargeReaction : IGasReactionEffect
{
    public ReactionResult React(
        GasMixture mixture,
        IGasMixtureHolder? holder,
        AtmosphereSystem atmosphereSystem,
        float heatScale)
    {
        if (holder is not TileAtmosphere tile)
            return ReactionResult.NoReaction;

        const float decayConstant = 0.025f;

        var chargedMoles = mixture.GetMoles(Gas.ChargedElectrovae);
        var intensity = Math.Min(chargedMoles / Atmospherics.ChargedElectrovaeIntensityDivisor, 1f);
        atmosphereSystem.ChargedElectrovaeExpose(tile.GridIndex, tile, intensity);

        var decayAmount = chargedMoles * decayConstant;
        mixture.AdjustMoles(Gas.ChargedElectrovae, -decayAmount);
        mixture.AdjustMoles(Gas.Electrovae, decayAmount);

        return ReactionResult.Reacting;
    }
}
