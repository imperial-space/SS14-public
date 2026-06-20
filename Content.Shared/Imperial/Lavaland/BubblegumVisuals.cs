using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland;

[Serializable, NetSerializable]
public enum BubblegumVisuals : byte
{
    Raging,
}

[RegisterComponent]
public sealed partial class BubblegumAppearanceComponent : Component
{
}
