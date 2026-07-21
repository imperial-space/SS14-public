using Content.Server.Locale.Components;
using Content.Shared.Paper;

namespace Content.Server.Locale.Systems;

public sealed class AdvancedLocaleSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvancedLocaleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, AdvancedLocaleComponent comp, MapInitEvent args)
    {
        if (comp.Name != "")
            _metaData.SetEntityName(uid, Loc.GetString(comp.Name));
        if (comp.Desc != "")
            _metaData.SetEntityDescription(uid, Loc.GetString(comp.Desc));
        if (comp.Content != "")
            _paper.SetContent(uid, Loc.GetString(comp.Content));
        if (comp.WayStoneName != "" && TryComp<WaystoneComponent>(uid, out var waystone))
            waystone.Name = comp.WayStoneName;
    }
}
