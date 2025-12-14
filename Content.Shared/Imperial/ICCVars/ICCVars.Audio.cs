using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.ICCVar;

public sealed partial class ICCVars
{
    /// <summary>
    ///     Location-based ambient sound volume.
    /// </summary>
    public static readonly CVarDef<float> LocationAmbientVolume =
        CVarDef.Create("audio.location_ambient_volume", 1.0f, CVar.ARCHIVE | CVar.CLIENTONLY);
}
