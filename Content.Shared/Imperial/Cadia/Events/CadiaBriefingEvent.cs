using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Открыть памятку роли у бойца.</summary>
[Serializable, NetSerializable]
public sealed class CadiaBriefingEvent(string role, string name) : EntityEventArgs
{
    public readonly string Role = role;
    public readonly string Name = name;
}
