using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.EntityEffects;


public sealed partial class AddMagicEssence : EntityEffect
{
    private IRobustRandom? _random;
    private ImperialStoreSystem? _storeSys;

    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> AddedEssences = [];

    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> BonusEssences = [];

    [DataField]
    public float EssenceAddProbability;

    [DataField]
    public float BonusAddProbability;


    public override void Effect(EntityEffectBaseArgs args)
    {
        _random ??= IoCManager.Resolve<IRobustRandom>();
        _storeSys ??= args.EntityManager.System<ImperialStoreSystem>();

        if (args is not MagicEntityEffectsArgs magicEntityEffectsArgs)
            return;

        var enumerator = args.EntityManager.EntityQueryEnumerator<BindStoreOnEquipComponent>();

        while (enumerator.MoveNext(out var spellBookUid, out var bindStoreOnEquipComponent))
        {
            if (bindStoreOnEquipComponent.BindedEntity != magicEntityEffectsArgs.Performer)
                continue;

            if (_random.Prob(EssenceAddProbability))
                _storeSys.TryAddCurrency(AddedEssences, spellBookUid);

            if (_random.Prob(BonusAddProbability))
                _storeSys.TryAddBonus(BonusEssences, spellBookUid);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";
}
