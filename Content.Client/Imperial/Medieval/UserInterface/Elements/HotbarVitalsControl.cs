using System.Numerics;
using Content.Client.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.UserInterface.Elements;

public sealed class HotbarVitalsControl : BoxContainer
{
    private const float BarSmoothingSpeed = 14f;
    private const float BarTextureScale = 2f;
    private const float DefaultFramePatchMargin = 4f;
    private const float DefaultFillPatchMargin = 3f;
    private const float DefaultFillHorizontalInset = 2f;
    private const float DefaultFillTopInset = 4f;
    private const float DefaultFillBottomInset = 3f;
    private const float VitalBarHeight = 20f;
    private const string BarFrameTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_frame.png";
    private const string BarFillTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_fill.png";

    private static readonly Color BarBackground = Color.FromHex("#1A1414");
    private static readonly Color HealthColor = Color.FromHex("#E5483F");
    private static readonly Color CriticalHealthColor = Color.FromHex("#B81232");
    private static readonly Color StaminaColor = Color.FromHex("#39D64B");
    private static readonly Color ManaColor = Color.FromHex("#2F84FF");
    private static readonly Color HungerColor = Color.FromHex("#E67E22");
    private static readonly TimeSpan ManaHideDelay = TimeSpan.FromSeconds(3);

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly BoxContainer _content;
    private readonly FramedStatBar _healthBar;
    private readonly FramedStatBar _manaBar;
    private readonly FramedStatBar _staminaBar;
    private readonly FramedStatBar _hungerBar;

    private MobStateSystem? _mobStateSystem;
    private MobThresholdSystem? _mobThresholdSystem;
    private StaminaSystem? _staminaSystem;

    private EntityUid? _trackedEntity;
    private TimeSpan? _manaHideAt;
    private bool _manaWasFull;
    private float? _displayedHealthRatio;
    private float? _displayedManaRatio;
    private float? _displayedStaminaRatio;
    private float? _displayedHungerRatio;
    private float? _appliedWidth;
    private Control? _widthReference;

    public Control? WidthReference
    {
        get => _widthReference;
        set
        {
            if (_widthReference == value)
                return;

            if (_widthReference != null)
                _widthReference.OnResized -= UpdateWidth;

            _widthReference = value;
            _appliedWidth = null;

            if (_widthReference != null)
                _widthReference.OnResized += UpdateWidth;

            UpdateWidth();
        }
    }

    public HotbarVitalsControl()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Ignore;

        _content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _manaBar = new FramedStatBar(_resourceCache)
        {
            SetHeight = VitalBarHeight,
            FillColor = ManaColor,
        };

        _hungerBar = new FramedStatBar(_resourceCache)
        {
            SetHeight = VitalBarHeight,
            FillColor = HungerColor,
            Visible = false
        };

        var lowerRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 0,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _healthBar = new FramedStatBar(_resourceCache)
        {
            SetHeight = VitalBarHeight,
            FillColor = HealthColor,
        };

        _staminaBar = new FramedStatBar(_resourceCache)
        {
            SetHeight = VitalBarHeight,
            FillColor = StaminaColor,
        };

        lowerRow.AddChild(_healthBar);
        lowerRow.AddChild(_staminaBar);

        _content.AddChild(_hungerBar);
        _content.AddChild(_manaBar);
        _content.AddChild(lowerRow);
        AddChild(_content);

        ResetTrackedEntity(null);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateWidth();
        UpdateVitals(args.DeltaSeconds);
    }

    private void UpdateVitals(float frameTime)
    {
        if ((_mobStateSystem == null && !_entityManager.TrySystem(out _mobStateSystem)) ||
            (_mobThresholdSystem == null && !_entityManager.TrySystem(out _mobThresholdSystem)) ||
            (_staminaSystem == null && !_entityManager.TrySystem(out _staminaSystem)))
        {
            _content.Visible = false;
            return;
        }

        if (_player.LocalEntity is not { } entity || !_entityManager.EntityExists(entity))
        {
            _content.Visible = false;
            ResetTrackedEntity(null);
            return;
        }

        if (_trackedEntity != entity)
            ResetTrackedEntity(entity);

        var hasHealth = UpdateHealth(entity, frameTime);
        var hasStamina = UpdateStamina(entity, frameTime);

        var isForged = _entityManager.HasComponent<ForgedComponent>(entity);

        // Мана скрыта для кованных, Голод скрыт для всех остальных
        var hasMana = !isForged && UpdateMana(entity, frameTime);
        var hasHunger = isForged && UpdateHunger(entity, frameTime);

        _hungerBar.Visible = hasHunger;
        if (isForged) _manaBar.Visible = false;

        _content.Visible = hasHealth || hasStamina || hasMana || hasHunger;
    }

    private bool UpdateHealth(EntityUid entity, float frameTime)
    {
        if (!_entityManager.TryGetComponent(entity, out DamageableComponent? damageable) ||
            !_entityManager.TryGetComponent(entity, out MobStateComponent? mobState) ||
            !_entityManager.TryGetComponent(entity, out MobThresholdsComponent? thresholds))
        {
            _healthBar.Visible = false;
            _displayedHealthRatio = null;
            return false;
        }

        var ratio = 0f;
        var isCritical = false;

        if (_mobStateSystem!.IsAlive(entity, mobState))
        {
            if (!_mobThresholdSystem!.TryGetThresholdForState(entity, MobState.Critical, out var critThreshold, thresholds) &&
                !_mobThresholdSystem.TryGetThresholdForState(entity, MobState.Dead, out critThreshold, thresholds) || critThreshold.Value <= 0)
            {
                ratio = 1f;
            }
            else
            {
                ratio = Math.Clamp(1f - damageable.TotalDamage.Float() / critThreshold.Value.Float(), 0f, 1f);
            }
        }
        else if (_mobStateSystem.IsCritical(entity, mobState))
        {
            isCritical = true;

            if (_mobThresholdSystem!.TryGetThresholdForState(entity, MobState.Critical, out var critThreshold, thresholds) &&
                _mobThresholdSystem.TryGetThresholdForState(entity, MobState.Dead, out var deadThreshold, thresholds))
            {
                var thresholdRange = (deadThreshold.Value - critThreshold.Value).Float();
                if (thresholdRange > 0f)
                    ratio = Math.Clamp(1f - (damageable.TotalDamage - critThreshold.Value).Float() / thresholdRange, 0f, 1f);
            }
        }

        if (_displayedHealthRatio == null)
        {
            _displayedHealthRatio = ratio;
        }
        else
        {
            var smoothing = 1f - MathF.Exp(-BarSmoothingSpeed * frameTime);
            _displayedHealthRatio = MathHelper.Lerp(_displayedHealthRatio.Value, ratio, smoothing);

            if (Math.Abs(_displayedHealthRatio.Value - ratio) < 0.002f)
                _displayedHealthRatio = ratio;
        }

        _healthBar.FillColor = isCritical ? CriticalHealthColor : HealthColor;
        _healthBar.Visible = true;
        _healthBar.Value = _displayedHealthRatio.Value;
        return true;
    }

    private bool UpdateStamina(EntityUid entity, float frameTime)
    {
        if (!_entityManager.TryGetComponent(entity, out StaminaComponent? stamina) || stamina.CritThreshold <= 0f)
        {
            _staminaBar.Visible = false;
            _displayedStaminaRatio = null;
            return false;
        }

        var ratio = Math.Clamp(1f - _staminaSystem!.GetStaminaDamage(entity, stamina) / stamina.CritThreshold, 0f, 1f);

        if (_displayedStaminaRatio == null)
        {
            _displayedStaminaRatio = ratio;
        }
        else
        {
            var smoothing = 1f - MathF.Exp(-BarSmoothingSpeed * frameTime);
            _displayedStaminaRatio = MathHelper.Lerp(_displayedStaminaRatio.Value, ratio, smoothing);

            if (Math.Abs(_displayedStaminaRatio.Value - ratio) < 0.002f)
                _displayedStaminaRatio = ratio;
        }

        _staminaBar.Visible = true;
        _staminaBar.Value = _displayedStaminaRatio.Value;
        return true;
    }

    private bool UpdateMana(EntityUid entity, float frameTime)
    {
        if (!_entityManager.TryGetComponent(entity, out ManaComponent? mana) || mana.MaxMana <= 0f)
        {
            _manaHideAt = null;
            _manaWasFull = false;
            _manaBar.Visible = false;
            _displayedManaRatio = null;
            return false;
        }

        var ratio = Math.Clamp(mana.Mana / mana.MaxMana, 0f, 1f);

        if (_displayedManaRatio == null)
        {
            _displayedManaRatio = ratio;
        }
        else
        {
            var smoothing = 1f - MathF.Exp(-BarSmoothingSpeed * frameTime);
            _displayedManaRatio = MathHelper.Lerp(_displayedManaRatio.Value, ratio, smoothing);

            if (Math.Abs(_displayedManaRatio.Value - ratio) < 0.002f)
                _displayedManaRatio = ratio;
        }

        var displayedRatio = _displayedManaRatio.Value;
        var isFull = displayedRatio >= 0.999f;

        if (!isFull)
        {
            _manaHideAt = null;
            _manaWasFull = false;
            _manaBar.Visible = true;
        }
        else
        {
            if (!_manaWasFull)
                _manaHideAt = _timing.CurTime + ManaHideDelay;

            _manaWasFull = true;
            _manaBar.Visible = _manaHideAt == null || _timing.CurTime < _manaHideAt.Value;
        }

        _manaBar.Value = displayedRatio;
        return _manaBar.Visible;
    }

    private bool UpdateHunger(EntityUid entity, float frameTime)
    {
        if (!_entityManager.TryGetComponent(entity, out HungerComponent? hunger))
        {
            _displayedHungerRatio = null;
            return false;
        }

        float maxHunger = 200f;
        float currentHunger = hunger.LastAuthoritativeHungerValue;
        var ratio = Math.Clamp(currentHunger / maxHunger, 0f, 1f);

        if (_displayedHungerRatio == null)
            _displayedHungerRatio = ratio;
        else
        {
            var smoothing = 1f - MathF.Exp(-BarSmoothingSpeed * frameTime);
            _displayedHungerRatio = MathHelper.Lerp(_displayedHungerRatio.Value, ratio, smoothing);
        }

        _hungerBar.Value = _displayedHungerRatio.Value;
        return true;
    }

    private void UpdateWidth()
    {
        if (WidthReference == null)
            return;

        var referenceWidth = WidthReference.Size.X;
        if (referenceWidth <= 0f)
            referenceWidth = WidthReference.DesiredSize.X;

        var width = MathF.Round(referenceWidth * UIScale) / UIScale;
        if (width <= 0f)
            return;

        if (_appliedWidth != null && Math.Abs(_appliedWidth.Value - width) < 0.5f)
            return;

        _appliedWidth = width;
        _content.SetWidth = width;
        InvalidateMeasure();
        Parent?.InvalidateMeasure();
    }

    private void ResetTrackedEntity(EntityUid? entity)
    {
        _trackedEntity = entity;
        _manaHideAt = entity == null ? null : _timing.CurTime + ManaHideDelay;
        _manaWasFull = false;
        _displayedHealthRatio = null;
        _displayedManaRatio = null;
        _displayedStaminaRatio = null;
        _displayedHungerRatio = null;
        _manaBar.Visible = false;
    }

    private sealed class FramedStatBar : LayoutContainer
    {
        private readonly LayoutContainer _fillRegion;
        private readonly PanelContainer _fillSprite;
        private readonly StyleBoxTexture _fillStyleBox;
        private float _value;

        public Color FillColor
        {
            get => _fillStyleBox.Modulate;
            set => _fillStyleBox.Modulate = value;
        }

        public float Value
        {
            get => _value;
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_value - clamped) < 0.001f)
                    return;

                _value = clamped;
                UpdateFill();
            }
        }

        public FramedStatBar(IResourceCache resourceCache)
        {
            MouseFilter = MouseFilterMode.Ignore;
            MinSize = new Vector2(1f, 1f);
            HorizontalExpand = true;
            SetHeight = 14f;

            var fillTexture = resourceCache.GetResource<TextureResource>(BarFillTexturePath).Texture;
            var frameTexture = resourceCache.GetResource<TextureResource>(BarFrameTexturePath).Texture;

            var backgroundStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = BarBackground,
                Texture = fillTexture,
            };
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFillPatchMargin);

            _fillStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = Color.White,
                Texture = fillTexture,
            };
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFillPatchMargin);

            var frameStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Texture = frameTexture,
            };
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFramePatchMargin);

            _fillRegion = new LayoutContainer
            {
                MouseFilter = MouseFilterMode.Ignore,
                RectClipContent = true,
                InheritChildMeasure = false,
            };
            SetAnchorPreset(_fillRegion, LayoutPreset.Wide);
            SetMarginLeft(_fillRegion, DefaultFillHorizontalInset);
            SetMarginTop(_fillRegion, DefaultFillTopInset);
            SetMarginRight(_fillRegion, -DefaultFillHorizontalInset);
            SetMarginBottom(_fillRegion, -DefaultFillBottomInset);

            var background = new PanelContainer
            {
                PanelOverride = backgroundStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
            };
            SetAnchorPreset(background, LayoutPreset.Wide);

            _fillSprite = new PanelContainer
            {
                PanelOverride = _fillStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false,
            };
            SetAnchorPreset(_fillSprite, LayoutPreset.LeftWide);
            SetMarginLeft(_fillSprite, 0f);
            SetMarginTop(_fillSprite, 0f);
            SetMarginBottom(_fillSprite, 0f);

            var frame = new PanelContainer
            {
                PanelOverride = frameStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
            };
            SetAnchorPreset(frame, LayoutPreset.Wide);

            AddChild(_fillRegion);
            AddChild(frame);
            _fillRegion.AddChild(background);
            _fillRegion.AddChild(_fillSprite);

            OnResized += UpdateFill;
            _fillRegion.OnResized += UpdateFill;
        }

        private void UpdateFill()
        {
            var maxWidth = MathF.Round(_fillRegion.Size.X * UIScale) / UIScale;
            var width = Math.Clamp(MathF.Ceiling(_fillRegion.Size.X * _value * UIScale) / UIScale, 0f, maxWidth);
            _fillSprite.Visible = width > 0.5f;
            SetMarginRight(_fillSprite, width);
        }
    }
}
