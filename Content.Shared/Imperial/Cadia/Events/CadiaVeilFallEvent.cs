using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Завеса пала: клиент вспыхивает небом красного варпа.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public sealed class CadiaVeilFallEvent : EntityEventArgs;
