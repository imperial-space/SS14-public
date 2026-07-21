using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> ParryDesyncTolerance =
        CVarDef.Create("parry.desync_tolerance", 0.1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<int> ParryReadySoundType =
        CVarDef.Create("parry.ready_sound_type", 0, CVar.CLIENT | CVar.ARCHIVE);
}
