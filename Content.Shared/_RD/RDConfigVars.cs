/*
 * Project: raincidation
 * File: RDConfigVars.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Robust.Shared.Configuration;

namespace Content.Shared._RD;

[CVarDefs]
public sealed class RDConfigVars
{
    /*
     * Weight
     */

    public static readonly CVarDef<int> WeightMaxUpdates =
        CVarDef.Create("rd_weight.max_updates", 10_000, CVar.REPLICATED);
}
