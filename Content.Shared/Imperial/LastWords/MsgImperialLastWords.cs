using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.LastWords;

/// <summary>Последние слова одного игрока.</summary>
public sealed class ImperialLastWordsEntry
{
    /// <summary>Тело игрока — для спрайта (может не дойти до клиента).</summary>
    public NetEntity? Entity;

    public string Name = string.Empty;

    /// <summary>Должность — заголовок карточки.</summary>
    public string Job = string.Empty;

    public string Phrase = string.Empty;

    /// <summary>Умер к концу раунда (иначе — был при смерти, но выжил).</summary>
    public bool Dead;
}

/// <summary>
/// Сервер → все клиенты, в конце раунда: последние слова игроков перед смертью.
/// Сериализуется вручную; клиент не может запросить его сам — только получить.
/// </summary>
public sealed class MsgImperialLastWords : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public int RoundId;

    public List<ImperialLastWordsEntry> Entries = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        RoundId = buffer.ReadInt32();
        var count = buffer.ReadVariableInt32();
        Entries = new List<ImperialLastWordsEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = new ImperialLastWordsEntry();
            if (buffer.ReadBoolean())
                entry.Entity = new NetEntity(buffer.ReadInt32());
            entry.Dead = buffer.ReadBoolean();
            buffer.ReadPadBits();
            entry.Name = buffer.ReadString();
            entry.Job = buffer.ReadString();
            entry.Phrase = buffer.ReadString();
            Entries.Add(entry);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(RoundId);
        buffer.WriteVariableInt32(Entries.Count);
        foreach (var entry in Entries)
        {
            buffer.Write(entry.Entity != null);
            if (entry.Entity is { } entity)
                buffer.Write(entity.Id);
            buffer.Write(entry.Dead);
            buffer.WritePadBits();
            buffer.Write(entry.Name);
            buffer.Write(entry.Job);
            buffer.Write(entry.Phrase);
        }
    }
}
