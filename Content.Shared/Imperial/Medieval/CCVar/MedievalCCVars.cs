using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.CCVar;

[CVarDefs]
public sealed partial class MedievalCCVars : CVars
{
    public static readonly CVarDef<int> CreationsMaxPaintings =
        CVarDef.Create("creations.max_paintings", 4, CVar.SERVER);

    public static readonly CVarDef<int> CreationsMaxBooks =
        CVarDef.Create("creations.max_books", 4, CVar.SERVER);

    // AfkTime и AfkKickTime измеряется в секундах
    public static readonly CVarDef<float> AfkTime =
        CVarDef.Create("medieval.afk_time", 15 * 60f, CVar.SERVER | CVar.REPLICATED);

    // суммируется с AfkTime
    public static readonly CVarDef<float> AfkKickTime =
        CVarDef.Create("medieval.afk_kick_time", 60f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> BloodMoonPeriod =
        CVarDef.Create("medieval.blood_moon_period", 7, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> BloodMoonWerewolves =
        CVarDef.Create("medieval.blood_moon_werewolves", 5, CVar.SERVER | CVar.REPLICATED);

    // Работа возрождений
    public static readonly CVarDef<bool> GhostRevive =
    CVarDef.Create("medieval.ghost_revive", true, CVar.SERVER | CVar.REPLICATED);
    // Количество убийств духов для возрождения
    public static readonly CVarDef<int> GhostKillsToRevive =
    CVarDef.Create("medieval.ghost_kills_to_revive", 12, CVar.SERVER | CVar.REPLICATED);
}
