using Content.Server.Construction.Completions;
using Content.Server.Salvage;
using Content.Shared.Database;
using Content.Server.Administration.Logs;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.ImperialBorgs;
using Content.Shared.Imperial.ImperialBorgs.Events;
using Content.Shared.Popups;
using Content.Shared.Actions.Components;

namespace Content.Server.Imperial.ImperialBorgs;

public sealed class EnergyZiptiesSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgEnergyZiptiesComponent, GetItemActionsEvent>(GetStartAction);
        SubscribeLocalEvent<BorgEnergyZiptiesComponent, BorgEnergyZiptiesAction>(OnUseAction);
    }

    private void GetStartAction(EntityUid uid, BorgEnergyZiptiesComponent component, GetItemActionsEvent args)
    {

        args.AddAction(ref component.Action, component.StartAction);
        component.User = args.User;
        if (component.Action == null)
            return;
        TryComp<ActionComponent>(component.Action.Value, out var actionComp);
        var zipties = SpawnAttachedTo(component.ZiptiesPrototype, Transform(uid).Coordinates);
        _actionsSystem.SetEntityIcon(component.Action.Value, zipties);
        _entMan.DeleteEntity(zipties);
    }

    private void OnUseAction(EntityUid uid, BorgEnergyZiptiesComponent component, BorgEnergyZiptiesAction args)
    {
        if (args.Handled)
            return;
        if (!_hands.ActiveHandIsEmpty(uid))
        {
            _popup.PopupPredicted(Loc.GetString("Borg-energy-zipties-no-empty"), uid, uid, type: PopupType.MediumCaution);
        }
        var zipties = SpawnAttachedTo(component.ZiptiesPrototype, Transform(uid).Coordinates);
        if (!_hands.TryPickup(component.User, zipties))
        {
            _entMan.DeleteEntity(zipties);
            return;
        }
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(component.User)} received {ToPrettyString(zipties)}");

        args.Handled = true;
    }
}
