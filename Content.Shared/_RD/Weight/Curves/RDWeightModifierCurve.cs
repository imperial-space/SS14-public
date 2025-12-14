/*
 * Project: raincidation
 * File: RDWeightModifierCurve.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Robust.Shared.Serialization;

namespace Content.Shared._RD.Weight.Curves;

[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class RDWeightModifierCurve
{
    public abstract float Calculate(float total);
}
