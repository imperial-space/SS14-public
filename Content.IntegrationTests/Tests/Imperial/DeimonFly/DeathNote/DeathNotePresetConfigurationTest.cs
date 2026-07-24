using System.Collections.Generic;
using Content.Server.Anomaly.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.ImmovableRod;
using Content.Server.Mining;
using Content.Server.NPC.HTN;
using Content.Shared.Anomaly.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNotePresetConfigurationTest : DeathNotePresetTestBase
{
    private static readonly EntProtoId StandardNotebook = "DeathNote";
    private static readonly EntProtoId IrreversibleNotebook = "DeathNoteUnrevivable";
    private static readonly EntProtoId SecondNotebook = "DeathNote2";
    private static readonly EntProtoId SecondIrreversibleNotebook = "DeathNoteUnrevivable2";
    private static readonly EntProtoId ThirdNotebook = "DeathNote3";
    private static readonly EntProtoId ThirdIrreversibleNotebook = "DeathNoteUnrevivable3";
    private static readonly EntProtoId ShinigamiNotebook = "DeathGodNote";
    private static readonly EntProtoId IrreversibleShinigamiNotebook = "DeathGodNoteUnrevivable";

    [Test]
    public async Task NotebookVariantsDifferOnlyByUnrevivablePolicy()
    {
        await SpawnTarget("DeathNote");

        await Server.WaitPost(() =>
        {
            var standard = Server.ProtoMan.Index<EntityPrototype>(StandardNotebook);
            var irreversible = Server.ProtoMan.Index<EntityPrototype>(IrreversibleNotebook);
            var second = Server.ProtoMan.Index<EntityPrototype>(SecondNotebook);
            var secondIrreversible = Server.ProtoMan.Index<EntityPrototype>(SecondIrreversibleNotebook);
            var third = Server.ProtoMan.Index<EntityPrototype>(ThirdNotebook);
            var thirdIrreversible = Server.ProtoMan.Index<EntityPrototype>(ThirdIrreversibleNotebook);
            var shinigami = Server.ProtoMan.Index<EntityPrototype>(ShinigamiNotebook);
            var shinigamiIrreversible =
                Server.ProtoMan.Index<EntityPrototype>(IrreversibleShinigamiNotebook);
            Assert.That(standard.TryGetComponent<DeathNoteComponent>(out var standardComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(irreversible.TryGetComponent<DeathNoteComponent>(out var irreversibleComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(second.TryGetComponent<DeathNoteComponent>(out var secondComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(secondIrreversible.TryGetComponent<DeathNoteComponent>(out var secondIrreversibleComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(third.TryGetComponent<DeathNoteComponent>(out var thirdComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(thirdIrreversible.TryGetComponent<DeathNoteComponent>(out var thirdIrreversibleComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(shinigami.TryGetComponent<DeathNoteComponent>(out var shinigamiComp, SEntMan.ComponentFactory), Is.True);
            Assert.That(shinigamiIrreversible.TryGetComponent<DeathNoteComponent>(out var shinigamiIrreversibleComp, SEntMan.ComponentFactory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(standardComp!.MakeTargetsUnrevivable, Is.False);
                Assert.That(irreversibleComp!.MakeTargetsUnrevivable, Is.True);
                Assert.That(irreversibleComp.WritablePageCount, Is.EqualTo(standardComp.WritablePageCount));
                Assert.That(secondComp!.OwnershipChannel, Is.EqualTo(DeathNoteOwnershipChannel.Second));
                Assert.That(secondIrreversibleComp!.OwnershipChannel, Is.EqualTo(DeathNoteOwnershipChannel.Second));
                Assert.That(secondIrreversibleComp.MakeTargetsUnrevivable, Is.True);
                Assert.That(thirdComp!.OwnershipChannel, Is.EqualTo(DeathNoteOwnershipChannel.Third));
                Assert.That(thirdIrreversibleComp!.OwnershipChannel, Is.EqualTo(DeathNoteOwnershipChannel.Third));
                Assert.That(thirdIrreversibleComp.MakeTargetsUnrevivable, Is.True);
                Assert.That(shinigamiComp!.AssignsPermanentOwner, Is.False);
                Assert.That(shinigamiComp.GrantsAllShinigamiVisibility, Is.True);
                Assert.That(shinigamiComp.MakeTargetsUnrevivable, Is.False);
                Assert.That(shinigamiIrreversibleComp!.AssignsPermanentOwner, Is.False);
                Assert.That(shinigamiIrreversibleComp.GrantsAllShinigamiVisibility, Is.True);
                Assert.That(shinigamiIrreversibleComp.MakeTargetsUnrevivable, Is.True);
            });
        });
    }

    [Test]
    public async Task EveryEnabledPresetHasRegisteredHandlerAndAlias()
    {
        await SpawnTarget("DeathNote");

        var missingHandlers = new List<string>();
        var enabledPresetCount = 0;
        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            foreach (var preset in registry.Presets)
            {
                enabledPresetCount++;
                if (!registry.TryGetHandler(preset.Handler, out var handler) || handler == null)
                    missingHandlers.Add(preset.ID);

                Assert.That(registry.TryResolve(preset.ID, out var byId), Is.True, preset.ID);
                Assert.That(byId?.ID, Is.EqualTo(preset.ID), preset.ID);
                foreach (var alias in preset.Aliases)
                {
                    Assert.That(registry.TryResolve(alias, out var byAlias), Is.True, alias);
                    Assert.That(byAlias?.ID, Is.EqualTo(preset.ID), alias);
                }
            }

            var aliases = new Dictionary<string, string>
            {
                ["инфаркт"] = "DeathNoteHeartAttack",
                ["стержень"] = "DeathNoteImmovableRod",
                ["взрыв"] = "DeathNoteExplosion",
                ["огонь"] = "DeathNoteFire",
                ["удушье"] = "DeathNoteAsphyxiation",
                ["ток"] = "DeathNoteElectrocution",
                ["яд"] = "DeathNotePoison",
                ["метеор"] = "DeathNoteMeteor",
                ["карпы"] = "DeathNoteCarp",
                ["карпов"] = "DeathNoteCarp",
                ["пауки"] = "DeathNoteSpiders",
                ["молния"] = "DeathNoteLightning",
                ["турель"] = "DeathNoteTurret",
                ["случайность"] = "DeathNoteAirlockAccident",
                ["утилизация"] = "DeathNoteDisposalCatastrophe",
                ["торговый автомат"] = "DeathNoteVendingMachineCrush",
                ["обрушение потолка"] = "DeathNoteCeilingCollapse",
                ["мимик"] = "DeathNoteMimic",
                ["бс аномалия"] = "DeathNoteBluespaceAnomaly",
                ["poisoned food"] = "DeathNotePoisonedFood",
                ["poisoned drink"] = "DeathNotePoisonedDrink",
                ["еда"] = "DeathNotePoisonedFood",
                ["напитка"] = "DeathNotePoisonedDrink",
                ["питье"] = "DeathNotePoisonedDrink",
                ["питьё"] = "DeathNotePoisonedDrink",
            };

            foreach (var (alias, expectedId) in aliases)
            {
                Assert.That(registry.TryResolve(alias, out var preset), Is.True, alias);
                Assert.That(preset?.ID, Is.EqualTo(expectedId), alias);
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(enabledPresetCount, Is.GreaterThan(0));
            Assert.That(missingHandlers, Is.Empty);
        });
    }

    [Test]
    public async Task EnabledPresetConfigurationMatchesHandlerContracts()
    {
        await SpawnTarget("DeathNote");

        var errors = new List<string>();
        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var localization = Server.ResolveDependency<ILocalizationManager>();

            foreach (var preset in registry.Presets)
                ValidatePresetConfiguration(preset, localization, errors);
        });

        Assert.That(
            errors,
            Is.Empty,
            () => $"Invalid enabled Death Note preset configuration:{Environment.NewLine}" +
                  string.Join(Environment.NewLine, errors));
    }

    private void ValidatePresetConfiguration(
        DeathNotePresetPrototype preset,
        ILocalizationManager localization,
        List<string> errors)
    {
        var parameters = preset.Parameters;

        void Require(bool condition, string requirement)
        {
            if (!condition)
                errors.Add($"{preset.ID}: {requirement}");
        }

        void RequireOrderedDamageRange()
        {
            Require(
                parameters.RandomDamageMin > 0f &&
                parameters.RandomDamageMax >= parameters.RandomDamageMin,
                "random damage range must be positive and ordered");
        }

        void RequireDamageAndRange()
        {
            Require(
                !parameters.Damage.Empty &&
                parameters.Damage.GetTotal() > FixedPoint2.Zero,
                "damage must have a positive total");
            RequireOrderedDamageRange();
        }

        void RequireEntityComponent<T>(EntProtoId? prototypeId, string field)
            where T : IComponent, new()
        {
            if (prototypeId is not { } id ||
                !Server.ProtoMan.TryIndex(id, out var prototype))
            {
                Require(false, $"{field} must reference an existing entity prototype");
                return;
            }

            Require(
                prototype.TryGetComponent<T>(out _, SEntMan.ComponentFactory),
                $"{field} entity must contain {typeof(T).Name}");
        }

        void RequireGuidedScenario(
            DeathNoteGuidedScenarioType expectedScenario,
            bool requiresGuidanceText,
            bool requiresPrelude = true)
        {
            Require(parameters.GuidedScenario == expectedScenario,
                $"guidedScenario must be {expectedScenario}");
            if (requiresPrelude)
            {
                Require(parameters.PreludeDuration > TimeSpan.Zero,
                    "guided preludeDuration must be positive");
                Require(
                    parameters.MinimumExecutionDelay >=
                    parameters.PreludeDuration + parameters.ExecutionAdvance,
                    "guided minimumExecutionDelay must cover the complete prelude and execution advance");
            }
            else
            {
                Require(parameters.PreludeDuration == TimeSpan.Zero,
                    "persistent guided scenario must not have a timed prelude");
                Require(parameters.MinimumExecutionDelay == TimeSpan.Zero,
                    "persistent guided scenario must not impose an extra execution delay");
            }

            Require(parameters.GuidedCleanupGracePeriod >= TimeSpan.Zero,
                "guided cleanup grace period must not be negative");
            if (requiresGuidanceText)
                Require(parameters.GuidanceText != null, "guided scenario must define guidanceText");
        }

        Require(localization.HasString(preset.Name), "name localization is missing");
        Require(preset.Aliases.Count > 0, "at least one input alias is required");
        Require(parameters.PreludeDuration >= TimeSpan.Zero, "preludeDuration must not be negative");
        Require(parameters.ExecutionAdvance >= TimeSpan.Zero, "executionAdvance must not be negative");
        Require(parameters.MinimumExecutionDelay >= TimeSpan.Zero,
            "minimumExecutionDelay must not be negative");
        Require(parameters.EffectTrackingDuration > TimeSpan.Zero,
            "effectTrackingDuration must be positive");

        if (parameters.TargetPopup is { } targetPopup)
            Require(localization.HasString(targetPopup), "targetPopup localization is missing");
        if (parameters.GuidanceText is { } guidanceText)
            Require(localization.HasString(guidanceText), "guidanceText localization is missing");

        if (parameters.EntityPrototype is { } entityPrototype)
            Require(Server.ProtoMan.HasIndex<EntityPrototype>(entityPrototype),
                "entityPrototype reference is missing");
        if (parameters.SecondaryEntityPrototype is { } secondaryPrototype)
            Require(Server.ProtoMan.HasIndex<EntityPrototype>(secondaryPrototype),
                "secondaryEntityPrototype reference is missing");
        if (parameters.RiftPrototype is { } riftPrototype)
            Require(Server.ProtoMan.HasIndex<EntityPrototype>(riftPrototype),
                "riftPrototype reference is missing");
        if (parameters.GameRule is { } gameRule)
        {
            Require(Server.ProtoMan.HasIndex<EntityPrototype>(gameRule),
                "gameRule reference is missing");
            RequireEntityComponent<GameRuleComponent>(gameRule, "gameRule");
        }

        if (parameters.Reagent is { } reagent)
            Require(Server.ProtoMan.HasIndex<ReagentPrototype>(reagent), "reagent reference is missing");
        if (parameters.ExplosionType is { } explosionType)
            Require(Server.ProtoMan.HasIndex<ExplosionPrototype>(explosionType),
                "explosionType reference is missing");
        if (parameters.Faction is { } faction)
            Require(Server.ProtoMan.HasIndex<NpcFactionPrototype>(faction),
                "faction reference is missing");

        foreach (var poison in parameters.ConsumablePoisons)
        {
            Require(Server.ProtoMan.HasIndex<ReagentPrototype>(poison.Reagent),
                $"consumable poison reagent {poison.Reagent} is missing");
            Require(poison.MinimumQuantity > FixedPoint2.Zero,
                $"consumable poison {poison.Reagent} minimum quantity must be positive");
            Require(poison.MaximumQuantity >= poison.MinimumQuantity,
                $"consumable poison {poison.Reagent} range must be ordered");
        }

        switch (preset.Handler)
        {
            case DeathNotePresetHandlerType.HeartAttack:
                RequireDamageAndRange();
                Require(parameters.TargetPopup != null, "heart attack must define targetPopup");
                Require(parameters.PreludeSound != null, "heart attack must define preludeSound");
                Require(parameters.PreludeDuration > TimeSpan.Zero,
                    "heart attack preludeDuration must be positive");
                Require(parameters.ExecutionAdvance <= parameters.PreludeDuration,
                    "heart attack executionAdvance must not exceed its prelude");
                Require(
                    parameters.MinimumExecutionDelay >=
                    parameters.PreludeDuration + parameters.ExecutionAdvance,
                    "heart attack minimumExecutionDelay must cover the complete prelude and execution advance");
                break;
            case DeathNotePresetHandlerType.ImmovableRod:
                RequireEntityComponent<ImmovableRodComponent>(
                    parameters.EntityPrototype,
                    "entityPrototype");
                Require(parameters.SpawnDistance > 0f &&
                        parameters.LaunchSpeed > 0f &&
                        parameters.TurnResponsiveness > 0f &&
                        parameters.ImpactRadius > 0f,
                    "rod movement parameters must be positive");
                break;
            case DeathNotePresetHandlerType.Meteor:
                RequireEntityComponent<MeteorComponent>(parameters.EntityPrototype, "entityPrototype");
                RequireEntityComponent<PhysicsComponent>(parameters.EntityPrototype, "entityPrototype");
                RequireDamageAndRange();
                Require(parameters.SpawnDistance > 0f &&
                        parameters.LaunchSpeed > 0f &&
                        parameters.TurnResponsiveness > 0f &&
                        parameters.ImpactRadius > 0f,
                    "meteor movement parameters must be positive");
                break;
            case DeathNotePresetHandlerType.Explosion:
                RequireDamageAndRange();
                Require(parameters.ExplosionType != null, "explosionType is required");
                Require(parameters.TotalIntensity > 0f &&
                        parameters.IntensitySlope > 0f &&
                        parameters.MaxTileIntensity > 0f &&
                        parameters.TileBreakScale >= 0f &&
                        parameters.MaxTileBreak >= 0,
                    "explosion intensity and tile parameters are invalid");
                break;
            case DeathNotePresetHandlerType.Electrocution:
                RequireDamageAndRange();
                Require(parameters.ShockDamage > 0, "shockDamage must be positive");
                Require(parameters.EffectDuration > TimeSpan.Zero, "effectDuration must be positive");
                break;
            case DeathNotePresetHandlerType.Fire:
                RequireDamageAndRange();
                Require(parameters.FireStacks > 0f, "fireStacks must be positive");
                break;
            case DeathNotePresetHandlerType.Poison:
                RequireDamageAndRange();
                Require(parameters.Reagent != null, "reagent is required");
                Require(parameters.ReagentQuantity > FixedPoint2.Zero,
                    "reagentQuantity must be positive");
                break;
            case DeathNotePresetHandlerType.Asphyxiation:
            case DeathNotePresetHandlerType.DirectDamage:
            case DeathNotePresetHandlerType.Lightning:
                RequireDamageAndRange();
                if (preset.Handler == DeathNotePresetHandlerType.Lightning)
                {
                    Require(parameters.EntityPrototype != null,
                        "lightning entityPrototype is required");
                    Require(parameters.SpawnDistance > 0f,
                        "lightning spawnDistance must be positive");
                }
                break;
            case DeathNotePresetHandlerType.Fauna:
                RequireEntityComponent<HTNComponent>(parameters.EntityPrototype, "entityPrototype");
                Require(parameters.SpawnCount > 0 &&
                        parameters.MaximumSpawnCount > 0 &&
                        parameters.SpawnCount <= parameters.MaximumSpawnCount,
                    "fauna spawn counts are invalid");
                Require(parameters.SpawnRadius >= 0f &&
                        parameters.RiftSpawnRadius >= 0f &&
                        parameters.RetargetInterval > TimeSpan.Zero,
                    "fauna radius or retarget timing is invalid");
                break;
            case DeathNotePresetHandlerType.HostileFaction:
                Require(parameters.Faction != null, "faction is required");
                RequireDamageAndRange();
                Require(parameters.TurretMinimumTargetHits > 0 &&
                        parameters.TurretMaximumTargetHits >= parameters.TurretMinimumTargetHits,
                    "energy-turret hit range is invalid");
                Require(parameters.TurretTargetRange > 0f,
                    "energy-turret target range must be positive");
                break;
            case DeathNotePresetHandlerType.GuidedAirlock:
                RequireGuidedScenario(
                    DeathNoteGuidedScenarioType.AirlockAccident,
                    false,
                    requiresPrelude: false);
                RequireDamageAndRange();
                Require(parameters.AirlockForceCloseDelay > TimeSpan.Zero &&
                        parameters.DoorCloseStageDuration > TimeSpan.Zero &&
                        parameters.AirlockAutoCloseDelayModifier >= 0f &&
                        parameters.DoorwayImpactRadius > 0f,
                    "guided airlock timings or radius are invalid");
                break;
            case DeathNotePresetHandlerType.GuidedDisposal:
                RequireGuidedScenario(DeathNoteGuidedScenarioType.DisposalCatastrophe, true);
                RequireDamageAndRange();
                Require(parameters.ImpactCount > 0 && parameters.ImpactInterval > TimeSpan.Zero,
                    "guided disposal impact settings are invalid");
                break;
            case DeathNotePresetHandlerType.GuidedVending:
                RequireGuidedScenario(DeathNoteGuidedScenarioType.VendingMachineCrush, true);
                RequireDamageAndRange();
                Require(parameters.VendingFallDuration > TimeSpan.Zero,
                    "guided vending fall duration must be positive");
                break;
            case DeathNotePresetHandlerType.GuidedFoodPoisoning:
                RequireGuidedScenario(DeathNoteGuidedScenarioType.PoisonedFood, true);
                Require(parameters.PoisonedConsumableLimit > 0,
                    "poisoned consumable limit must be positive");
                Require(parameters.ConsumablePoisons.Count > 0,
                    "guided poisoning must define at least one poison option");
                break;
            case DeathNotePresetHandlerType.GuidedDrinkPoisoning:
                RequireGuidedScenario(DeathNoteGuidedScenarioType.PoisonedDrink, true);
                Require(parameters.PoisonedConsumableLimit > 0,
                    "poisoned consumable limit must be positive");
                Require(parameters.ConsumablePoisons.Count > 0,
                    "guided poisoning must define at least one poison option");
                break;
            case DeathNotePresetHandlerType.CeilingCollapse:
                Require(parameters.EntityPrototype != null, "primary debris entityPrototype is required");
                Require(parameters.SpawnCount > 0 &&
                        parameters.SecondarySpawnCount >= 0 &&
                        parameters.MaximumSpawnCount > 0 &&
                        (long) parameters.SpawnCount + parameters.SecondarySpawnCount <=
                        parameters.MaximumSpawnCount,
                    "ceiling collapse spawn counts are invalid");
                Require(parameters.SecondarySpawnCount == 0 ||
                        parameters.SecondaryEntityPrototype != null,
                    "secondary debris prototype is required when secondarySpawnCount is positive");
                Require(parameters.SpawnRadius >= 0f && parameters.StructuralDamage >= 0f,
                    "ceiling collapse radius or structural damage is invalid");
                RequireDamageAndRange();
                break;
            case DeathNotePresetHandlerType.Mimic:
                RequireEntityComponent<HTNComponent>(parameters.EntityPrototype, "entityPrototype");
                RequireEntityComponent<MeleeWeaponComponent>(parameters.EntityPrototype, "entityPrototype");
                Require(parameters.SpawnRadius > 0f &&
                        parameters.RetargetInterval > TimeSpan.Zero &&
                        parameters.MimicMinimumTargetHits > 0 &&
                        parameters.MimicMaximumTargetHits >= parameters.MimicMinimumTargetHits,
                    "mimic radius, retarget timing, or hit range is invalid");
                RequireOrderedDamageRange();
                break;
            case DeathNotePresetHandlerType.BluespaceAnomaly:
                RequireEntityComponent<AnomalyComponent>(parameters.EntityPrototype, "entityPrototype");
                RequireEntityComponent<BluespaceAnomalyComponent>(parameters.EntityPrototype, "entityPrototype");
                Require(parameters.BluespaceTeleportDelay >= TimeSpan.Zero &&
                        parameters.BluespaceCollapseDelay > parameters.BluespaceTeleportDelay &&
                        parameters.BluespaceReturnDelay > parameters.BluespaceCollapseDelay,
                    "bluespace timings must be strictly ordered");
                RequireDamageAndRange();
                break;
            default:
                errors.Add($"{preset.ID}: unsupported handler {preset.Handler}");
                break;
        }
    }
}
