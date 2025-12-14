/*
 * Project: raincidation
 * File: RDWeightSpeedModifierComponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Curves;
using Content.Shared._RD.Weight.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RD.Weight.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RDWeightSpeedModifierSystem), Other = AccessPermissions.None)]
public sealed partial class RDWeightSpeedModifierComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public float Value = 1;

    [DataField, ViewVariables, AutoNetworkedField]
    public RDWeightModifierCurve Curve = new RDWeightModifierLinearCurve();
}
