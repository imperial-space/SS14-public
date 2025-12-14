/*
 * Project: raincidation
 * File: RDWeightThrowerModifierComponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RD.Weight.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RDWeightThrowModifierSystem))]
public sealed partial class RDWeightThrowerModifierComponent : Component;
