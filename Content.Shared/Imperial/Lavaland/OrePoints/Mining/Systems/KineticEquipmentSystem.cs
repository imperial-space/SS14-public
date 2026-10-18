using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Модули кинетического акселератора и трофеи дробителя: установка, снятие, осмотр,
/// перезарядка и урон в ближнем бою.
/// </summary>
public sealed class KineticEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    private static readonly string Prying = "Prying";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KineticAcceleratorComponent, ComponentInit>(OnAcceleratorInit);
        SubscribeLocalEvent<KineticAcceleratorComponent, MapInitEvent>(OnAcceleratorMapInit);
        SubscribeLocalEvent<KineticAcceleratorComponent, InteractUsingEvent>(OnAcceleratorInteractUsing);
        SubscribeLocalEvent<KineticAcceleratorComponent, ExaminedEvent>(OnAcceleratorExamined);
        SubscribeLocalEvent<KineticAcceleratorComponent, GetVerbsEvent<Verb>>(OnAcceleratorVerbs);
        SubscribeLocalEvent<KineticAcceleratorComponent, EntInsertedIntoContainerMessage>(OnAcceleratorContainerChanged);
        SubscribeLocalEvent<KineticAcceleratorComponent, EntRemovedFromContainerMessage>(OnAcceleratorContainerChanged);

        SubscribeLocalEvent<KineticModkitComponent, ExaminedEvent>(OnModkitExamined);

        SubscribeLocalEvent<KineticCrusherComponent, ComponentInit>(OnCrusherInit);
        SubscribeLocalEvent<KineticCrusherComponent, MapInitEvent>(OnCrusherMapInit);
        SubscribeLocalEvent<KineticCrusherComponent, InteractUsingEvent>(OnCrusherInteractUsing);
        SubscribeLocalEvent<KineticCrusherComponent, ExaminedEvent>(OnCrusherExamined);
        SubscribeLocalEvent<KineticCrusherComponent, GetVerbsEvent<Verb>>(OnCrusherVerbs);
        SubscribeLocalEvent<KineticCrusherComponent, EntInsertedIntoContainerMessage>(OnCrusherContainerChanged);
        SubscribeLocalEvent<KineticCrusherComponent, EntRemovedFromContainerMessage>(OnCrusherContainerChanged);
        SubscribeLocalEvent<KineticCrusherComponent, GetMeleeDamageEvent>(OnCrusherGetMeleeDamage);

        SubscribeLocalEvent<CrusherTrophyComponent, ExaminedEvent>(OnTrophyExamined);
    }

    #region Акселератор

    private void OnAcceleratorInit(Entity<KineticAcceleratorComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
    }

    private void OnAcceleratorMapInit(Entity<KineticAcceleratorComponent> ent, ref MapInitEvent args)
    {
        RefreshAccelerator(ent);
    }

    public IEnumerable<Entity<KineticModkitComponent>> GetModkits(Entity<KineticAcceleratorComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            yield break;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<KineticModkitComponent>(uid, out var modkit))
                yield return (uid, modkit);
        }
    }

    /// <summary>get_remaining_mod_capacity().</summary>
    public int GetRemainingCapacity(Entity<KineticAcceleratorComponent> ent)
    {
        return ent.Comp.MaxModCapacity - GetModkits(ent).Sum(m => m.Comp.Cost);
    }

    private void OnAcceleratorInteractUsing(Entity<KineticAcceleratorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, Prying))
        {
            args.Handled = true;
            RemoveAll(ent, ent.Comp.ContainerId, args.User, "kinetic-modkit-removed-all", "kinetic-modkit-none");
            return;
        }

        if (!TryComp<KineticModkitComponent>(args.Used, out var modkit))
            return;

        args.Handled = true;
        TryInstallModkit(ent, (args.Used, modkit), args.User);
    }

    /// <summary>modkit/install().</summary>
    public bool TryInstallModkit(Entity<KineticAcceleratorComponent> ent, Entity<KineticModkitComponent> modkit, EntityUid? user)
    {
        if (modkit.Comp.Group is { } group)
        {
            var max = Math.Max(1, modkit.Comp.MaxOfGroup);
            var installed = GetModkits(ent).Count(m => m.Comp.Group == group);
            if (installed >= max)
            {
                if (user != null)
                    _popup.PopupClient(Loc.GetString("kinetic-modkit-conflict"), ent, user.Value);
                return false;
            }
        }

        var remaining = GetRemainingCapacity(ent);
        if (remaining < modkit.Comp.Cost)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("kinetic-modkit-no-space", ("remaining", remaining), ("cost", modkit.Comp.Cost)), ent, user.Value);
            return false;
        }

        if (!_container.Insert(modkit.Owner, _container.GetContainer(ent, ent.Comp.ContainerId)))
            return false;

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("kinetic-modkit-installed"), ent, user.Value);
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, user);
        }

        return true;
    }

    private void OnAcceleratorExamined(Entity<KineticAcceleratorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(KineticAcceleratorComponent)))
        {
            args.PushMarkup(Loc.GetString("kinetic-accelerator-examine-capacity", ("remaining", GetRemainingCapacity(ent))));
            args.PushMarkup(Loc.GetString("kinetic-accelerator-examine-remove"));
            foreach (var modkit in GetModkits(ent))
            {
                args.PushMarkup(Loc.GetString("kinetic-accelerator-examine-modkit", ("modkit", modkit.Owner), ("cost", modkit.Comp.Cost)));
            }
        }
    }

    private void OnModkitExamined(Entity<KineticModkitComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("kinetic-modkit-examine-cost", ("cost", ent.Comp.Cost)));
    }

    private void OnAcceleratorVerbs(Entity<KineticAcceleratorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        foreach (var modkit in GetModkits(ent).ToList())
        {
            var uid = modkit.Owner;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("kinetic-modkit-verb-remove", ("modkit", uid)),
                Category = VerbCategory.Eject,
                Act = () => RemoveOne(ent, ent.Comp.ContainerId, uid, user),
            });
        }
    }

    private void OnAcceleratorContainerChanged<T>(Entity<KineticAcceleratorComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            RefreshAccelerator(ent);
    }

    /// <summary>cooldown/get_recharge_time(): recharge_time минус модификаторы всех установленных модулей.</summary>
    public float GetRechargeTime(Entity<KineticAcceleratorComponent> ent)
    {
        var time = ent.Comp.RechargeTime;
        foreach (var modkit in GetModkits(ent))
        {
            time -= modkit.Comp.Cooldown;
        }

        return Math.Max(0.1f, time);
    }

    private void RefreshAccelerator(Entity<KineticAcceleratorComponent> ent)
    {
        if (!TryComp<RechargeBasicEntityAmmoComponent>(ent, out var recharge))
            return;

        recharge.RechargeCooldown = GetRechargeTime(ent);
        Dirty(ent, recharge);
    }

    #endregion

    #region Дробитель

    private void OnCrusherInit(Entity<KineticCrusherComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
    }

    private void OnCrusherMapInit(Entity<KineticCrusherComponent> ent, ref MapInitEvent args)
    {
        RefreshCrusher(ent);
    }

    public IEnumerable<Entity<CrusherTrophyComponent>> GetTrophies(Entity<KineticCrusherComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            yield break;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<CrusherTrophyComponent>(uid, out var trophy))
                yield return (uid, trophy);
        }
    }

    public float GetTrophyBonus(Entity<KineticCrusherComponent> ent, CrusherTrophyKind kind)
    {
        var total = 0f;
        foreach (var trophy in GetTrophies(ent))
        {
            if (trophy.Comp.Trophy == kind)
                total += trophy.Comp.BonusValue;
        }

        return total;
    }

    public bool HasTrophy(Entity<KineticCrusherComponent> ent, CrusherTrophyKind kind)
    {
        return GetTrophies(ent).Any(t => t.Comp.Trophy == kind);
    }

    private void OnCrusherInteractUsing(Entity<KineticCrusherComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, Prying))
        {
            args.Handled = true;
            RemoveAll(ent, ent.Comp.ContainerId, args.User, "crusher-trophy-removed-all", "crusher-trophy-none");
            return;
        }

        if (!TryComp<CrusherTrophyComponent>(args.Used, out var trophy))
            return;

        args.Handled = true;
        TryAddTrophy(ent, (args.Used, trophy), args.User);
    }

    /// <summary>crusher_trophy/add_to().</summary>
    public bool TryAddTrophy(Entity<KineticCrusherComponent> ent, Entity<CrusherTrophyComponent> trophy, EntityUid? user)
    {
        if (GetTrophies(ent).Any(t => t.Comp.Group == trophy.Comp.Group))
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("crusher-trophy-conflict", ("trophy", trophy.Owner), ("crusher", ent.Owner)), ent, user.Value);
            return false;
        }

        if (!_container.Insert(trophy.Owner, _container.GetContainer(ent, ent.Comp.ContainerId)))
            return false;

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("crusher-trophy-attached", ("trophy", trophy.Owner), ("crusher", ent.Owner)), ent, user.Value);
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, user);
        }

        return true;
    }

    private void OnCrusherExamined(Entity<KineticCrusherComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var force = GetForceWielded(ent);
        var detonation = GetDetonationDamage(ent);
        using (args.PushGroup(nameof(KineticCrusherComponent)))
        {
            args.PushMarkup(Loc.GetString("crusher-examine-mark", ("damage", force + detonation)));
            args.PushMarkup(Loc.GetString("crusher-examine-backstab", ("damage", force + detonation + ent.Comp.BackstabBonus), ("normal", force + detonation)));
            foreach (var trophy in GetTrophies(ent))
            {
                args.PushMarkup(Loc.GetString("crusher-examine-trophy", ("trophy", trophy.Owner), ("effect", TrophyEffect(trophy))));
            }
        }
    }

    private void OnTrophyExamined(Entity<CrusherTrophyComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("crusher-trophy-examine", ("effect", TrophyEffect(ent))));
    }

    public string TrophyEffect(Entity<CrusherTrophyComponent> ent)
    {
        var bonus = ent.Comp.BonusValue;
        return Loc.GetString(ent.Comp.Effect,
            ("bonus", bonus),
            ("tenth", bonus * 0.1f),
            ("fifth", bonus * 0.2f),
            ("per", bonus > 0f ? 10f / bonus : 0f),
            ("half", bonus * 0.5f));
    }

    private void OnCrusherVerbs(Entity<KineticCrusherComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        foreach (var trophy in GetTrophies(ent).ToList())
        {
            var uid = trophy.Owner;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("crusher-trophy-verb-remove", ("trophy", uid)),
                Category = VerbCategory.Eject,
                Act = () => RemoveOne(ent, ent.Comp.ContainerId, uid, user),
            });
        }
    }

    private void OnCrusherContainerChanged<T>(Entity<KineticCrusherComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            RefreshCrusher(ent);
    }

    /// <summary>charge_time с учётом legion_skull (bonus_value в десятых секунды).</summary>
    public float GetChargeTime(Entity<KineticCrusherComponent> ent)
    {
        return Math.Max(0.1f, ent.Comp.ChargeTime - GetTrophyBonus(ent, CrusherTrophyKind.LegionSkull) * 0.1f);
    }

    /// <summary>Прибавка к force_wielded от demon_claws и wendigo_horn.</summary>
    public float GetForceBonus(Entity<KineticCrusherComponent> ent)
    {
        var bonus = 0f;
        foreach (var trophy in GetTrophies(ent))
        {
            bonus += trophy.Comp.Trophy switch
            {
                CrusherTrophyKind.DemonClaws => trophy.Comp.BonusValue * 0.2f,
                CrusherTrophyKind.WendigoHorn => trophy.Comp.BonusValue,
                _ => 0f,
            };
        }

        return bonus;
    }

    public float GetForceWielded(Entity<KineticCrusherComponent> ent)
    {
        var baseDamage = 0f;
        if (TryComp<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(ent, out var melee))
            baseDamage = melee.Damage.GetTotal().Float();
        return baseDamage + GetForceBonus(ent);
    }

    /// <summary>detonation_damage с учётом demon_claws.</summary>
    public float GetDetonationDamage(Entity<KineticCrusherComponent> ent)
    {
        return ent.Comp.DetonationDamage + GetTrophyBonus(ent, CrusherTrophyKind.DemonClaws) * 0.8f;
    }

    private void RefreshCrusher(Entity<KineticCrusherComponent> ent)
    {
        _appearance.SetData(ent, CrusherVisuals.Skull, HasTrophy(ent, CrusherTrophyKind.Retool));

        if (!TryComp<RechargeBasicEntityAmmoComponent>(ent, out var recharge))
            return;

        recharge.RechargeCooldown = GetChargeTime(ent);
        Dirty(ent, recharge);
    }

    private void OnCrusherGetMeleeDamage(Entity<KineticCrusherComponent> ent, ref GetMeleeDamageEvent args)
    {
        var bonus = GetForceBonus(ent);
        if (bonus <= 0f)
            return;

        var total = args.Damage.GetTotal().Float();
        if (total <= 0f)
            return;

        // update_wielding(): force_wielded растёт, тип урона прежний.
        args.Damage *= (total + bonus) / total;
    }

    #endregion

    private void RemoveAll(EntityUid uid, string containerId, EntityUid user, string removedLoc, string noneLoc)
    {
        if (!_container.TryGetContainer(uid, containerId, out var container) || container.ContainedEntities.Count == 0)
        {
            _popup.PopupClient(Loc.GetString(noneLoc), uid, user);
            return;
        }

        foreach (var item in container.ContainedEntities.ToList())
        {
            _container.Remove(item, container);
        }

        _popup.PopupClient(Loc.GetString(removedLoc), uid, user);
    }

    private void RemoveOne(EntityUid uid, string containerId, EntityUid item, EntityUid user)
    {
        if (!_container.TryGetContainer(uid, containerId, out var container) || !container.Contains(item))
            return;

        _container.Remove(item, container);
        _hands.PickupOrDrop(user, item);
    }
}
