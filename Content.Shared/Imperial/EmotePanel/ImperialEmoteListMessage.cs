using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Сервер → клиент: доступные эмоции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteListMessage(List<ImperialEmoteEntry> emotes) : EntityEventArgs
{
    public readonly List<ImperialEmoteEntry> Emotes = emotes;
}
