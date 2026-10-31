using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Значок роли войны над головой. Видят только бойцы той же стороны (и призраки) — по значкам свои узнают
/// командиров и медиков в толпе, а враг не может по ним выбирать цели.
/// </summary>
public sealed class CadiaFactionIconSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaFactionMemberComponent, GetStatusIconsEvent>(OnGetIcons);
    }

    private void OnGetIcons(Entity<CadiaFactionMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.Role is not { } roleId || !_proto.TryIndex(roleId, out var role))
            return;

        if (_player.LocalEntity is not { } local)
            return;
        if (!HasComp<GhostComponent>(local) &&
            (!TryComp<CadiaFactionMemberComponent>(local, out var viewer) || viewer.Role == null || viewer.Faction != ent.Comp.Faction))
            return;

        if (_proto.TryIndex(role.Icon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
