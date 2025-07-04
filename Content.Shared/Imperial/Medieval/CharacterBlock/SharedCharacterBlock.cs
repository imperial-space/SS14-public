using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CharacterBlock;

public class UpdateBlockerCharactersMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;

    public List<string> BlockedCharactersHashes { get; set; } = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        BlockedCharactersHashes.EnsureCapacity(count);

        for (var i = 0; i < count; i++)
        {
            BlockedCharactersHashes.Add(buffer.ReadString());
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(BlockedCharactersHashes.Count);

        foreach (var blockerCharacter in BlockedCharactersHashes)
        {
            buffer.Write(blockerCharacter);
        }
    }
}
