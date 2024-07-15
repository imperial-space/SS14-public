using System.Text.Json.Serialization;
using Content.Server.Administration.Systems;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReactionEffects;

/// <summary>
///     Resurrection of an entity with the instant return of the ghost to the body.
/// </summary>
[DataDefinition]
public sealed partial class Resurrection : EntityEffect
{
    [JsonPropertyName("penaltyDamage")]
    [DataField("penaltyDamage")]
    public DamageSpecifier PenaltyDamage = new()!;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-resurrection",
            ("chance", Probability)
        );

    public override void Effect(EntityEffectBaseArgs args)
    {
        var rejuvenateSystem = args.EntityManager.System<RejuvenateSystem>();
        var damageSystem = args.EntityManager.System<DamageableSystem>();
        var popupSystem = args.EntityManager.System<SharedPopupSystem>();
        var mindSystem = args.EntityManager.System<SharedMindSystem>();

        rejuvenateSystem.PerformRejuvenate(args.TargetEntity);

        damageSystem.TryChangeDamage(
            args.TargetEntity,
            PenaltyDamage,
            true
        );

        if (mindSystem.TryGetMind(args.TargetEntity, out var mindUid, out var mind))
            mindSystem.UnVisit(mindUid, mind);

        popupSystem.PopupEntity(Loc.GetString("reagent-effect-body-resurrection"), args.TargetEntity, PopupType.LargeCaution);
    }
}
