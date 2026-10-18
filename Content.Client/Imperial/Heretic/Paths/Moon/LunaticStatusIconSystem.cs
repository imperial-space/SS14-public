using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Heretic.Paths.Moon;

/// <summary>add_team_hud(): лунатики и их Постановщик видят друг друга.</summary>
public sealed class LunaticStatusIconSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LunaticComponent, GetStatusIconsEvent>(OnLunaticIcon);
        SubscribeLocalEvent<LunaticMasterComponent, GetStatusIconsEvent>(OnMasterIcon);
    }

    private void OnLunaticIcon(Entity<LunaticComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_proto.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnMasterIcon(Entity<LunaticMasterComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_proto.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
