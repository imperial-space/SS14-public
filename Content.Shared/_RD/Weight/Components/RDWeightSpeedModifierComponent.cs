/*
 * Project: raincidation
 * File: RDWeightSpeedModifierComponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Mathematics.Extensions;
using Content.Shared._RD.Weight.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RD.Weight.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RDWeightSpeedModifierSystem), Other = AccessPermissions.None)]
public sealed partial class RDWeightSpeedModifierComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public float Value = 1;

    [DataField, ViewVariables, AutoNetworkedField]
    public RDWeightSpeedModifierCurve Curve = new RDWeightSpeedModifierLinearCurve();
}

[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class RDWeightSpeedModifierCurve
{
    public abstract float Calculate(float total);
}

[Serializable, NetSerializable]
public sealed partial class RDWeightSpeedModifierLinearCurve : RDWeightSpeedModifierCurve
{
    [DataField]
    public float Min;

    [DataField]
    public float Max;

    public override float Calculate(float total)
    {
        if ((total - Min).AboutEquals(0))
            return 1;

        return Math.Clamp(1 - (total - Min) / (Max - Min), 0f, 1f);
    }
}
