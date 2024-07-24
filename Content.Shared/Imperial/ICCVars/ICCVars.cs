using Robust.Shared.Configuration;
using Robust.Shared;

namespace Content.Shared.Imperial.ICCVar;

[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class ICCVars : CVars
{
    /// <summary>
    /// Enables autovote for map and preset in lobby
    /// </summary>
    public static readonly CVarDef<bool>
        VoteAutoStartInLobby = CVarDef.Create("vote.autostartinlobby", true, CVar.SERVERONLY);
        
    /// <summary>
    /// Timer for end round
    /// </summary>
    public static readonly CVarDef<int>
        GameEndRoundDuration = CVarDef.Create("game.endroundduration", 40, CVar.SERVERONLY);

    #region Sponsors
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);
    #endregion
}
