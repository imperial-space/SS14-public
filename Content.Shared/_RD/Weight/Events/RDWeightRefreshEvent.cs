/*
 * Project: raincidation
 * File: RDWeightRefreshEvent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Components;

namespace Content.Shared._RD.Weight.Events;

[ByRefEvent]
public record RDWeightRefreshEvent(Entity<RDWeightComponent> Entity, float Total);
