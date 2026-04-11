using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Popups;
using Content.Shared.Inventory;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Управляет магическим щитом культовых доспехов.
/// При надевании доспеха с <see cref="CultArmorComponent"/> носитель получает
/// <see cref="CultShieldComponent"/> на 3 заряда, каждый блокирует один удар.
/// Заряды восстанавливаются через Кровавый Обряд.
/// </summary>
public sealed class CultShieldSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultArmorComponent, ClothingGotEquippedEvent>(OnArmorEquipped);
        SubscribeLocalEvent<CultArmorComponent, ClothingGotUnequippedEvent>(OnArmorUnequipped);
        SubscribeLocalEvent<CultShieldComponent, ComponentShutdown>(OnShieldShutdown);
        SubscribeLocalEvent<CultShieldComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    // ── Надевание/снятие доспеха ─────────────────────────────────────────────

    private void OnArmorEquipped(Entity<CultArmorComponent> ent, ref ClothingGotEquippedEvent args)
        => ActivateShield(args.Wearer, ent.Comp);

    private void OnArmorUnequipped(Entity<CultArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (TryComp<CultShieldComponent>(args.Wearer, out var shield))
        {
            ent.Comp.StoredShieldCharges = Math.Clamp(shield.Charges, 0, shield.MaxCharges);
            RemoveShieldVisual(shield);
        }
        RemComp<CultShieldComponent>(args.Wearer);
    }

    private void OnShieldShutdown(Entity<CultShieldComponent> ent, ref ComponentShutdown args)
        => RemoveShieldVisual(ent.Comp);

    // ── Блокирование удара ───────────────────────────────────────────────────

    private void OnBeforeDamage(EntityUid uid, CultShieldComponent shield, ref BeforeDamageChangedEvent args)
    {
        if (shield.Charges <= 0)
            return;

        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        // Блокируем удар
        args.Cancelled = true;

        shield.Charges--;
        Dirty(uid, shield);
        SyncStoredArmorCharges(uid, shield);

        _popup.PopupEntity(Loc.GetString("cult-shield-blocked"), uid, uid, PopupType.Small);
        _audio.PlayPvs("/Audio/Imperial/cult/weapons/cult_shield.ogg", uid);

        if (shield.Charges <= 0)
        {
            RemoveShieldVisual(shield);
            _popup.PopupEntity(Loc.GetString("cult-shield-broken"), uid, uid, PopupType.MediumCaution);
        }
    }

    // ── Публичный API ────────────────────────────────────────────────────────

    /// <summary>
    /// Восстанавливает все заряды щита (вызывается Blood Rites).
    /// Если визуальный эффект пропал — пересоздаёт его.
    /// </summary>
    public void RestoreShield(EntityUid uid)
    {
        if (!TryComp<CultShieldComponent>(uid, out var shield))
            return;

        shield.Charges = shield.MaxCharges;
        Dirty(uid, shield);
        SyncStoredArmorCharges(uid, shield);

        if (shield.VisualEntity == null || Deleted(shield.VisualEntity.Value))
            SpawnShieldVisual(uid, shield);

        _popup.PopupEntity(Loc.GetString("cult-shield-restored", ("charges", shield.Charges)), uid, uid, PopupType.Small);
    }

    // ── Вспомогательные ─────────────────────────────────────────────────────

    private void ActivateShield(EntityUid wearer, CultArmorComponent armor)
    {
        var shield = EnsureComp<CultShieldComponent>(wearer);
        shield.Charges = Math.Clamp(armor.StoredShieldCharges, 0, shield.MaxCharges);
        Dirty(wearer, shield);

        if (shield.Charges > 0)
            SpawnShieldVisual(wearer, shield);

        _popup.PopupEntity(Loc.GetString("cult-shield-activated", ("charges", shield.Charges)), wearer, wearer, PopupType.Small);
    }

    private void SpawnShieldVisual(EntityUid wearer, CultShieldComponent shield)
    {
        var visual = Spawn("CultShieldEffect", Transform(wearer).Coordinates);
        _xform.SetParent(visual, wearer);
        shield.VisualEntity = visual;
    }

    private void RemoveShieldVisual(CultShieldComponent shield)
    {
        if (shield.VisualEntity != null && !Deleted(shield.VisualEntity.Value))
        {
            QueueDel(shield.VisualEntity.Value);
            shield.VisualEntity = null;
        }
    }

    private void SyncStoredArmorCharges(EntityUid wearer, CultShieldComponent shield)
    {
        if (!_inventory.TryGetSlotEntity(wearer, "outerClothing", out var armorUid))
            return;

        if (!TryComp<CultArmorComponent>(armorUid, out var armor))
            return;

        armor.StoredShieldCharges = Math.Clamp(shield.Charges, 0, shield.MaxCharges);
    }
}
