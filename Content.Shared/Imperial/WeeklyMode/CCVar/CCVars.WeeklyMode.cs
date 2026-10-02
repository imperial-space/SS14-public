using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<bool> WeeklyModeEnabled =
        CVarDef.Create("weekly_mode.enabled", true, CVar.ARCHIVE | CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<string> WeeklyModeDataRoot =
        CVarDef.Create("weekly_mode.data_root", "/weekly-mode", CVar.ARCHIVE | CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 1, max: 60)]
    public static readonly CVarDef<int> WeeklyModeDefaultAutosaveMinutes =
        CVarDef.Create("weekly_mode.default_autosave_minutes", 30, CVar.ARCHIVE | CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 1, max: 128)]
    public static readonly CVarDef<int> WeeklyModeDefaultRetainAutosaves =
        CVarDef.Create("weekly_mode.default_retain_autosaves", 8, CVar.ARCHIVE | CVar.SERVERONLY);
}
