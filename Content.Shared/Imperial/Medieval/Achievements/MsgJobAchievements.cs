using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed class MsgJobAchievements : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public HashSet<string> Achievements = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        Achievements.EnsureCapacity(count);

        for (var i = 0; i < count; i++)
        {
            Achievements.Add(buffer.ReadString());
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Achievements.Count);

        foreach (var jobId in Achievements)
        {
            buffer.Write(jobId);
        }
    }
}
