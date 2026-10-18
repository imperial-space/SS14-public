using Content.Shared.Stacks;
using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeCrossbreedSystem
{

    private static readonly string[] PrismColours = { "#FFFFFF", "#FF4040", "#FFA040", "#FFFF40", "#40FF40", "#40C0FF", "#4060FF", "#C040FF", "#FF60C0" };
    private static readonly EntProtoId PrismColourAction = "ActionSlimePrismColour";
    private static readonly EntProtoId PrismPlaceAction = "ActionSlimePrismPlace";
    private static readonly ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>[] AggressiveFactions = { "SimpleHostile", "Xeno", "Dragon" };

    private void InitializeItems()
    {
        SubscribeLocalEvent<SlimeBarrierCubeComponent, UseInHandEvent>(OnBarrierCubeUse);
        SubscribeLocalEvent<SlimeCaptureDeviceComponent, AfterInteractEvent>(OnCaptureInteract);
        SubscribeLocalEvent<SlimeCaptureDeviceComponent, UseInHandEvent>(OnCaptureUse);
        SubscribeLocalEvent<SlimeRebreatherComponent, ClothingGotEquippedEvent>(OnRebreatherEquipped);
        SubscribeLocalEvent<SlimeRebreatherComponent, ClothingGotUnequippedEvent>(OnRebreatherUnequipped);
        SubscribeLocalEvent<SlimePeaceFlowerComponent, ClothingGotEquippedEvent>(OnFlowerEquipped);
        SubscribeLocalEvent<SlimePeaceFlowerComponent, ClothingGotUnequippedEvent>(OnFlowerUnequipped);
        SubscribeLocalEvent<SlimePeaceFlowerComponent, BeingUnequippedAttemptEvent>(OnFlowerUnequipAttempt);
        SubscribeLocalEvent<SlimeRainbowKnifeComponent, GetMeleeDamageEvent>(OnRainbowKnifeDamage);
        SubscribeLocalEvent<SlimeBloodChillProjectileComponent, ProjectileHitEvent>(OnBloodChillHit);
        SubscribeLocalEvent<SlimePrismGlassesComponent, GetItemActionsEvent>(OnPrismGetActions);
        SubscribeLocalEvent<SlimePrismGlassesComponent, SlimePrismColourActionEvent>(OnPrismColour);
        SubscribeLocalEvent<SlimePrismGlassesComponent, SlimePrismPlaceActionEvent>(OnPrismPlace);
        SubscribeLocalEvent<SlimeLightPrismComponent, InteractHandEvent>(OnLightPrismInteract);
        SubscribeLocalEvent<SlimeIceStasisComponent, EntRemovedFromContainerMessage>(OnIceRemoved);
        SubscribeLocalEvent<SlimeRewindCameraComponent, AfterInteractEvent>(OnRewindCameraInteract);
        SubscribeLocalEvent<SlimeRewindCameraComponent, UseInHandEvent>(OnRewindCameraUse);
        SubscribeLocalEvent<SlimeCrossPotionComponent, AfterInteractEvent>(OnCrossPotionInteract);
        SubscribeLocalEvent<SlimeCrossPotionComponent, SlimeCrossPotionDoAfterEvent>(OnCrossPotionDoAfter);
        SubscribeLocalEvent<SlimeGolemComponent, MapInitEvent>(OnGolemInit);
        SubscribeLocalEvent<SlimePotionedComponent, GetPressureProtectionValuesEvent>(OnSpaceproofPressure);
    }

    private void UpdateItems(TimeSpan now)
    {
        var chillers = EntityQueryEnumerator<SlimeBloodChillerComponent, BasicEntityAmmoProviderComponent>();
        while (chillers.MoveNext(out var uid, out var chiller, out var ammo))
        {
            if (now < chiller.NextCharge)
                continue;
            chiller.NextCharge = now + TimeSpan.FromSeconds(1);
            if (ammo.Count >= ammo.Capacity)
                continue;

            // Перезарядка кровью владельца: 20 крови, либо 5 урона, если крови нет.
            var owner = Transform(uid).ParentUid;
            if (!HasComp<MobStateComponent>(owner))
                continue;
            if (HasComp<Content.Shared.Body.Components.BloodstreamComponent>(owner))
                _bloodstream.TryModifyBloodLevel(owner, -20);
            else
                Damage(owner, "Blunt", 5f);
            _gunAmmo.ChangeBasicEntityAmmoCount((uid, ammo), 1);
        }
    }

    [Dependency] private readonly SharedGunSystem _gunAmmo = default!;

    #region Барьеры

    /// <summary>barriercube/attack_self().</summary>
    private void OnBarrierCubeUse(Entity<SlimeBarrierCubeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var coords = At(args.User).SnapToGrid(EntityManager);
        if (_lookup.GetEntitiesInRange(coords, 0.4f).Any(e => MetaData(e).EntityPrototype?.ID == ent.Comp.Barrier.Id))
        {
            PopupUser("slime-cross-barrier-occupied", args.User);
            return;
        }

        PopupUser("slime-cross-barrier-squeeze", args.User, ("cube", ent.Owner));
        var barrier = Spawn(ent.Comp.Barrier, coords);
        Popup("slime-cross-barrier-grow", barrier, ("cube", ent.Owner));
        QueueDel(ent);
    }

    #endregion

    #region Устройство захвата

    /// <summary>capturedevice/attack(): только простые мирные существа.</summary>
    private void OnCaptureInteract(Entity<SlimeCaptureDeviceComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;
        args.Handled = true;

        var container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        if (container.ContainedEntity != null)
        {
            PopupUser("slime-cross-capture-full", args.User);
            return;
        }

        if (HasComp<HumanoidProfileComponent>(target))
        {
            PopupUser("slime-cross-capture-simple-only", args.User);
            return;
        }

        if (TryComp<MindContainerComponent>(target, out var mind) && mind.HasMind)
        {
            PopupUser("slime-cross-capture-refused", args.User, ("target", target));
            return;
        }

        if (AggressiveFactions.Any(f => _faction.IsMember(target, f)))
        {
            PopupUser("slime-cross-capture-aggressive", args.User);
            return;
        }

        _container.Insert(target, container);
        PopupUser("slime-cross-capture-stored", args.User, ("target", target));
    }

    private void OnCaptureUse(Entity<SlimeCaptureDeviceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        if (container.ContainedEntity is not { } stored)
        {
            PopupUser("slime-cross-capture-empty", args.User);
            return;
        }

        _container.Remove(stored, container, destination: At(args.User));
        PopupUser("slime-cross-capture-open", args.User);
    }

    #endregion

    #region Одежда

    private void OnRebreatherEquipped(Entity<SlimeRebreatherComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        AddEffect(args.Wearer, "rebreathing", null);
    }

    private void OnRebreatherUnequipped(Entity<SlimeRebreatherComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        RemoveEffect(args.Wearer, "rebreathing");
    }

    private void OnFlowerEquipped(Entity<SlimePeaceFlowerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        AddEffect(args.Wearer, "plur", TimeSpan.MaxValue - _timing.CurTime);
    }

    private void OnFlowerUnequipped(Entity<SlimePeaceFlowerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemoveEffect(args.Wearer, "plur");
    }

    /// <summary>peaceflower/can_mob_unequip(): сам себе не снимешь.</summary>
    private void OnFlowerUnequipAttempt(Entity<SlimePeaceFlowerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (args.User == args.UnEquipTarget)
        {
            PopupUser("slime-cross-peaceflower-stay", args.User);
            args.Cancel();
        }
    }

    /// <summary>Огнеупорность и герметичность зелий кроссбридинга.</summary>
    private void OnSpaceproofPressure(Entity<SlimePotionedComponent> ent, ref GetPressureProtectionValuesEvent args)
    {
        if (!ent.Comp.Spaceproof)
            return;
        args.LowPressureMultiplier = Math.Max(args.LowPressureMultiplier, 1000f);
        args.HighPressureMultiplier = Math.Min(args.HighPressureMultiplier, 0.5f);
    }

    #endregion

    #region Оружие

    /// <summary>rainbowknife/afterattack(): случайный тип урона.</summary>
    private void OnRainbowKnifeDamage(Entity<SlimeRainbowKnifeComponent> ent, ref GetMeleeDamageEvent args)
    {
        var total = args.Damage.GetTotal();
        if (total <= 0)
            return;
        var type = _random.Pick(new[] { "Slash", "Heat", "Poison", "Asphyxiation" });
        args.Damage = new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(type), total);
    }

    /// <summary>projectile/magic/bloodchill: замедление и обморожение.</summary>
    private void OnBloodChillHit(Entity<SlimeBloodChillProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (HasComp<MobStateComponent>(args.Target))
            AddEffect(args.Target, "bloodchill", TimeSpan.FromSeconds(10));
    }

    #endregion

    #region Призматические очки

    private void OnPrismGetActions(Entity<SlimePrismGlassesComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } slots || (slots & Content.Shared.Inventory.SlotFlags.EYES) == 0)
            return;
        args.AddAction(ref ent.Comp.ColourAction, PrismColourAction);
        args.AddAction(ref ent.Comp.PlaceAction, PrismPlaceAction);
    }

    private void OnPrismColour(Entity<SlimePrismGlassesComponent> ent, ref SlimePrismColourActionEvent args)
    {
        args.Handled = true;
        var index = Array.FindIndex(PrismColours, c => Color.FromHex(c) == ent.Comp.Color);
        ent.Comp.Color = Color.FromHex(PrismColours[(index + 1) % PrismColours.Length]);
        PopupUser("slime-cross-prism-colour", args.Performer, ("colour", ent.Comp.Color.ToHex()));
    }

    private void OnPrismPlace(Entity<SlimePrismGlassesComponent> ent, ref SlimePrismPlaceActionEvent args)
    {
        args.Handled = true;
        var coords = At(args.Performer).SnapToGrid(EntityManager);
        if (_lookup.GetEntitiesInRange<SlimeLightPrismComponent>(coords, 0.4f).Count > 0)
        {
            PopupUser("slime-cross-prism-occupied", args.Performer);
            return;
        }

        PopupUser("slime-cross-prism-place", args.Performer);
        var prism = Spawn("SlimeLightPrism", coords);
        _pointLight.SetColor(prism, ent.Comp.Color);
        SetTint(prism, "prism", ent.Comp.Color);
    }

    [Dependency] private readonly Robust.Server.GameObjects.PointLightSystem _pointLight = default!;

    private void OnLightPrismInteract(Entity<SlimeLightPrismComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        PopupUser("slime-cross-prism-dispel", args.User, ("prism", ent.Owner));
        QueueDel(ent);
    }

    #endregion

    #region Ледяной блок, камера, голем

    /// <summary>Существо выбралось из льда: неуязвимость снимается, блок тает.</summary>
    private void OnIceRemoved(Entity<SlimeIceStasisComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Entity != ent.Comp.Frozen)
            return;
        _godmode.DisableGodmode(args.Entity);
        ent.Comp.Frozen = null;
        if (TerminatingOrDeleted(ent))
            return;
        _audio.PlayPvs(new Robust.Shared.Audio.SoundCollectionSpecifier("GlassBreak"), At(args.Entity));
        QueueDel(ent);
    }

    private void OnRewindCameraUse(Entity<SlimeRewindCameraComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        RewindPhoto(ent, args.User, args.User);
    }

    private void OnRewindCameraInteract(Entity<SlimeRewindCameraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;
        args.Handled = true;
        RewindPhoto(ent, target, args.User);
    }

    /// <summary>camera/rewind/on_flash(): «запомни этот момент навсегда».</summary>
    private void RewindPhoto(Entity<SlimeRewindCameraComponent> ent, EntityUid target, EntityUid user)
    {
        PopupUser(target == user ? "slime-cross-rewind-selfie" : "slime-cross-rewind-photo", user, ("target", target));
        PopupUser("slime-cross-rewind-remember", target);
        StartDejavu(target, 2);
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Weapons/flash.ogg"), At(user));
        QueueDel(ent);
    }

    private void OnGolemInit(Entity<SlimeGolemComponent> ent, ref MapInitEvent args)
    {
        AddEffect(ent, "golem", null);
    }

    #endregion

    #region Зелья кроссбридинга

    private void OnCrossPotionInteract(Entity<SlimeCrossPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        var user = args.User;
        switch (ent.Comp.Kind)
        {
            // extract_cloner: копия обычного экстракта вместе с зарядами.
            case "extractcloner":
            {
                if (HasComp<SlimeCrossbreedComponent>(target))
                {
                    PopupUser("slime-cross-potion-too-complex", user, ("target", target));
                    args.Handled = true;
                    return;
                }

                if (!TryComp<SlimeExtractComponent>(target, out var extract))
                    return;
                args.Handled = true;
                if (extract.Recurring)
                {
                    PopupUser("slime-cross-potion-too-complex", user, ("target", target));
                    return;
                }

                var proto = MetaData(target).EntityPrototype?.ID;
                if (proto == null)
                    return;
                var copy = Spawn(proto, At(target));
                if (TryComp<SlimeExtractComponent>(copy, out var copyExtract))
                {
                    copyExtract.Uses = extract.Uses;
                    Dirty(copy, copyExtract);
                }

                PopupUser("slime-cross-potion-cloned", user, ("target", target));
                QueueDel(ent);
                return;
            }
            // peacepotion, lovepotion: кормление с do_after.
            case "peace":
            case "love":
            {
                if (!HasComp<MobStateComponent>(target))
                    return;
                args.Handled = true;
                if (_mobState.IsDead(target))
                {
                    PopupUser("slime-potion-living-only", user);
                    return;
                }

                // ismegafauna(): компонент мегафауны лаваленда ищется по имени, чтобы не зависеть от его сборки.
                if (EntityManager.ComponentFactory.TryGetRegistration("MegafaunaAi", out var megafauna) && HasComp(target, megafauna.Type))
                {
                    PopupUser("slime-cross-potion-evil", user, ("potion", ent.Owner));
                    return;
                }

                if (ent.Comp.Kind == "love")
                {
                    if (target == user)
                    {
                        PopupUser("slime-cross-potion-narcissist", user);
                        return;
                    }

                    if (HasEffect(target, "inlove"))
                    {
                        PopupUser("slime-cross-potion-already-love", user, ("target", target));
                        return;
                    }
                }

                Popup(target == user ? "slime-cross-potion-drink-start" : "slime-cross-potion-feed-start", target,
                    ("user", user), ("target", target), ("potion", ent.Owner));
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(ent.Comp.Kind == "love" ? 5 : 10),
                    new SlimeCrossPotionDoAfterEvent(), ent, target, ent)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                });
                return;
            }
            // spaceproof / lavaproof: обработка одежды и предметов, два применения.
            case "spaceproof":
            case "lavaproof":
            {
                var space = ent.Comp.Kind == "spaceproof";
                if (space && !HasComp<ClothingComponent>(target) || !space && HasComp<MobStateComponent>(target))
                    return;
                args.Handled = true;

                var potioned = EnsureComp<SlimePotionedComponent>(target);
                if (space && (potioned.Spaceproof || HasComp<PressureProtectionComponent>(target)))
                {
                    PopupUser("slime-cross-potion-already-spaceproof", user, ("target", target));
                    return;
                }

                if (space)
                {
                    potioned.Spaceproof = true;
                    EnsureComp<PressureProtectionComponent>(target);
                    _meta.SetEntityName(target, Loc.GetString("slime-cross-spaceproof-name", ("name", Name(target))));
                    PopupUser("slime-cross-potion-spaceproof", user, ("target", target));
                }
                else
                {
                    potioned.Lavaproof = true;
                    _meta.SetEntityName(target, Loc.GetString("slime-cross-lavaproof-name", ("name", Name(target))));
                    PopupUser("slime-cross-potion-lavaproof", user, ("target", target));
                }

                Dirty(target, potioned);
                ent.Comp.Uses--;
                if (ent.Comp.Uses <= 0)
                    QueueDel(ent);
                return;
            }
            // slime_reviver: оживляет мёртвого слайма ценой 10 здоровья.
            case "reviver":
            {
                if (!TryComp<SlimeComponent>(target, out _))
                {
                    if (HasComp<MobStateComponent>(target))
                    {
                        PopupUser("slime-cross-potion-slimes-only", user);
                        args.Handled = true;
                    }
                    return;
                }

                args.Handled = true;
                if (!_mobState.IsDead(target))
                {
                    PopupUser("slime-cross-potion-still-alive", user);
                    return;
                }

                if (!_thresholds.TryGetDeadThreshold(target, out var dead) || dead <= 10)
                {
                    PopupUser("slime-cross-potion-unstable", user);
                    return;
                }

                _rejuvenate.PerformRejuvenate(target);
                _thresholds.SetMobStateThreshold(target, dead.Value - 10, Content.Shared.Mobs.MobState.Dead);
                Popup("slime-cross-potion-revived", target, ("target", target));
                QueueDel(ent);
                return;
            }
            // chargedstabilizer: шанс мутации обнуляется.
            case "omnistabilizer":
            {
                if (!TryComp<SlimeComponent>(target, out var slime))
                    return;
                args.Handled = true;
                if (_mobState.IsDead(target))
                {
                    PopupUser("slime-potion-slime-dead", user);
                    return;
                }

                if (slime.MutationChance <= 0)
                {
                    PopupUser("slime-potion-stabilizer-max", user);
                    return;
                }

                slime.MutationChance = 0;
                Dirty(target, slime);
                PopupUser("slime-cross-potion-omnistabilizer", user);
                QueueDel(ent);
                return;
            }
        }
    }

    private void OnCrossPotionDoAfter(Entity<SlimeCrossPotionComponent> ent, ref SlimeCrossPotionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || TerminatingOrDeleted(ent))
            return;
        args.Handled = true;

        if (ent.Comp.Kind == "peace")
        {
            PopupUser(target == args.User ? "slime-cross-potion-drank" : "slime-cross-potion-fed", args.User, ("target", target), ("potion", ent.Owner));
            // TRAIT_PACIFISM / severe pacifism trauma: навсегда.
            EnsureComp<PacifiedComponent>(target);
        }
        else
        {
            PopupUser("slime-cross-potion-fed", args.User, ("target", target), ("potion", ent.Owner));
            PopupUser("slime-cross-potion-love", target, ("user", args.User));
            MakeAlly(target, args.User);
            AddEffect(target, "inlove", null);
        }

        QueueDel(ent);
    }

    #endregion
}

/// <summary>Снаряд кровавого охладителя.</summary>
[RegisterComponent]
public sealed partial class SlimeBloodChillProjectileComponent : Component;

/// <summary>Призма света.</summary>
[RegisterComponent]
public sealed partial class SlimeLightPrismComponent : Component;

/// <summary>camera/rewind: одна фотография, запоминающая момент.</summary>
[RegisterComponent]
public sealed partial class SlimeRewindCameraComponent : Component;

/// <summary>Адамантиновый голем: эффект голема при появлении.</summary>
[RegisterComponent]
public sealed partial class SlimeGolemComponent : Component;
