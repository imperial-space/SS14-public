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
            hidden ? (int)Shared.DrawDepth.DrawDepth.FloorObjects : (int)Shared.DrawDepth.DrawDepth.Mobs);
    }
}
