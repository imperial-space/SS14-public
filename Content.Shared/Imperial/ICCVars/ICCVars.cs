using Robust.Shared.Configuration;
using Robust.Shared;

namespace Content.Shared.Imperial.ICCVar;

[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class ICCVars : CVars
{
    public static readonly CVarDef<string> LobbyName =
            CVarDef.Create("server.lobby_name", "MyServer", CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<bool>
        VoteAutoStartInLobby = CVarDef.Create("vote.autostartinlobby", true, CVar.SERVERONLY);
    public static readonly CVarDef<int>
        GameEndRoundDuration = CVarDef.Create("game.endroundduration", 40, CVar.SERVERONLY);
    #region Sponsors
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);
    #endregion
}
