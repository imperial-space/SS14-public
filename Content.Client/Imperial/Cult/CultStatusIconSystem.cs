using Content.Shared.Imperial.Cult.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Отображает иконку Нар'Си над культистами (видно только другим культистам).
/// Иконка показывается через FactionIconPrototype.ShowTo = CultistComponent.
/// </summary>
public sealed class CultStatusIconSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultistComponent, GetStatusIconsEvent>(GetCultistIcon);
    }

    private void GetCultistIcon(Entity<CultistComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.StatusIcon, out var icon))
            return;
        args.StatusIcons.Add(icon);
    }
}

