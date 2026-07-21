using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

public sealed class AchievementTabButton : ContainerButton
{
    private const int TabSize = 44;

    private static readonly Color BorderNormal = Color.FromHex("#3d2e1e");
    private static readonly Color BorderHover = Color.FromHex("#8b6914");
    private static readonly Color BorderSelected = Color.FromHex("#d4af37");
    private static readonly Color BgNormal = Color.FromHex("#1a130dee");
    private static readonly Color BgSelected = Color.FromHex("#2a1f18ee");
    private static readonly Color IconDimmed = Color.FromHex("#a89f91");

    private readonly TextureRect _icon;

    private readonly StyleBoxFlat _normalBox;
    private readonly StyleBoxFlat _hoverBox;
    private readonly StyleBoxFlat _selectedBox;

    public AchievementTabButton()
    {
        ToggleMode = true;
        SetSize = new Vector2(TabSize, TabSize);
        MinSize = new Vector2(TabSize, TabSize);

        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            CanShrink = true,
            HorizontalExpand = true,
            VerticalExpand = true,
            ModulateSelfOverride = IconDimmed,
        };
        AddChild(_icon);

        const float border = 2f;
        const float margin = 5f;

        _normalBox = new StyleBoxFlat
        {
            BackgroundColor = BgNormal,
            BorderColor = BorderNormal,
            BorderThickness = new Thickness(border),
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        _hoverBox = new StyleBoxFlat
        {
            BackgroundColor = BgNormal,
            BorderColor = BorderHover,
            BorderThickness = new Thickness(border),
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        _selectedBox = new StyleBoxFlat
        {
            BackgroundColor = BgSelected,
            BorderColor = BorderSelected,
            BorderThickness = new Thickness(border),
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        StyleBoxOverride = _normalBox;
    }

    public Texture? Icon
    {
        get => _icon.Texture;
        set => _icon.Texture = value;
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (_icon == null)
            return;

        if (Pressed)
        {
            StyleBoxOverride = _selectedBox;
            _icon.ModulateSelfOverride = null;
            return;
        }

        StyleBoxOverride = DrawMode switch
        {
            DrawModeEnum.Hover => _hoverBox,
            DrawModeEnum.Pressed => _hoverBox,
            _ => _normalBox
        };

        _icon.ModulateSelfOverride = IconDimmed;
    }
}
