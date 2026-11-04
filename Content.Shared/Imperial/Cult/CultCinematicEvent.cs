using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultCinematicEvent(CultCinematic cinematic) : EntityEventArgs
{
    public readonly CultCinematic Cinematic = cinematic;
}
