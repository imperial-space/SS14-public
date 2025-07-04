/*
 * Project: raincidation
 * File: RDWeightAlertsComponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RD.Weight.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class RDWeightAlertsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype>? Alert;

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<AlertPrototype>, Range> Alerts = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class Range
{
    [DataField]
    public float Min;

    [DataField]
    public float Max;
}
