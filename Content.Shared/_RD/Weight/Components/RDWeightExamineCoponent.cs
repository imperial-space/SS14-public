/*
 * Project: raincidation
 * File: RDWeightExamineCoponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Robust.Shared.GameStates;

namespace Content.Shared._RD.Weight.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class RDWeightExamineComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId? Current;

    [DataField, AutoNetworkedField]
    public Dictionary<LocId, Range> Examines = new();
}
