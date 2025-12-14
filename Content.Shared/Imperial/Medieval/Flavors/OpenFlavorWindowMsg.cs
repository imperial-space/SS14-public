using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Flavors
{
    public sealed class OpenFlavorWindowMsg : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public string Description = string.Empty;
        public string Path = string.Empty;

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            Description = buffer.ReadString();
            Path = buffer.ReadString();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(Description);
            buffer.Write(Path);
        }
    }
}
