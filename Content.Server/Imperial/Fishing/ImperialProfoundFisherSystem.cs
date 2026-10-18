using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Imperial.ModModules;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Перчатки, которые сами служат удочкой (/obj/item/clothing/gloves/fishing + profound_fisher).
/// Атлетические перчатки изматывают во время вываживания и ранят руки при поимке крупной рыбы.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialProfoundFisherComponent : Component
{
    /// <summary>/obj/item/fishing_rod/mob_fisher/athletic.</summary>
    [DataField]
    public bool Athletic;
}

/// <summary>Модуль МОДа «рыболовная перчатка» (/obj/item/mod/module/fishing_glove): удочка вставляется в модуль.</summary>
[RegisterComponent]
public sealed partial class ImperialModFishingGloveComponent : Component
{
    public const string SlotId = "mod_fishing_rod";

    [DataField]
    public float DifficultyModifier = -5;

    [ViewVariables]
    public EntityUid? Wearer;
}

/// <summary>Владелец может рыбачить пустой рукой этой удочкой (TRAIT_PROFOUND_FISHER).</summary>
[RegisterComponent]
public sealed partial class ImperialProfoundFisherUserComponent : Component
{
    [ViewVariables]
    public EntityUid Rod;

    /// <summary>Перчатки или модуль, давшие способность.</summary>
    [ViewVariables]
    public EntityUid Source;

    [ViewVariables]
    public float DifficultyModifier;

    [ViewVariables]
    public bool Athletic;
}

public sealed class ImperialProfoundFisherSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly ImperialFishingSystem _fishing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialProfoundFisherComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ImperialProfoundFisherComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ImperialProfoundFisherComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialProfoundFisherComponent, ImperialFishingCaughtEvent>(OnCaught);
        SubscribeLocalEvent<ImperialProfoundFisherUserComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<ImperialModFishingGloveComponent, ImperialModModuleToggledEvent>(OnModuleToggled);
        SubscribeLocalEvent<ImperialModFishingGloveComponent, EntInsertedIntoContainerMessage>(OnGloveRodChanged);
        SubscribeLocalEvent<ImperialModFishingGloveComponent, EntRemovedFromContainerMessage>(OnGloveRodRemoved);
        SubscribeLocalEvent<ImperialModFishingGloveComponent, ExaminedEvent>(OnGloveExamined);
    }

    private void OnEquipped(Entity<ImperialProfoundFisherComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.GLOVES) == 0)
            return;

        var user = EnsureComp<ImperialProfoundFisherUserComponent>(args.EquipTarget);
        user.Rod = ent;
        user.Source = ent;
        user.Athletic = ent.Comp.Athletic;
        user.DifficultyModifier = 0;
    }

    private void OnUnequipped(Entity<ImperialProfoundFisherComponent> ent, ref GotUnequippedEvent args)
    {
        if (TryComp<ImperialProfoundFisherUserComponent>(args.EquipTarget, out var user) && user.Source == ent.Owner)
            RemComp<ImperialProfoundFisherUserComponent>(args.EquipTarget);
    }

    private void OnExamined(Entity<ImperialProfoundFisherComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-profound-fisher-examine"));
    }

    /// <summary>on_unarmed_attack(): клик пустой рукой по месту ловли без боевого режима.</summary>
    private void OnBeforeInteractHand(Entity<ImperialProfoundFisherUserComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || _combat.IsInCombatMode(ent) || TerminatingOrDeleted(ent.Comp.Rod))
            return;

        if (_fishing.TryProfoundFish(ent.Comp.Rod, ent, args.Target))
            args.Handled = true;
    }

    /// <summary>noodling_is_dangerous(): крупная рыба ранит руки.</summary>
    private void OnCaught(Entity<ImperialProfoundFisherComponent> ent, ref ImperialFishingCaughtEvent args)
    {
        if (!ent.Comp.Athletic || args.Reward is not { } reward || !HasComp<ImperialFishComponent>(reward)
            || !TryComp<ItemComponent>(reward, out var item))
            return;

        var size = _item.GetSizePrototype(item.Size);
        var damage = 0;
        if (size >= _item.GetSizePrototype("Ginormous"))
            damage = 18;
        else if (size >= _item.GetSizePrototype("Huge"))
            damage = 14;
        else if (size >= _item.GetSizePrototype("Large"))
            damage = 10;

        if (damage == 0)
            return;

        _damageable.TryChangeDamage(args.User, new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), damage));
        _popup.PopupEntity(Loc.GetString("imperial-profound-fisher-hurt", ("fish", reward)), args.User, args.User, PopupType.MediumCaution);
    }

    /// <summary>Модуль МОДа: при активации владелец рыбачит вставленной в модуль удочкой, сложность −5.</summary>
    private void OnModuleToggled(Entity<ImperialModFishingGloveComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        if (!args.Active)
        {
            if ((args.Wearer ?? ent.Comp.Wearer) is not { } oldWearer)
                return;

            var wearer0 = oldWearer;
            ent.Comp.Wearer = null;
            if (TryComp<ImperialProfoundFisherUserComponent>(wearer0, out var old) && old.Source == ent.Owner)
                RemComp<ImperialProfoundFisherUserComponent>(wearer0);
            return;
        }

        if (args.Wearer is not { } wearer)
            return;

        ent.Comp.Wearer = wearer;

        if (_slots.GetItemOrNull(ent, ImperialModFishingGloveComponent.SlotId) is not { } rod)
            return;

        var user = EnsureComp<ImperialProfoundFisherUserComponent>(wearer);
        user.Rod = rod;
        user.Source = ent;
        user.Athletic = false;
        user.DifficultyModifier = ent.Comp.DifficultyModifier;
    }

    /// <summary>Удочку вставили в уже работающий модуль.</summary>
    private void OnGloveRodChanged(Entity<ImperialModFishingGloveComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ImperialModFishingGloveComponent.SlotId || ent.Comp.Wearer is not { } wearer)
            return;

        var user = EnsureComp<ImperialProfoundFisherUserComponent>(wearer);
        user.Rod = args.Entity;
        user.Source = ent;
        user.Athletic = false;
        user.DifficultyModifier = ent.Comp.DifficultyModifier;
    }

    private void OnGloveRodRemoved(Entity<ImperialModFishingGloveComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ImperialModFishingGloveComponent.SlotId || ent.Comp.Wearer is not { } wearer)
            return;

        if (TryComp<ImperialProfoundFisherUserComponent>(wearer, out var user) && user.Source == ent.Owner)
            RemComp<ImperialProfoundFisherUserComponent>(wearer);
    }

    private void OnGloveExamined(Entity<ImperialModFishingGloveComponent> ent, ref ExaminedEvent args)
    {
        var rod = _slots.GetItemOrNull(ent, ImperialModFishingGloveComponent.SlotId);
        args.PushMarkup(Loc.GetString(rod == null ? "imperial-mod-fishing-glove-empty" : "imperial-mod-fishing-glove-rod", ("rod", rod ?? ent.Owner)));
        args.PushMarkup(Loc.GetString("imperial-mod-fishing-glove-help"));
    }

    /// <summary>process() атлетических перчаток: выносливость тратится во время вываживания.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ImperialProfoundFisherUserComponent>();
        while (query.MoveNext(out var uid, out var user))
        {
            if (!user.Athletic || _fishing.GetChallenge(uid) is not { } challenge
                || challenge.Rod != user.Rod || challenge.Phase != ImperialFishingPhase.Minigame)
                continue;

            _stamina.TakeStaminaDamage(uid, (2 + challenge.Difficulty * 0.02f) * frameTime, visual: false);
        }
    }
}
