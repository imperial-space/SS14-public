using Content.Shared.Imperial.Medieval.Additions;

namespace Content.Server.Imperial.Medieval.Additions;

public sealed class MedievalTimedDespawnSystem : MedievalSharedTimedDespawnSystem
{
    protected override bool CanDelete(EntityUid uid)
    {
        return true;
    }
}
