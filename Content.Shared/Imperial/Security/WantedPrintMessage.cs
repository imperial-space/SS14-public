using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class WantedPrintMessage(string name, WantedPrintType type, string alias, string header, string description)
    : BoundUserInterfaceMessage
{
    public readonly string Name = name;
    public readonly WantedPrintType Type = type;
    public readonly string Alias = alias;
    public readonly string Header = header;
    public readonly string Description = description;
}
