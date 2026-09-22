using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Heretic;

/// <summary>
/// Показывает HUD иконки для еретика:
/// — hudheretic над другими еретиками (видно только другим еретикам/админам, через showTo)
/// — hudtarget над именными жертвами (видна только тому еретику, который их назначил)
/// </summary>
public sealed class HereticStatusIconSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<FactionIconPrototype> TargetIconId = "HereticNamedTarget";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, GetStatusIconsEvent>(GetHereticIcon);
        SubscribeLocalEvent<HereticNamedTargetComponent, GetStatusIconsEvent>(GetNamedTargetIcon);
    }

    private void GetHereticIcon(Entity<HereticComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.StatusIcon, out var icon))
            return;
        args.StatusIcons.Add(icon);
    }

    private void GetNamedTargetIcon(Entity<HereticNamedTargetComponent> ent, ref GetStatusIconsEvent args)
    {
        var localPlayer = _player.LocalEntity;
        if (!localPlayer.HasValue)
            return;
        if (!ent.Comp.OwningHeretics.Contains(localPlayer.Value))
            return;
        if (!_proto.TryIndex(TargetIconId, out var icon))
            return;
        args.StatusIcons.Add(icon);
    }
}
