using System.Numerics;
using Content.Client.Popups;
using Content.Shared.Imperial.Changeling;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Changeling;

/// <summary>
/// /atom/movable/screen/ling/chems и /atom/movable/screen/ling/sting на левом краю экрана
/// (ui_lingchemdisplay = "EAST,CENTER-1:15", ui_lingstingdisplay = "EAST:-6,CENTER-3:11").
/// </summary>
public sealed class ImperialChangelingHudSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly ResPath Hud = new("/Textures/Imperial/Changeling/hud.rsi");

    /// <summary>Иконки HUD TG 32×32 при масштабе 2.</summary>
    private const float Scale = 2f;

    private ChemDisplay? _chems;
    private StingDisplay? _sting;

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveHud();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !TryComp<ImperialChangelingComponent>(local, out var ling))
        {
            RemoveHud();
            return;
        }

        if (_chems == null || _sting == null)
            CreateHud();

        var root = _ui.WindowRoot;
        var center = root.Size.Y / 2f;
        var tile = 32 * Scale;

        // Правая колонка экрана (EAST): сразу слева от алертов, как экранные объекты TG рядом со статусами.
        var right = root.Size.X;
        if (_ui.ActiveScreen?.GetWidget<Content.Client.UserInterface.Systems.Alerts.Widgets.AlertsUI>() is { } alerts && alerts.Visible)
            right = alerts.GlobalPosition.X;
        var left = right - tile;

        // CENTER-1:15 и EAST:-6,CENTER-3:11 (в TG y растёт вверх).
        LayoutContainer.SetPosition(_chems!, new Vector2(left, center + tile / 2 - 15 * Scale));
        LayoutContainer.SetPosition(_sting!, new Vector2(left - 6 * Scale, center + tile * 2.5f - 11 * Scale));

        _chems!.Chemicals = ling.Chemicals;
        _chems.MaxChemicals = ling.MaxChemicals;

        // Иконка жала невидима, пока жало не выбрано.
        if (ling.ChosenSting is { } chosen && _proto.TryIndex(chosen, out var power))
        {
            _sting!.Visible = true;
            _sting.TextureNormal = _sprite.Frame0(new SpriteSpecifier.Rsi(Hud, power.Icon));
        }
        else
        {
            _sting!.Visible = false;
        }
    }

    private void CreateHud()
    {
        RemoveHud();
        var font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
        _chems = new ChemDisplay(_sprite.Frame0(new SpriteSpecifier.Rsi(Hud, "power_display")), font, _popup);
        _sting = new StingDisplay(() => RaiseNetworkEvent(new ImperialChangelingUnsetStingEvent()));
        // HUD под окнами, как экранные объекты TG под окнами tgui.
        _ui.WindowRoot.AddChild(_chems);
        _ui.WindowRoot.AddChild(_sting);
        _chems.SetPositionInParent(0);
        _sting.SetPositionInParent(0);
    }

    private void RemoveHud()
    {
        _chems?.Orphan();
        _sting?.Orphan();
        _chems = null;
        _sting = null;
    }

    /// <summary>/atom/movable/screen/ling/chems: число химикатов, при наведении — максимум.</summary>
    private sealed class ChemDisplay : Control
    {
        /// <summary>FORMAT_CHEM_CHARGES_TEXT и FORMAT_CHEM_MAX_TEXT.</summary>
        private static readonly Color ChargesColor = Color.FromHex("#dd66dd");
        private static readonly Color MaxColor = Color.FromHex("#dd2828");

        private readonly Texture _texture;
        private readonly Font _font;
        private readonly PopupSystem _popup;
        private bool _hovering;

        public float Chemicals;
        public float MaxChemicals;

        public ChemDisplay(Texture texture, Font font, PopupSystem popup)
        {
            _texture = texture;
            _font = font;
            _popup = popup;
            MouseFilter = MouseFilterMode.Stop;
            SetSize = new Vector2(32 * Scale, 32 * Scale);
        }

        protected override void MouseEntered()
        {
            base.MouseEntered();
            _hovering = true;
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            _hovering = false;
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _popup.PopupCursor(Robust.Shared.Localization.Loc.GetString("imperial-changeling-hud-chems-help"));
            args.Handle();
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            handle.DrawTextureRect(_texture, PixelSizeBox);

            // <div align='center' valign='middle' style='left:6px'>
            var text = ((int) MathF.Round(_hovering ? MaxChemicals : Chemicals)).ToString();
            var size = handle.GetDimensions(_font, text, UIScale);
            var position = (PixelSize - size) / 2f + new Vector2(6 * Scale * UIScale, 0);
            handle.DrawString(_font, position, text, UIScale, _hovering ? MaxColor : ChargesColor);
        }
    }

    /// <summary>/atom/movable/screen/ling/sting: щелчок убирает жало.</summary>
    private sealed class StingDisplay : TextureButton
    {
        public StingDisplay(Action unset)
        {
            Scale = new Vector2(ImperialChangelingHudSystem.Scale, ImperialChangelingHudSystem.Scale);
            DefaultCursorShape = CursorShape.Hand;
            Visible = false;
            OnPressed += _ => unset();
        }
    }
}
