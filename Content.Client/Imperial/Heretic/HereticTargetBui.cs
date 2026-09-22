using System;
using System.Collections.Generic;
using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticTargetBui : BoundUserInterface
{
    [Dependency] private readonly ISharedPlayerManager _playerMgr = default!;

    private SimpleRadialMenu? _menu;
    private HereticTargetBuiState? _lastState;

    public HereticTargetBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        OpenRadialMenu();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticTargetBuiState s) return;
        _lastState = s;
        _menu?.SetButtons(BuildButtons(s));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private void OpenRadialMenu()
    {
        _menu = this.CreateWindow<SimpleRadialMenu>();
        if (_lastState != null)
            _menu.SetButtons(BuildButtons(_lastState));
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildButtons(HereticTargetBuiState state)
    {
        var buttons = new List<RadialMenuOptionBase>();
        foreach (var data in state.Targets)
        {
            var uid = EntMan.GetEntity(data.Entity);
            var isDead = data.IsDead;
            if (EntMan.TryGetComponent(uid, out MobStateComponent? mob))
                isDead = mob.CurrentState == MobState.Dead;

            var option = new RadialMenuActionOption<EntityUid>(OnTargetSelected, uid)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(uid),
                ToolTip = $"{(isDead ? "☠" : "♥")} {data.Name}",
            };
            buttons.Add(option);
        }
        return buttons;
    }

    private void OnTargetSelected(EntityUid uid)
    {
        EntMan.System<HereticHeartbeatSystem>().ShowArrow(uid);
        AnnounceDistance(uid);
        SendMessage(new HereticTargetSelectedMessage());
    }

    private void AnnounceDistance(EntityUid target)
    {
        var player = _playerMgr.LocalSession?.AttachedEntity;
        if (player == null) return;

        var xformSys = EntMan.System<SharedTransformSystem>();
        if (!EntMan.TryGetComponent(player.Value, out TransformComponent? px)) return;
        if (!EntMan.TryGetComponent(target, out TransformComponent? tx)) return;
        if (px.MapID != tx.MapID) return;

        var dist = (xformSys.GetWorldPosition(tx) - xformSys.GetWorldPosition(px)).Length();
        var msg = dist < 5f
            ? Loc.GetString("heretic-heartbeat-target-very-close")
            : dist < 15f
                ? Loc.GetString("heretic-heartbeat-target-close")
                : Loc.GetString("heretic-heartbeat-target-far");

        EntMan.System<PopupSystem>().PopupClient(msg, player.Value, player.Value);
    }
}
