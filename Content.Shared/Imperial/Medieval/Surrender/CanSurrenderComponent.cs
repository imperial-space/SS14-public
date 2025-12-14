using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Surrender
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class CanSurrenderComponent : Component
    {
        public bool SurrenderActive = false;
        public TimeSpan SurrenderTime = TimeSpan.FromSeconds(30);
        public TimeSpan Unsurrender = TimeSpan.Zero;
        [DataField]
        public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/Medieval/surrender.ogg")
        {
            Params = new AudioParams
            {
                MaxDistance = 9
            }
        };
    }
    public sealed partial class MedievalSurrenderEvent : InstantActionEvent { }
    [Serializable, NetSerializable]
    public enum SurrenderVisuals : byte
    {
        Key
    }
}
