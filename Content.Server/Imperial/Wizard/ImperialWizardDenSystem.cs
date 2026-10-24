using System.Linq;
using Content.Server.Administration;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>/obj/structure/mirror/magic: кроме причёски, позволяет сменить имя.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardMagicMirrorComponent : Component;

/// <summary>/obj/machinery/computer/camera_advanced логова: свободный глаз над станцией.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardScryingConsoleComponent : Component
{
    [DataField]
    public EntProtoId Eye = "ImperialWizardScryingEye";

    [DataField]
    public EntProtoId ExitAction = "ImperialActionScryingExit";
}

/// <summary>Тот, кто сейчас смотрит через консоль.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardScryingUserComponent : Component
{
    [ViewVariables]
    public EntityUid Eye;

    [ViewVariables]
    public EntityUid Console;

    [ViewVariables]
    public EntityUid? Action;
}

/// <summary>Логово мага TG: волшебное зеркало и консоль наблюдения.</summary>
public sealed class ImperialWizardDenSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ImperialWizardTeleportSystem _teleport = default!;

    private const int MaxNameLength = 32;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialWizardMagicMirrorComponent, GetVerbsEvent<AlternativeVerb>>(OnMirrorVerbs);

        SubscribeLocalEvent<ImperialWizardScryingConsoleComponent, ActivateInWorldEvent>(OnConsoleActivate);
        SubscribeLocalEvent<ImperialWizardScryingConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ImperialWizardScryingUserComponent, ImperialScryingExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<ImperialWizardScryingUserComponent, DamageChangedEvent>(OnUserDamaged);
        SubscribeLocalEvent<ImperialWizardScryingUserComponent, MobStateChangedEvent>(OnUserStateChanged);
        SubscribeLocalEvent<ImperialWizardScryingUserComponent, PlayerDetachedEvent>(OnUserDetached);
        SubscribeLocalEvent<ImperialWizardScryingUserComponent, ComponentShutdown>(OnUserShutdown);
    }

    #region Зеркало

    private void OnMirrorVerbs(Entity<ImperialWizardMagicMirrorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        var user = args.User;
        var session = actor.PlayerSession;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-wizard-mirror-rename"),
            Act = () => _dialog.OpenDialog<string>(session, Loc.GetString("imperial-wizard-mirror-rename"),
                Loc.GetString("imperial-wizard-mirror-rename-prompt"), name =>
                {
                    name = name.Trim();
                    if (name.Length == 0 || name.Length > MaxNameLength || TerminatingOrDeleted(user))
                        return;

                    _meta.SetEntityName(user, name);
                    _popup.PopupEntity(Loc.GetString("imperial-wizard-mirror-renamed", ("name", name)), user, user);
                }),
        });
    }

    #endregion

    #region Консоль наблюдения

    /// <summary>camera_advanced/CreateEye(): глаз появляется у случайной зоны станции.</summary>
    private void OnConsoleActivate(Entity<ImperialWizardScryingConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || HasComp<ImperialWizardScryingUserComponent>(args.User))
            return;

        var areas = _teleport.GetAreas().Values.ToList();
        if (areas.Count == 0 || !_teleport.TryGetDestination(_random.Pick(areas), out var dest))
        {
            _popup.PopupEntity(Loc.GetString("imperial-wizard-scrying-no-station"), ent, args.User);
            return;
        }

        args.Handled = true;
        var user = args.User;
        var eye = Spawn(ent.Comp.Eye, dest);
        var scrying = EnsureComp<ImperialWizardScryingUserComponent>(user);
        scrying.Eye = eye;
        scrying.Console = ent;
        _actions.AddAction(user, ref scrying.Action, ent.Comp.ExitAction);
        _mover.SetRelay(user, eye);
        _eye.SetTarget(user, eye);
        _eye.SetDrawFov(user, false);
    }

    private void OnExitAction(Entity<ImperialWizardScryingUserComponent> ent, ref ImperialScryingExitActionEvent args)
    {
        args.Handled = true;
        RemComp<ImperialWizardScryingUserComponent>(ent);
    }

    private void OnUserDamaged(Entity<ImperialWizardScryingUserComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            RemComp<ImperialWizardScryingUserComponent>(ent);
    }

    private void OnUserStateChanged(Entity<ImperialWizardScryingUserComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            RemComp<ImperialWizardScryingUserComponent>(ent);
    }

    private void OnUserDetached(Entity<ImperialWizardScryingUserComponent> ent, ref PlayerDetachedEvent args)
    {
        RemComp<ImperialWizardScryingUserComponent>(ent);
    }

    private void OnConsoleShutdown(Entity<ImperialWizardScryingConsoleComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<ImperialWizardScryingUserComponent>();
        while (query.MoveNext(out var uid, out var user))
        {
            if (user.Console == ent.Owner)
                RemCompDeferred<ImperialWizardScryingUserComponent>(uid);
        }
    }

    /// <summary>remove_eye(): вернуть взгляд в тело.</summary>
    private void OnUserShutdown(Entity<ImperialWizardScryingUserComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RemComp<RelayInputMoverComponent>(ent);
        _eye.SetTarget(ent, null);
        _eye.SetDrawFov(ent, true);
        _actions.RemoveAction(ent.Owner, ent.Comp.Action);
        QueueDel(ent.Comp.Eye);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Отошёл от консоли — связь рвётся.
        var query = EntityQueryEnumerator<ImperialWizardScryingUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var user, out var xform))
        {
            if (TerminatingOrDeleted(user.Console) || TerminatingOrDeleted(user.Eye)
                || !_transform.InRange(xform.Coordinates, Transform(user.Console).Coordinates, 1.5f))
                RemCompDeferred<ImperialWizardScryingUserComponent>(uid);
        }
    }

    #endregion
}
