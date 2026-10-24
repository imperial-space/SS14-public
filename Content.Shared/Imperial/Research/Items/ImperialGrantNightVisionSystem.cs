using Content.Shared.Clothing;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Очки ночного видения TG: при надевании выдают ImperialNightVision, при снятии забирают, если выдали сами.
/// Компонент берётся по имени: Imperial/Vision живёт в другой ветке, без неё очки ничего не делают.
/// </summary>
public sealed class ImperialGrantNightVisionSystem : EntitySystem
{
    private const string NightVisionComponent = "ImperialNightVision";

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialGrantNightVisionComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ImperialGrantNightVisionComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<ImperialGrantNightVisionComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!EntityManager.ComponentFactory.TryGetRegistration(NightVisionComponent, out var reg)
            || HasComp(args.Wearer, reg.Type))
            return;

        AddComp(args.Wearer, EntityManager.ComponentFactory.GetComponent(reg));
        ent.Comp.Granted = true;
    }

    private void OnUnequipped(Entity<ImperialGrantNightVisionComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!ent.Comp.Granted)
            return;

        ent.Comp.Granted = false;
        if (EntityManager.ComponentFactory.TryGetRegistration(NightVisionComponent, out var reg))
            RemComp(args.Wearer, reg.Type);
    }
}
