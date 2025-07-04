using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.EntityEffects;


public sealed partial class AddMagicEssence : EntityEffect
{
    /// <summary>
    /// Added reagents to entity
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> AddedEssences = new();


    public override void Effect(EntityEffectBaseArgs args)
    {
        //if (args is not MagicEntityEffectsArgs magicEntityEffectsArgs) return;
//
        //var enumerator = args.EntityManager.EntityQueryEnumerator<BindStoreOnEquipComponent>();
//
        //while (enumerator.MoveNext(out var spellBookUid, out var bindStoreOnEquipComponent))
        //{
        //    if (bindStoreOnEquipComponent.BindedEntity != magicEntityEffectsArgs.Performer) continue;
//
        //    //foreach (var (currencyPrototype, count) in AddedEssences) poka chto was sdelana another system of poluschenie essence
        //    //    TryAddEssence(currencyPrototype, count, spellBookUid, args.EntityManager);
//
        //    return;
        //}
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";

    #region Helpers

    private bool TryAddEssence(EntProtoId currencyPrototype, int count, EntityUid spellBookUid, IEntityManager entityManager)
    {
        var imperialStoreSystem = entityManager.System<ImperialStoreSystem>();

        var addOneOrMoreEssence = false;

        for (var i = 0; i < count; i++)
        {
            var essenceEntity = entityManager.Spawn(currencyPrototype);

            addOneOrMoreEssence = true;
            imperialStoreSystem.TryAddCurrency(essenceEntity, spellBookUid);
        }

        return addOneOrMoreEssence;
    }

    #endregion
}
