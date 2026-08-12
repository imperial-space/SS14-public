using System.Linq;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Передаёт цели приватное указание и хранит ограниченное окно взаимодействия.
/// </summary>
public sealed class DeathNoteGuidanceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public DeathNotePresetExecutionResult Begin(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        DeathNoteGuidedScenarioType expectedScenario)
    {
        if (parameters.GuidedScenario != expectedScenario ||
            parameters.GuidanceText is not { } guidance ||
            parameters.PreludeDuration <= TimeSpan.Zero ||
            !HasValidGuidedParameters(expectedScenario, parameters))
        {
            return DeathNotePresetExecutionResult.Failed("Guided scenario parameters are incomplete.");
        }

        if (!_players.TryGetSessionByEntity(context.Target, out var session))
            return DeathNotePresetExecutionResult.Failed("Guided scenario target has no connected player session.");

        // Одно физическое взаимодействие нельзя однозначно отнести к нескольким
        // параллельным указаниям, поэтому на цели одновременно действует только одно.
        if (TryComp(context.Target, out DeathNoteGuidedScenarioComponent? existingGuide) &&
            existingGuide.EntryId != context.EntryId)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Target already has another active guided Death Note scenario.");
        }

        var guide = EnsureComp<DeathNoteGuidedScenarioComponent>(context.Target);
        guide.EntryId = context.EntryId;
        guide.Scenario = expectedScenario;
        guide.ExpiresAt = _timing.CurTime + parameters.PreludeDuration;
        guide.PersistentUntilTriggered = false;
        guide.Triggered = false;
        ConfigureGuide(guide, parameters);

        RaiseNetworkEvent(
            new DeathNoteInfluenceMessage(Loc.GetString(guidance), false),
            session);
        return DeathNotePresetExecutionResult.Succeeded("Private guided scenario was delivered to the target.");
    }

    public DeathNotePresetExecutionResult BeginSilent(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        DeathNoteGuidedScenarioType expectedScenario)
    {
        if (parameters.GuidedScenario != expectedScenario ||
            parameters.PreludeDuration <= TimeSpan.Zero ||
            !HasValidGuidedParameters(expectedScenario, parameters))
        {
            return DeathNotePresetExecutionResult.Failed("Silent guided scenario parameters are incomplete.");
        }

        // Безмолвный сценарий использует тот же взаимоисключающий слот взаимодействия.
        if (TryComp(context.Target, out DeathNoteGuidedScenarioComponent? existingGuide) &&
            existingGuide.EntryId != context.EntryId)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Target already has another active guided Death Note scenario.");
        }

        var guide = EnsureComp<DeathNoteGuidedScenarioComponent>(context.Target);
        guide.EntryId = context.EntryId;
        guide.Scenario = expectedScenario;
        guide.ExpiresAt = _timing.CurTime + parameters.PreludeDuration;
        guide.PersistentUntilTriggered = false;
        guide.Triggered = false;
        ConfigureGuide(guide, parameters);

        return DeathNotePresetExecutionResult.Succeeded("Silent guided scenario was armed on the target.");
    }

    public DeathNotePresetExecutionResult BeginPersistentSilent(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        DeathNoteGuidedScenarioType expectedScenario)
    {
        if (parameters.GuidedScenario != expectedScenario ||
            !HasValidGuidedParameters(expectedScenario, parameters))
        {
            return DeathNotePresetExecutionResult.Failed("Persistent silent scenario parameters are incomplete.");
        }

        if (TryComp(context.Target, out DeathNoteGuidedScenarioComponent? existingGuide) &&
            existingGuide.EntryId != context.EntryId)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Target already has another active guided Death Note scenario.");
        }

        var guide = EnsureComp<DeathNoteGuidedScenarioComponent>(context.Target);
        guide.EntryId = context.EntryId;
        guide.Scenario = expectedScenario;
        guide.ExpiresAt = TimeSpan.Zero;
        guide.PersistentUntilTriggered = true;
        guide.Triggered = false;
        ConfigureGuide(guide, parameters);

        return DeathNotePresetExecutionResult.Succeeded(
            "Persistent silent guided scenario was armed on the target.");
    }

    public DeathNotePresetExecutionResult Finish(
        in DeathNotePresetExecutionContext context,
        DeathNoteGuidedScenarioType expectedScenario)
    {
        if (!TryComp(context.Target, out DeathNoteGuidedScenarioComponent? guide) ||
            guide.EntryId != context.EntryId ||
            guide.Scenario != expectedScenario)
        {
            return DeathNotePresetExecutionResult.Failed("Target did not trigger the guided scenario in time.");
        }

        var triggered = guide.Triggered;
        RemComp<DeathNoteGuidedScenarioComponent>(context.Target);
        return triggered
            ? DeathNotePresetExecutionResult.Succeeded("Target triggered the guided scenario.")
            : DeathNotePresetExecutionResult.Failed("Target did not trigger the guided scenario in time.");
    }

    private static void ConfigureGuide(
        DeathNoteGuidedScenarioComponent guide,
        DeathNotePresetParameters parameters)
    {
        DeathNoteDamageHelper.TryCreateRange(
            parameters,
            out guide.MinimumDamage,
            out guide.MaximumDamage);
        guide.ImpactCount = parameters.ImpactCount;
        guide.ImpactInterval = parameters.ImpactInterval;
        guide.AirlockForceCloseDelay = parameters.AirlockForceCloseDelay;
        guide.DoorCloseStageDuration = parameters.DoorCloseStageDuration;
        guide.VendingFallDuration = parameters.VendingFallDuration;
        guide.CleanupGracePeriod = parameters.GuidedCleanupGracePeriod;
        guide.EffectTrackingDuration = parameters.EffectTrackingDuration;
        guide.AirlockAutoCloseDelayModifier = parameters.AirlockAutoCloseDelayModifier;
        guide.DoorwayImpactRadius = parameters.DoorwayImpactRadius;
        guide.VendingImpactSound = parameters.VendingImpactSound;
        guide.ConsumablePoisons.Clear();
        guide.ConsumablePoisons.AddRange(parameters.ConsumablePoisons);
        guide.PoisonedConsumableLimit = parameters.PoisonedConsumableLimit;
        guide.PoisonedConsumables.Clear();
    }

    private static bool HasValidGuidedParameters(
        DeathNoteGuidedScenarioType scenario,
        DeathNotePresetParameters parameters)
    {
        if (parameters.GuidedCleanupGracePeriod < TimeSpan.Zero ||
            parameters.EffectTrackingDuration <= TimeSpan.Zero)
            return false;

        switch (scenario)
        {
            case DeathNoteGuidedScenarioType.AirlockAccident:
                return DeathNoteDamageHelper.HasValidDamageAndRange(parameters) &&
                       parameters.AirlockForceCloseDelay > TimeSpan.Zero &&
                       parameters.DoorCloseStageDuration > TimeSpan.Zero &&
                       parameters.AirlockAutoCloseDelayModifier >= 0f &&
                       parameters.DoorwayImpactRadius > 0f;
            case DeathNoteGuidedScenarioType.DisposalCatastrophe:
                return DeathNoteDamageHelper.HasValidDamageAndRange(parameters) &&
                       parameters.ImpactCount > 0 &&
                       parameters.ImpactInterval > TimeSpan.Zero;
            case DeathNoteGuidedScenarioType.VendingMachineCrush:
                return DeathNoteDamageHelper.HasValidDamageAndRange(parameters) &&
                       parameters.VendingFallDuration > TimeSpan.Zero;
            case DeathNoteGuidedScenarioType.PoisonedFood:
            case DeathNoteGuidedScenarioType.PoisonedDrink:
                return parameters.PoisonedConsumableLimit > 0 &&
                       parameters.ConsumablePoisons.Count > 0 &&
                       parameters.ConsumablePoisons.All(option =>
                           option.MinimumQuantity > Content.Shared.FixedPoint.FixedPoint2.Zero &&
                           option.MaximumQuantity >= option.MinimumQuantity);
            default:
                return false;
        }
    }
}
