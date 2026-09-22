using System;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

/// <summary>
/// HeartbeatMansus window — shows all 5 named targets with alive/dead status,
/// and a directional arrow toward the selected one.
/// </summary>
public sealed class HereticTargetWindow : DefaultWindow
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _playerMgr;
    private readonly SharedTransformSystem _xformSys;

    private EntityUid? _selectedTarget;
    private readonly Label _selectedLabel;
    private readonly DirectionIcon _dirIcon;
    private readonly BoxContainer _targetList;

    /// <summary>Вызывается при клике на строку цели. Передаёт EntityUid выбранной цели.</summary>
    public event Action<EntityUid>? OnTargetClicked;

    // Color palette
    private static readonly Color AliveColor = Color.FromHex("#55dd55");
    private static readonly Color DeadColor  = Color.FromHex("#dd4444");
    private static readonly Color HeaderGold = Color.FromHex("#e0c070");

    public HereticTargetWindow()
    {
        _entMan    = IoCManager.Resolve<IEntityManager>();
        _playerMgr = IoCManager.Resolve<IPlayerManager>();
        _xformSys  = _entMan.System<SharedTransformSystem>();

        Title = "Цели Мансуса";
        MinSize = new Vector2(340, 280);

        // ── Root layout: target list on the left, tracker on the right ──────
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        Contents.AddChild(root);

        // Left: scrollable list of targets
        var listScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            MinWidth = 190,
        };
        root.AddChild(listScroll);

        _targetList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        listScroll.AddChild(_targetList);

        // Right: direction tracker panel
        var trackerPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 110,
            HorizontalExpand = false,
        };
        root.AddChild(trackerPanel);

        var trackerHeader = new Label
        {
            Text = "Направление:",
            FontColorOverride = HeaderGold,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        trackerPanel.AddChild(trackerHeader);

        _selectedLabel = new Label
        {
            Text = "—",
            HorizontalAlignment = Control.HAlignment.Center,
            ClipText = true,
        };
        trackerPanel.AddChild(_selectedLabel);

        // The direction icon — 80×80 px, south-pointing arrow by default (unknown)
        _dirIcon = new DirectionIcon(snap: false)
        {
            MinSize = new Vector2(80, 80),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };
        trackerPanel.AddChild(_dirIcon);

        var hint = new Label
        {
            Text = "(нажмите на цель)",
            FontColorOverride = Color.Gray,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        trackerPanel.AddChild(hint);
    }

    /// <summary>
    /// Rebuilds the target list from the BUI state snapshot.
    /// </summary>
    public void Populate(HereticTargetBuiState state)
    {
        _targetList.DisposeAllChildren();

        if (state.Targets.Count == 0)
        {
            var empty = new Label
            {
                Text = "Нет именных целей.",
                FontColorOverride = Color.Gray,
            };
            _targetList.AddChild(empty);
            return;
        }

        foreach (var data in state.Targets)
        {
            var localUid = _entMan.GetEntity(data.Entity);

            // Check live mob state if available (more up-to-date than the snapshot).
            var isDead = data.IsDead;
            if (_entMan.TryGetComponent(localUid, out MobStateComponent? mobComp))
                isDead = mobComp.CurrentState == MobState.Dead;

            var row = BuildTargetRow(localUid, data.Name, isDead);
            _targetList.AddChild(row);
        }
    }

    private Control BuildTargetRow(EntityUid uid, string name, bool isDead)
    {
        var btn = new Button
        {
            ToggleMode = true,
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };

        // Status symbol: ♥ alive / ☠ dead
        var statusLabel = new Label
        {
            Text  = isDead ? "☠" : "♥",
            FontColorOverride = isDead ? DeadColor : AliveColor,
        };

        var nameLabel = new Label
        {
            Text = name,
            ClipText = true,
            HorizontalExpand = true,
        };
        if (isDead)
            nameLabel.FontColorOverride = DeadColor;

        row.AddChild(statusLabel);
        row.AddChild(nameLabel);
        btn.AddChild(row);

        btn.OnPressed += _ =>
        {
            _selectedTarget = uid;
            _selectedLabel.Text = name;
            OnTargetClicked?.Invoke(uid);
        };

        return btn;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateDirectionIcon();
    }

    private void UpdateDirectionIcon()
    {
        if (_selectedTarget == null || !_entMan.EntityExists(_selectedTarget.Value))
        {
            _dirIcon.SetOnlyStyleClass(DirectionIcon.StyleClassDirectionIconUnknown);
            return;
        }

        if (_playerMgr.LocalEntity is not { } player)
        {
            _dirIcon.SetOnlyStyleClass(DirectionIcon.StyleClassDirectionIconUnknown);
            return;
        }

        if (!_entMan.TryGetComponent(player, out TransformComponent? playerXform) ||
            !_entMan.TryGetComponent(_selectedTarget.Value, out TransformComponent? targetXform))
        {
            _dirIcon.SetOnlyStyleClass(DirectionIcon.StyleClassDirectionIconUnknown);
            return;
        }

        // Abort if the entities are on different maps (e.g., target is in maintenance,
        // heretic is on an away site). Direction would be meaningless.
        if (playerXform.MapID != targetXform.MapID)
        {
            _dirIcon.SetOnlyStyleClass(DirectionIcon.StyleClassDirectionIconUnknown);
            return;
        }

        var playerPos = _xformSys.GetWorldPosition(playerXform);
        var targetPos = _xformSys.GetWorldPosition(targetXform);
        var delta = targetPos - playerPos;

        // Use world-space north-up direction (relativeAngle = 0).
        // The arrow points true north = up on the screen.
        _dirIcon.UpdateDirection(delta, Angle.Zero);
    }
}
