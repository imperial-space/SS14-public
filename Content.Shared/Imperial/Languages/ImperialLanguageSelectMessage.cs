using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

/// <summary>Клиент выбирает язык по умолчанию.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLanguageSelectMessage(ProtoId<ImperialLanguagePrototype> language) : EntityEventArgs
{
    public readonly ProtoId<ImperialLanguagePrototype> Language = language;
}
