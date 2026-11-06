using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: список для tgui_input_list (камеры или цели слежения).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiListEvent(ImperialAiListKind kind, List<ImperialAiListEntry> entries) : EntityEventArgs
{
    public ImperialAiListKind Kind = kind;
    public List<ImperialAiListEntry> Entries = entries;
}
