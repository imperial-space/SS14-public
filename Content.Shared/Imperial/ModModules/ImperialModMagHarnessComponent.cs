using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>magnetic_harness: выпавшее оружие примагничивается в слот хранилища костюма.</summary>
[RegisterComponent]
public sealed partial class ImperialModMagHarnessComponent : Component
{
    [ViewVariables]
    public List<(EntityUid Gun, TimeSpan At)> Pending = new();
}
