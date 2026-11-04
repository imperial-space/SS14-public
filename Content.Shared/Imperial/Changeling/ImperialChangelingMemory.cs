using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>Украденное воспоминание (stolen_memories: name → story).</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingMemory(string name, string story)
{
    public readonly string Name = name;
    public readonly string Story = story;
}
