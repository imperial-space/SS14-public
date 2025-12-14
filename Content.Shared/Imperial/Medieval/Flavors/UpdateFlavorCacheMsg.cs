using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Flavors
{
    public sealed class UpdateFlavorCacheMsg : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;
        public Dictionary<string, byte[]> CacheImages = new();

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            var countCache = buffer.ReadInt32();
            CacheImages = new();
            for (var i = 0; i < countCache; i++)
            {
                var length = buffer.ReadVariableInt32();
                CacheImages.Add(buffer.ReadString(), buffer.ReadBytes(length));
            }
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(CacheImages.Count);
            foreach (var (key, value) in CacheImages)
            {
                buffer.WriteVariableInt32(value.Length);
                buffer.Write(key);
                buffer.Write(value);
            }
        }
    }
}
