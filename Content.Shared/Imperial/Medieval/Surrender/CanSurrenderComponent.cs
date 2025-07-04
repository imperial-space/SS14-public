using Content.Shared.Actions;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Surrender
{
    [RegisterComponent]
    public sealed partial class CanSurrenderComponent : Component
    {
        public bool SurrenderActive = false;
        public TimeSpan SurrenderTime = TimeSpan.FromSeconds(30);
        public TimeSpan Unsurrender = TimeSpan.Zero;
    }
    public sealed partial class MedievalSurrenderEvent : InstantActionEvent { }
    [Serializable, NetSerializable]
    public enum SurrenderVisuals : byte
    {
        Key
    }
}
