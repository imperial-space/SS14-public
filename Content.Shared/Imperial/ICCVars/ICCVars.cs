using Robust.Shared.Configuration;
using Robust.Shared;

namespace Content.Shared.Imperial.ICCVar;

[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class ICCVars : CVars
{
    #region Sponsors
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);
    #endregion
}
