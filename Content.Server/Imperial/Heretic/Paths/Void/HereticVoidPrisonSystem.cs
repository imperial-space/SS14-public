using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Polymorph;

namespace Content.Server.Imperial.Heretic.Paths.Void;

public sealed class HereticVoidPrisonSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;

    private static readonly PolymorphConfiguration VoidPrisonConfig = new()
    {
        Entity = "HereticVoidPrisonEntity",
        Duration = 10,
        Forced = true,
        TransferDamage = false,
        RevertOnCrit = false,
        RevertOnDeath = false,
        AllowRepeatedMorphs = false,
        IgnoreAllowRepeatedMorphs = true,
        PolymorphPopup = null,
        ExitPolymorphPopup = null,
        Inventory = PolymorphInventoryChange.None,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VoidPrisonComponent, PolymorphedEvent>(OnPolymorphed);
    }

    public void ApplyVoidPrison(EntityUid target)
    {
        _polymorph.PolymorphEntity(target, VoidPrisonConfig);
    }

    private void OnPolymorphed(Entity<VoidPrisonComponent> ent, ref PolymorphedEvent ev)
    {
        if (!ev.IsRevert)
            return;
        if (!HasComp<HereticComponent>(ev.NewEntity))
            _hereticEffects.ApplyVoidChill(ev.NewEntity, 1);
    }
}
