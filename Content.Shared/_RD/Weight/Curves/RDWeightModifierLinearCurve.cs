/*
 * Project: raincidation
 * File: RDWeightModifierLinearCurve.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Mathematics.Extensions;
using Robust.Shared.Serialization;

namespace Content.Shared._RD.Weight.Curves;

[Serializable, NetSerializable]
public sealed partial class RDWeightModifierLinearCurve : RDWeightModifierCurve
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
