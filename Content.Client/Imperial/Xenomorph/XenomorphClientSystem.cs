using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.StatusIcon.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Xenomorph;

/// <summary>
/// Клиент ксеноморфов: иконки заражения infected0–5 (видны только ксеноморфам) и спрятавшаяся личинка.
/// </summary>
public sealed class XenomorphClientSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEmbryoComponent, GetStatusIconsEvent>(OnEmbryoIcons);
        SubscribeLocalEvent<XenomorphComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    /// <summary>RefreshInfectionImage: стадия эмбриона над носителем.</summary>
    private void OnEmbryoIcons(Entity<XenoEmbryoComponent> ent, ref GetStatusIconsEvent args)
    {
        // image "infected[stage]": обновляется до 5-й стадии, на 6-й остаётся infected5.
        var stage = Math.Clamp(ent.Comp.Stage, 0, 5);
        if (_proto.TryIndex<Shared.StatusIcon.FactionIconPrototype>($"XenoInfected{stage}", out var icon))
            args.StatusIcons.Add(icon);
    }

    /// <summary>larva/hide: под столами и предметами.</summary>
    private void OnAppearanceChange(Entity<XenomorphComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !args.AppearanceData.TryGetValue(XenoVisuals.Hidden, out var hiddenObj) || hiddenObj is not bool hidden)
            return;

        _sprite.SetDrawDepth((ent.Owner, args.Sprite),
            hidden ? (int) Shared.DrawDepth.DrawDepth.FloorObjects : (int) Shared.DrawDepth.DrawDepth.Mobs);
    }
}

/// <summary>Радиальное меню ксеноморфа: выбор смолы и касты при эволюции.</summary>
[UsedImplicitly]
public sealed class XenoRadialBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public XenoRadialBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not XenoRadialState radial)
            return;

        var options = new List<RadialMenuOptionBase>();
        foreach (var option in radial.Options)
        {
            options.Add(new RadialMenuActionOption<string>(OnPicked, option.Id)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(option.Icon),
                ToolTip = option.Name,
            });
        }

        _menu.SetButtons(options);
    }

    private void OnPicked(string id)
    {
        SendMessage(new XenoRadialPickMessage(id));
        Close();
    }
}
