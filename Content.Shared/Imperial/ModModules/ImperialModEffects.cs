using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// magboot: носитель не теряет сцепление в невесомости, пока стоит на гриде.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModMagnetComponent : Component;

/// <summary>
/// jetpack (ion): носитель может двигаться в невесомости без газа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModJetpackUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Acceleration = 1f;

    [DataField, AutoNetworkedField]
    public float Friction = 0.25f;

    [DataField, AutoNetworkedField]
    public float WeightlessModifier = 1.2f;
}

/// <summary>
/// slowdown модулей: множитель скорости носителя.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModSpeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Walk = 1f;

    [DataField, AutoNetworkedField]
    public float Sprint = 1f;
}

/// <summary>
/// Защита, которую модуль добавляет части костюма (rad_protection и т. п.).
/// Не конфликтует с собственной Armor части.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModArmorComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}

public sealed class ImperialModEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModMagnetComponent, ComponentStartup>(OnMagnetChanged);
        SubscribeLocalEvent<ImperialModMagnetComponent, ComponentShutdown>(OnMagnetChanged);
        SubscribeLocalEvent<ImperialModMagnetComponent, IsWeightlessEvent>(OnIsWeightless);

        SubscribeLocalEvent<ImperialModJetpackUserComponent, ComponentStartup>(OnJetpackChanged);
        SubscribeLocalEvent<ImperialModJetpackUserComponent, ComponentShutdown>(OnJetpackChanged);
        SubscribeLocalEvent<ImperialModJetpackUserComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove);
        SubscribeLocalEvent<ImperialModJetpackUserComponent, RefreshWeightlessModifiersEvent>(OnWeightlessModifiers);

        SubscribeLocalEvent<ImperialModSpeedComponent, ComponentStartup>(OnSpeedChanged);
        SubscribeLocalEvent<ImperialModSpeedComponent, ComponentShutdown>(OnSpeedChanged);
        SubscribeLocalEvent<ImperialModSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        SubscribeLocalEvent<ImperialModArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ImperialModEmpProtectedComponent, EmpAttemptEvent>(OnEmpAttempt);
    }

    private void OnMagnetChanged<T>(Entity<ImperialModMagnetComponent> ent, ref T args)
    {
        _gravity.RefreshWeightless(ent.Owner);
    }

    private void OnIsWeightless(Entity<ImperialModMagnetComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled || ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!_gravity.EntityOnGravitySupportingGridOrMap(ent.Owner))
            return;

        args.IsWeightless = false;
        args.Handled = true;
    }

    private void OnJetpackChanged<T>(Entity<ImperialModJetpackUserComponent> ent, ref T args)
    {
        _speed.RefreshWeightlessModifiers(ent);
    }

    private void OnCanWeightlessMove(Entity<ImperialModJetpackUserComponent> ent, ref CanWeightlessMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.CanMove = true;
    }

    private void OnWeightlessModifiers(Entity<ImperialModJetpackUserComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.WeightlessAcceleration = ent.Comp.Acceleration;
        args.WeightlessModifier = ent.Comp.WeightlessModifier;
        args.WeightlessFriction = ent.Comp.Friction;
        args.WeightlessFrictionNoInput = ent.Comp.Friction;
    }

    private void OnSpeedChanged<T>(Entity<ImperialModSpeedComponent> ent, ref T args)
    {
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshSpeed(Entity<ImperialModSpeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ModifySpeed(ent.Comp.Walk, ent.Comp.Sprint);
    }

    private void OnEmpAttempt(Entity<ImperialModEmpProtectedComponent> ent, ref EmpAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDamageModify(Entity<ImperialModArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, ent.Comp.Modifiers);
    }
}

/// <summary>
/// waddle: носитель ходит вразвалочку (покачивание спрайта на клиенте).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModWaddleComponent : Component
{
    /// <summary>Наибольший наклон в градусах.</summary>
    [DataField, AutoNetworkedField]
    public float Angle = 8f;

    /// <summary>Шагов вразвалку в секунду.</summary>
    [DataField, AutoNetworkedField]
    public float Frequency = 2.5f;
}
