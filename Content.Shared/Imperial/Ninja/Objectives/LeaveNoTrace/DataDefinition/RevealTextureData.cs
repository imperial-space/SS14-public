using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Definition;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.LeaveNoTrace;


[DataDefinition, Serializable, NetSerializable]
public sealed partial class TextureGlitchParametersData
{
    /// <summary>
    /// Fill threshold at which texture starts to lag
    /// </summary>
    [DataField]
    public float GlitchThreshold = 0.9f;

    /// <summary>
    /// A sprite that will gradually transform as the ninja is revealed.
    /// </summary>
    [DataField]
    public ResPath RevealSpritePath = new("/Textures/Imperial/Interface/Misc/Ninja/eye.png");

    /// <summary>
    /// Eye glitch params
    /// </summary>
    [DataField]
    public GlitchShaderParametersData Glitch = new()
    {
        ShakePower = 0.02f
    };
}
