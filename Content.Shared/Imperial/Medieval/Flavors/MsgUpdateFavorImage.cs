using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Flavors
{
    public sealed class MsgUpdateFlavorImage : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public int Slot;
        public byte[] Image = Array.Empty<byte>();

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            Slot = buffer.ReadInt32();
            var length = buffer.ReadVariableInt32();
            Image = buffer.ReadBytes(length);
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(Slot);
            buffer.WriteVariableInt32(Image.Length);
            buffer.Write(Image);
        }
    }
}
