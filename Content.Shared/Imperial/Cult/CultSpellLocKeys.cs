using System.Collections.Generic;

namespace Content.Shared.Imperial.Cult;

public static class CultSpellLocKeys
{
    public static readonly IReadOnlyDictionary<string, string> Mapping = new Dictionary<string, string>
    {
        ["ActionCultStun"] = "cult-spell-stun",
        ["ActionCultShackles"] = "cult-spell-shackles",
        ["ActionCultTeleport"] = "cult-spell-teleport",
        ["ActionCultEmp"] = "cult-spell-emp",
        ["ActionCultTwistedConstruction"] = "cult-spell-twisted-construction",
        ["ActionCultSummonDagger"] = "cult-spell-summon-dagger",
        ["ActionCultSummonEquipment"] = "cult-spell-summon-equipment",
        ["ActionCultConcealPresence"] = "cult-spell-conceal-presence",
        ["ActionCultBloodRites"] = "cult-spell-blood-rites",
    };
}
