using System;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Ships.Helm;

[UsedImplicitly]
public sealed class HelmBoundUserInterface : BoundUserInterface
{
    private HelmMenu? _menu;

    public HelmBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<HelmMenu>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not HelmBoundUserInterfaceState helmState)
            return;

        _menu.UpdateState(helmState);
    }
}
