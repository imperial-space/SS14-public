using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Uplink.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Store;

namespace Content.Server.Imperial.Uplink.Conditions;

/// <summary>
/// Аналог purchasable_from из TG: кому доступна позиция — предателям и/или ядерным оперативникам.
/// Ядерщики определяются по тегу NukeOpsUplink на сторе; стор без сущности (ящик излишков) считается предательским.
/// </summary>
public sealed partial class ImperialUplinkPurchasableCondition : ListingCondition
{
    [DataField]
    public bool Traitor;

    [DataField]
    public bool NukeOps;

    public override bool Condition(ListingConditionArgs args)
    {
        var nuke = args.EntityManager.System<ImperialUplinkSystem>().IsNukeOpsStore(args.StoreEntity);
        return nuke ? NukeOps : Traitor;
    }
}
