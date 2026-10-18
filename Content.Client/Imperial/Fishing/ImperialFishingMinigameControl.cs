using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// HUD мини-игры (/atom/movable/screen/fishing_hud): фон, рамка, приманка, рыба и полоса прогресса.
/// Пиксели спрайта 32×96 рисуются с увеличением.
/// </summary>
public sealed class ImperialFishingMinigameControl : Control
{
    private static readonly ResPath HudRsi = new("/Textures/Imperial/Fishing/hud.rsi");

    private const int Scale = 3;
    private const int CompletionTop = 14;
    private const int CompletionBottom = 93;

    private readonly SpriteSystem _sprite;

    private ImperialFishingMinigameState? _state;
    private float _shownBait;
    private float _shownFish;
    private float _shownCompletion;
    private float _fishAlpha = 1f;

    /// <summary>Нажата кнопка мыши: 0 — нет, 1 — вверх, 2 — вниз.</summary>
    public event Action<byte>? OnReel;

    private byte _reel;

    public ImperialFishingMinigameControl(SpriteSystem sprite)
    {
        _sprite = sprite;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(32 * Scale, 96 * Scale);
    }

    public void SetState(ImperialFishingMinigameState state)
    {
        if (_state == null)
        {
            _shownBait = state.BaitPosition;
            _shownFish = state.FishPosition;
            _shownCompletion = state.Completion;
        }

        _state = state;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_state == null)
            return;

        // Плавно догоняем состояние сервера (обновляется 20 раз в секунду).
        var t = Math.Clamp(args.DeltaSeconds * 20f, 0f, 1f);
        _shownBait += (_state.BaitPosition - _shownBait) * t;
        _shownFish += (_state.FishPosition - _shownFish) * t;
        _shownCompletion += (_state.Completion - _shownCompletion) * t;

        // Маскировка: рыба тает за 2 секунды и возвращается за 1.2.
        var targetAlpha = _state.Camo ? 7f / 255f : 1f;
        var speed = _state.Camo ? 0.5f : 1f / 1.2f;
        _fishAlpha = _fishAlpha < targetAlpha
            ? MathF.Min(targetAlpha, _fishAlpha + speed * args.DeltaSeconds)
            : MathF.Max(targetAlpha, _fishAlpha - speed * args.DeltaSeconds);
    }

    private Texture Tex(string state) => _sprite.Frame0(new SpriteSpecifier.Rsi(HudRsi, state));

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_state == null)
            return;

        var origin = (PixelSize - new Vector2i(32 * Scale, 96 * Scale)) / 2;
        var flip = _state.Flip;

        void DrawLayer(Texture tex, float yOffsetPx, Color? color = null, UIBox2? region = null)
        {
            // Сдвиг вверх по pixel_z: строки изображения уходят на yOffsetPx вверх.
            var rect = UIBox2.FromDimensions(origin + new Vector2(0, -yOffsetPx * Scale), new Vector2(32 * Scale, 96 * Scale));
            if (flip)
                rect = UIBox2.FromDimensions(new Vector2(rect.Left, origin.Y + (origin.Y + 96 * Scale - rect.Bottom)), rect.Size);

            if (region == null)
            {
                handle.DrawTextureRect(tex, rect, color);
                return;
            }

            // Обрезанная часть текстуры (для полосы прогресса).
            var r = region.Value;
            var dest = new UIBox2(rect.Left + r.Left * Scale, rect.Top + r.Top * Scale, rect.Left + r.Right * Scale, rect.Top + r.Bottom * Scale);
            if (flip)
                dest = new UIBox2(dest.Left, origin.Y + (origin.Y + 96 * Scale - dest.Bottom), dest.Right, origin.Y + (origin.Y + 96 * Scale - dest.Top));
            handle.DrawTextureRectRegion(tex, dest, r, color);
        }

        DrawLayer(Tex(_state.Background), 0);

        // Рамка удочки — наложение самого HUD; приманка, рыба и прогресс (vis_contents) рисуются поверх неё.
        DrawLayer(Tex(_state.Frame), 0);

        // Приманка: низ, полоски и верх (hud_bait/update_overlays).
        var baitOffset = ImperialFishing.SliderHeight * (_shownBait / ImperialFishing.MinigameArea);
        var height = Math.Max(_state.BaitPixelHeight, ImperialFishing.BaitTopAndBottomHeight + 1);
        DrawLayer(Tex("bait_bottom"), baitOffset);
        for (var i = 1; i <= height - ImperialFishing.BaitTopAndBottomHeight; i++)
            DrawLayer(Tex("bait_bar"), baitOffset + i);
        DrawLayer(Tex("bait_top"), baitOffset + height - ImperialFishing.BaitTopAndBottomHeight);

        var fishOffset = ImperialFishing.SliderHeight * (_shownFish / ImperialFishing.MinigameArea);
        DrawLayer(Tex(_state.Icon.ToString().ToLowerInvariant() is var icon && icon == "fish" ? "fish" : icon), fishOffset, Color.White.WithAlpha(_fishAlpha));

        // Полоса прогресса заполняется снизу (alpha_mask_filter со сдвигом).
        var cut = (int) MathF.Round(ImperialFishing.CompletionBarHeight * (1 - _shownCompletion / 100f));
        var top = CompletionTop + cut;
        if (top <= CompletionBottom)
            DrawLayer(Tex("completion_overlay"), 0, region: new UIBox2(0, top, 32, CompletionBottom + 1));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            SetReel(1);
        else if (args.Function == EngineKeyFunctions.UIRightClick)
            SetReel(_state?.Bidirectional == true ? (byte) 2 : (byte) 1);
        else
            return;

        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        SetReel(0);
        args.Handle();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        SetReel(0);
    }

    private void SetReel(byte state)
    {
        if (_reel == state)
            return;

        _reel = state;
        OnReel?.Invoke(state);
    }
}
