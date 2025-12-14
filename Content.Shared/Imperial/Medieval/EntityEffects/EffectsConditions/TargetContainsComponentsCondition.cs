using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.EffectConditions;


/// <summary>
/// Proves whether the target has protective components
/// </summary>
public sealed partial class TargetContainsComponentsCondition : EntityEffectCondition
{
    /// <summary>
    /// Components for check
    /// </summary>
    [DataField]
    public ComponentRegistry ComponentsWhitelist = new();

    /// <summary>
    /// Components for check
    /// </summary>
    [DataField]
    public ComponentRegistry ComponentsBlacklist = new();


    public override bool Condition(EntityEffectBaseArgs args)
    {
        return CheckBlacklist(args.TargetEntity, args.EntityManager) && CheckWhitelist(args.TargetEntity, args.EntityManager);
    }

    public override string GuidebookExplanation(IPrototypeManager prototype) => "";

    #region Helpers

    private bool CheckWhitelist(EntityUid target, IEntityManager entityManager)
    {
        foreach (var (_, componentRegistry) in ComponentsWhitelist)
        {
            if (entityManager.HasComponent(target, componentRegistry.Component.GetType())) continue;

            return false;
        }

        return true;
    }

    private bool CheckBlacklist(EntityUid target, IEntityManager entityManager)
    {
        foreach (var (_, componentRegistry) in ComponentsBlacklist)
        {
            if (!entityManager.HasComponent(target, componentRegistry.Component.GetType())) continue;

            return false;
        }

        return true;
    }

    #endregion
}
