using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Музыка ритуала Нар'Си: один раз за раунд при первом черчении руны Нар'Си.</summary>
[Serializable, NetSerializable]
public sealed class CultRitualMusicEvent : EntityEventArgs;
