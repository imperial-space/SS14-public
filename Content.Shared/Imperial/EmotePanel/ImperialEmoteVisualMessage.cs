using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Сервер → клиенты рядом: анимация или наложение эмоции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteVisualMessage(NetEntity entity, ImperialEmoteEffect effect) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
    public readonly ImperialEmoteEffect Effect = effect;
}
