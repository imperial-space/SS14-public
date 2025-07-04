using Content.Shared.Imperial.Medieval.Additions;

namespace Content.Client.Imperial.Medieval.Additions;

public sealed class MedievalTimedDespawnSystem : MedievalSharedTimedDespawnSystem
{
    protected override bool CanDelete(EntityUid uid)
    {
        return IsClientSide(uid);
    }
}
