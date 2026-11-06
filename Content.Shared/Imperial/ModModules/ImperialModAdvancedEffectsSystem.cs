using Content.Shared.Gravity;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Эффекты модулей М3 на носителе: антигравитация и помощник стрелка.
/// </summary>
public sealed class ImperialModAdvancedEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModAntigravComponent, ComponentStartup>(OnAntigravChanged);
        SubscribeLocalEvent<ImperialModAntigravComponent, ComponentShutdown>(OnAntigravChanged);
        SubscribeLocalEvent<ImperialModAntigravComponent, IsWeightlessEvent>(OnAntigravWeightless);

        SubscribeLocalEvent<ImperialModShootingAssistantComponent, ComponentShutdown>(OnAssistantShutdown);
        SubscribeLocalEvent<ImperialModShootingAssistantComponent, AfterAutoHandleStateEvent>(OnAssistantState);
        SubscribeLocalEvent<ImperialModShootingAssistantComponent, DidEquipHandEvent>(OnAssistantEquipHand);
        SubscribeLocalEvent<ImperialModShootingAssistantComponent, DidUnequipHandEvent>(OnAssistantUnequipHand);
        SubscribeLocalEvent<ImperialModShootingAssistantComponent, RefreshMovementSpeedModifiersEvent>(OnAssistantSpeed);
        SubscribeLocalEvent<ImperialModAssistedGunComponent, GunRefreshModifiersEvent>(OnAssistedGunRefresh);
        SubscribeLocalEvent<ImperialModAssistedGunComponent, ComponentShutdown>(OnAssistedGunShutdown);
    }

    #region Антигравитация

    private void OnAntigravChanged<T>(Entity<ImperialModAntigravComponent> ent, ref T args)
    {
        _gravity.RefreshWeightless(ent.Owner);
    }

    private void OnAntigravWeightless(Entity<ImperialModAntigravComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled || ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.IsWeightless = true;
        args.Handled = true;
    }

    #endregion

    #region Помощник стрелка

    /// <summary>Сменить режим и обновить оружие в руках.</summary>
    public void SetMode(Entity<ImperialModShootingAssistantComponent> ent, ImperialModShootingMode mode)
    {
        ent.Comp.Mode = mode;
        Dirty(ent);
        RefreshHeld(ent);
    }

    private void OnAssistantState(Entity<ImperialModShootingAssistantComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshHeld(ent);
    }

    private void RefreshHeld(Entity<ImperialModShootingAssistantComponent> ent)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (HasComp<GunComponent>(held))
                MarkGun(held, ent.Comp.Mode);
        }

        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void MarkGun(EntityUid gun, ImperialModShootingMode mode)
    {
        if (mode == ImperialModShootingMode.Off)
        {
            RemComp<ImperialModAssistedGunComponent>(gun);
            return;
        }

        var assisted = EnsureComp<ImperialModAssistedGunComponent>(gun);
        assisted.Mode = mode;
        Dirty(gun, assisted);
        _gun.RefreshModifiers(gun);
    }

    private void OnAssistantShutdown(Entity<ImperialModShootingAssistantComponent> ent, ref ComponentShutdown args)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
            RemComp<ImperialModAssistedGunComponent>(held);

        if (!TerminatingOrDeleted(ent))
            _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnAssistantEquipHand(Entity<ImperialModShootingAssistantComponent> ent, ref DidEquipHandEvent args)
    {
        if (HasComp<GunComponent>(args.Equipped))
            MarkGun(args.Equipped, ent.Comp.Mode);
    }

    private void OnAssistantUnequipHand(Entity<ImperialModShootingAssistantComponent> ent, ref DidUnequipHandEvent args)
    {
        RemComp<ImperialModAssistedGunComponent>(args.Unequipped);
    }

    private void OnAssistantSpeed(Entity<ImperialModShootingAssistantComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // movespeed_modifier/shooting_assistant: multiplicative_slowdown 0.5.
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running && ent.Comp.Mode == ImperialModShootingMode.Sharpshooter)
            args.ModifySpeed(0.8f, 0.8f);
    }

    private void OnAssistedGunShutdown(Entity<ImperialModAssistedGunComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _gun.RefreshModifiers(ent.Owner);
    }

    private void OnAssistedGunRefresh(Entity<ImperialModAssistedGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        switch (ent.Comp.Mode)
        {
            // TRAIT_DOUBLE_TAP + разброс +15..25°.
            case ImperialModShootingMode.Stormtrooper:
                args.FireRate *= 2f;
                args.MinAngle += Angle.FromDegrees(15);
                args.MaxAngle += Angle.FromDegrees(25);
                break;
            // Разброс −20..−10°.
            case ImperialModShootingMode.Sharpshooter:
                args.MinAngle = Angle.FromDegrees(Math.Max(0, args.MinAngle.Degrees - 20));
                args.MaxAngle = Angle.FromDegrees(Math.Max(args.MinAngle.Degrees, args.MaxAngle.Degrees - 10));
                break;
        }
    }

    #endregion
}
