using Content.Server.GameTicking;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.EntityEffects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Security;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Security;

/// <summary>organ/cyberimp/eyes/hud/security: HUD СБ без очков.</summary>
[RegisterComponent]
public sealed partial class SecurityHudImplantComponent : Component
{
    /// <summary>Какие HUD-компоненты добавил сам имплант (остальные у носителя уже были).</summary>
    [ViewVariables]
    public List<string> Added = new();
}

/// <summary>
/// Перцовый баллончик (эффект конденсированного капсаицина), HUD-имплант СБ
/// и раскладка снаряжения по шкафам: баллончики у офицеров и смотрителя, нарукавники помощников у ГСБ.
/// </summary>
public sealed class SecurityPersonnelSystem : EntityEffectSystem<HumanoidProfileComponent, PepperSprayExposure>
{
    private static readonly Dictionary<string, string[]> LockerFills = new()
    {
        ["LockerSecurityFilled"] = ["PepperSpray"],
        ["LockerWardenFilled"] = ["PepperSpray"],
        ["LockerWardenFilledHardsuit"] = ["PepperSpray"],
        ["LockerHeadOfSecurityFilled"] = ["PepperSpray", "BoxDeputyArmbands"],
        ["LockerHeadOfSecurityFilledHardsuit"] = ["PepperSpray", "BoxDeputyArmbands"],
    };

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityHudImplantComponent, ImplantImplantedEvent>(OnHudImplanted);
        SubscribeLocalEvent<SecurityHudImplantComponent, ImplantRemovedEvent>(OnHudRemoved);
        SubscribeLocalEvent<PostGameMapLoad>(OnMapLoaded);
    }

    #region Перцовый спрей

    protected override void Effect(Entity<HumanoidProfileComponent> entity, ref EntityEffectEvent<PepperSprayExposure> args)
    {
        // is_pepper_proof(): нужна защита и рта, и глаз.
        if (IsPepperProof(entity))
            return;

        if (_random.Prob(0.05f))
            _chat.TryEmoteWithChat(entity, "Scream", ignoreActionBlocker: true);
        _chat.TryEmoteWithChat(entity, "Crying", ignoreActionBlocker: true);

        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(entity, TemporaryBlindnessSystem.BlindingStatusEffect,
            args.Effect.BlindTime, true);
        _stun.TryKnockdown(entity.Owner, args.Effect.KnockdownTime, true);
        _popup.PopupEntity(Loc.GetString("pepper-spray-burns"), entity, entity, PopupType.LargeCaution);
    }

    /// <summary>
    /// carbon/is_pepper_proof(): защищает только одна вещь, закрывающая и глаза, и рот (PEPPERPROOF) —
    /// противогаз или закрытый шлем. Очки и дыхательные маски не спасают.
    /// </summary>
    private bool IsPepperProof(EntityUid uid)
    {
        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item) ||
                !TryComp<IdentityBlockerComponent>(item, out var blocker) ||
                !blocker.Enabled || blocker.Coverage != IdentityBlockerCoverage.FULL)
            {
                continue;
            }

            if (HasComp<BreathToolComponent>(item) || HasComp<EyeProtectionComponent>(item))
                return true;
        }

        return false;
    }

    #endregion

    #region HUD-имплант

    private static readonly Type[] HudComponents =
    [
        typeof(ShowJobIconsComponent),
        typeof(ShowMindShieldIconsComponent),
        typeof(ShowCriminalRecordIconsComponent),
    ];

    private void OnHudImplanted(Entity<SecurityHudImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        ent.Comp.Added.Clear();
        foreach (var type in HudComponents)
        {
            if (HasComp(args.Implanted, type))
                continue;

            var comp = (Component) Factory.GetComponent(type);
            AddComp(args.Implanted, comp);
            ent.Comp.Added.Add(Factory.GetComponentName(type));
        }
    }

    private void OnHudRemoved(Entity<SecurityHudImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        foreach (var name in ent.Comp.Added)
        {
            RemComp(args.Implanted, Factory.GetRegistration(name).Type);
        }

        ent.Comp.Added.Clear();
    }

    #endregion

    #region Шкафы

    private void OnMapLoaded(PostGameMapLoad args)
    {
        var query = EntityQueryEnumerator<EntityStorageComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var storage, out var meta, out var xform))
        {
            if (xform.MapID != args.Map || meta.EntityPrototype?.ID is not { } id || !LockerFills.TryGetValue(id, out var fill))
                continue;

            foreach (var proto in fill)
            {
                var item = Spawn(proto, xform.Coordinates);
                if (!_entityStorage.Insert(item, uid, storage))
                    QueueDel(item);
            }
        }
    }

    #endregion
}
