using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland;

[Serializable, NetSerializable]
public enum AshDrakeVisuals : byte
{
    Unfurled,
    Flying,
}

[Serializable, NetSerializable]
public enum AshDrakeVisualLayers : byte
{
    Base,
}

[RegisterComponent]
public sealed partial class AshDrakeAppearanceComponent : Component
{
}
