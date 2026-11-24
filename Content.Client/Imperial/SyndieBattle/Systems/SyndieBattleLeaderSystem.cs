using Content.Shared.Imperial.SyndieBattle.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattleLeaderSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleLeaderComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(EntityUid uid, SyndieBattleLeaderComponent component, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_prototypeManager.Index(component.StatusIcon));
    }
}
