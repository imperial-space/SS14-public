using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.ICCVar;

public sealed partial class ICCVars
{
    public static readonly CVarDef<bool> QueueEnabled =
        CVarDef.Create("medieval.queue_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
            WaveShaderEnabled = CVarDef.Create("medieval.wave_shader_enabled", false, CVar.CLIENT | CVar.ARCHIVE); // RETURN TO TRUE AFTER FIX OPTIONS

    public static readonly CVarDef<bool> EnableLanguageFonts =
        CVarDef.Create("lang.enable_fonts", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<TimeSpan> StoreBalanceUpdateInterval =
        CVarDef.Create("store.balance_update_interval", TimeSpan.FromSeconds(60), CVar.SERVERONLY);
    // Imperial Medieval Flavor Images Begin

    public static readonly CVarDef<int> SetWidthFlavorImages = CVarDef.Create("medieval.flavor_images_width", 256, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<int> SetHeightFlavorImages = CVarDef.Create("medieval.flavor_images_height", 256, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<int> FlavorPlaytimeRequirement = CVarDef.Create("medieval.flavor_playtime_requirement", 4 * 60 * 60);
    // Imperial Medieval Flavor Images End
}
