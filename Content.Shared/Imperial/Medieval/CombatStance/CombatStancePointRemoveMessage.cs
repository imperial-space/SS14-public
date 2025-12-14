using Content.Shared.Imperial.Medieval.Factions;
using Lidgren.Network;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CombatStance
{
    public sealed class CombatStancePointRemoveMessage : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;
        public FactionMemberGroup Group { get; set; }

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            Group = (FactionMemberGroup)buffer.ReadByte();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write((byte)Group);
        }
    }
}
