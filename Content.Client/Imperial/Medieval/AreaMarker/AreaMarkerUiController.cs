using Content.Shared.Imperial.Medieval.AreaMarker;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Imperial.Medieval.AreaMarker;

public sealed class AreaMarkerUiController : UIController
{
    public event Action<string>? AreaEntered;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AreaMarkerAnnounceEvent>(OnAreaMarkerAnnounce);
    }

    private void OnAreaMarkerAnnounce(ref AreaMarkerAnnounceEvent ev)
    {
        AreaEntered?.Invoke(ev.Message);
    }
}
