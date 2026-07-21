using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.Imperial.Medieval.GhostRevive;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.MedievalReviveSpawner;
using Content.Shared.Imperial.Medieval.Revive;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Content.Shared.Imperial.Medieval.MedievalReviveSpawner;
using Robust.Shared.Network;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    [UISystemDependency] private readonly GhostSystem? _system = default;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    // Imperial Medieval Revive
    private GhostReviveWindow? _windowRules = null;

    public override void Initialize()
    {
        base.Initialize();
        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
        system.ReviveCountResponse += OnReviveCountResponse; // Imperial Medieval Tweak
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
        system.ReviveCountResponse -= OnReviveCountResponse; // Imperial Medieval Tweak
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;
        Gui.Update(_system?.AvailableGhostRoleCount, _system?.Player?.CanReturnToBody);
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.UpdateWarps(msg.Warps);
        window.Populate();
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    // Imperial Medieval Revive Start
    private int reviveCount = 3;
    private void OnReviveCountResponse(ReviveCountResponseEvent ev)
    {
        reviveCount = ev.MaxCount - ev.CurrentCount;

        _windowRules = new GhostReviveWindow("Выбейте необходимое количество магической эссенции из злых духов вокруг и купите за нее магический ключ. После чего кликните ключом по особой двери и отправьтесь на респавн. Учтите, [color=red]ЗАХОДИТЬ ЗА ТОГО ЖЕ ПЕРСОНАЖА НЕЛЬЗЯ[/color]!! У вас осталось "+reviveCount+" жизней", _ =>
        {
            var msg = new GhostReviveRequestEvent();
            _net.SendSystemNetworkMessage(msg);
            _windowRules?.Close();
        });
        _windowRules.OnClose += () =>
        {
            _windowRules = null;
        };
        _windowRules.OpenCentered();
    }

    private void OnGhostReviveClicked()
    {
        var request = new ReviveCountRequestEvent();
        _net.SendSystemNetworkMessage(request);
    }

    // Imperial Medieval Revive End

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.TargetWindow.WarpClicked += OnWarpClicked;
        Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;
        Gui.GhostRevivePressed += OnGhostReviveClicked; // Imperial Medieval Revive

        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.GhostRolesPressed -= GhostRolesPressed; // Imperial Medieval Revive
        Gui.GhostRevivePressed -= OnGhostReviveClicked;
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;

        Gui.Hide();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    private void RequestWarps()
    {
        _system?.RequestWarps();
        Gui?.TargetWindow.Populate();
        Gui?.TargetWindow.OpenCentered();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }
}
