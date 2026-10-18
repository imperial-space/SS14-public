using Content.Shared.Clothing;
using Content.Shared.Imperial.Vision;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Очки ночного видения TG: при надевании выдают ImperialNightVision, при снятии забирают, если выдали сами.
/// </summary>
public sealed class ImperialGrantNightVisionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialGrantNightVisionComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ImperialGrantNightVisionComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<ImperialGrantNightVisionComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (HasComp<ImperialNightVisionComponent>(args.Wearer))
            return;

        AddComp<ImperialNightVisionComponent>(args.Wearer);
        ent.Comp.Granted = true;
    }

    private void OnUnequipped(Entity<ImperialGrantNightVisionComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!ent.Comp.Granted)
            return;

        ent.Comp.Granted = false;
        RemComp<ImperialNightVisionComponent>(args.Wearer);
    }
}
