using System.Collections.Generic;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Imperial.Medieval.Achievements.UI;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Achievements;

public sealed class AchievementUIController : UIController,
    IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>,
    IOnStateEntered<LobbyState>,    IOnStateExited<LobbyState>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    [UISystemDependency] private readonly AchievementSystem _achievementSystem = default!;

    private const int MaxNotifications = 4;

    private BoxContainer? _notificationStack;
    private LayoutContainer? _canvas;

    private AchievementTreeMenuWindow? _menuWindow;

    private readonly AchievementTreeViewState _treeViewState = new();

    private HashSet<string> _cachedUnlocked = new();
    private Dictionary<string, float> _cachedPercents = new();
    private Dictionary<string, Dictionary<string, int>> _cachedProgress = new();

    private readonly Queue<string> _notificationQueue = new();
    private bool _isUiReady = false;
    private bool _hasCachedData = false;

    public override void Initialize()
    {
        base.Initialize();

        _proto.PrototypesReloaded += OnPrototypesReloaded;

        _isUiReady = true;
        ProcessQueue();
    }

    public void OnStateEntered(GameplayState state) => SetupUI();
    public void OnStateExited(GameplayState state)  => TeardownUI();
    public void OnStateEntered(LobbyState state)    => SetupUI();
    public void OnStateExited(LobbyState state)     => TeardownUI();

    private void SetupUI()
    {
        if (_canvas != null)
            return;

        _isUiReady = true;
        EnsureCanvas();
        ProcessQueue();
    }

    private void TeardownUI()
    {
        _isUiReady = false;

        if (_canvas != null)
        {
            _canvas.Orphan();
            _canvas.Dispose();
            _canvas = null;
            _notificationStack = null;
        }

        _menuWindow?.Close();
        _menuWindow = null;

        _cachedUnlocked.Clear();
        _cachedPercents.Clear();
        _cachedProgress.Clear();
        _notificationQueue.Clear();
        _hasCachedData = false;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.ContainsKey(typeof(AchievementPrototype)))
            return;

        if (_menuWindow == null || _menuWindow.Disposed)
            return;

        _menuWindow.Populate(_cachedUnlocked, _cachedPercents, _cachedProgress);
    }

    public void ToggleMenu()
    {
        if (_menuWindow != null && !_menuWindow.Disposed)
        {
            _menuWindow.Close();
            return;
        }

        var spriteSystem = EntityManager.System<SpriteSystem>();
        _menuWindow = new AchievementTreeMenuWindow(_proto, _cache, spriteSystem, _treeViewState);
        _menuWindow.OnClose += OnWindowClosed;
        _menuWindow.OpenCentered();

        if (_hasCachedData)
            _menuWindow.Populate(_cachedUnlocked, _cachedPercents, _cachedProgress);

        _achievementSystem?.RequestMenuData();
    }

    private void OnWindowClosed()
    {
        _menuWindow = null;
    }

    public void UpdateMenuData(
        HashSet<string> unlocked,
        Dictionary<string, float> percents,
        Dictionary<string, Dictionary<string, int>> progress)
    {
        _cachedUnlocked = unlocked;
        _cachedPercents = percents;
        _cachedProgress = progress;
        _hasCachedData  = true;

        if (_menuWindow == null || _menuWindow.Disposed)
            return;

        _menuWindow.Populate(unlocked, percents, progress);
    }

    public void QueueNotification(string achId)
    {
        _notificationQueue.Enqueue(achId);

        if (_isUiReady)
            ProcessQueue();
    }

    private void ProcessQueue()
    {
        if (_notificationStack == null)
            EnsureCanvas();

        while (_notificationQueue.Count > 0)
        {
            var achId = _notificationQueue.Dequeue();
            ShowNotification(achId);
        }
    }

    private void ShowNotification(string achId)
    {
        if (!_proto.TryIndex<AchievementPrototype>(achId, out var prototype))
            return;

        _cachedUnlocked.Add(achId);

        if (_menuWindow != null && !_menuWindow.Disposed)
            _menuWindow.Populate(_cachedUnlocked, _cachedPercents, _cachedProgress);

        if (_notificationStack?.ChildCount >= MaxNotifications)
            return;

        var spriteSystem = EntityManager.System<SpriteSystem>();
        var audioSystem  = EntityManager.System<AudioSystem>();

        audioSystem.PlayGlobal(prototype.AchievementSound, Filter.Local(), false, AudioParams.Default);

        var percent = _cachedPercents.GetValueOrDefault(achId, 100f);
        var widget = new AchievementWidget(prototype, _cache, spriteSystem, _proto, percent);
        _notificationStack?.AddChild(widget);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_notificationStack == null || _notificationStack.ChildCount == 0)
            return;

        var uiScale = _config.GetCVar(CVars.DisplayUIScale);
        if (uiScale == 0f)
            uiScale = UIManager.DefaultUIScale;

        for (var i = _notificationStack.ChildCount - 1; i >= 0; i--)
        {
            if (_notificationStack.GetChild(i) is not AchievementWidget widget)
                continue;

            widget.UpdateAnimation(args.DeltaSeconds, uiScale);

            if (widget.Timer >= widget.DisplayTime)
                widget.Orphan();
        }
    }

    private void EnsureCanvas()
    {
        if (_canvas != null && !_canvas.Disposed)
            return;

        _canvas = new LayoutContainer { MouseFilter = Control.MouseFilterMode.Ignore };
        UIManager.RootControl.AddChild(_canvas);
        LayoutContainer.SetAnchorPreset(_canvas, LayoutContainer.LayoutPreset.Wide);

        _notificationStack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = Control.VAlignment.Bottom,
            HorizontalAlignment = Control.HAlignment.Right,
            MouseFilter = Control.MouseFilterMode.Ignore
        };

        _canvas.AddChild(_notificationStack);

        LayoutContainer.SetAnchorPreset(_notificationStack, LayoutContainer.LayoutPreset.BottomRight);
        LayoutContainer.SetMarginRight(_notificationStack, 0);
        LayoutContainer.SetMarginBottom(_notificationStack, 0);
        LayoutContainer.SetGrowHorizontal(_notificationStack, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetGrowVertical(_notificationStack, LayoutContainer.GrowDirection.Begin);
    }
}
