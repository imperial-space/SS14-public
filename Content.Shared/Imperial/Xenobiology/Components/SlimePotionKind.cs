using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

[Serializable, Robust.Shared.Serialization.NetSerializable]
public enum SlimePotionKind : byte
{
    /// <summary>slimepotion/slime/docility.</summary>
    Docility,
    /// <summary>slimepotion/sentience.</summary>
    Sentience,
    /// <summary>slimepotion/transference.</summary>
    Transference,
    /// <summary>slimepotion/slime/steroid.</summary>
    Steroid,
    /// <summary>slimepotion/enhancer.</summary>
    Enhancer,
    /// <summary>slimepotion/enhancer/max.</summary>
    MaxEnhancer,
    /// <summary>slimepotion/slime/stabilizer.</summary>
    Stabilizer,
    /// <summary>slimepotion/slime/mutator.</summary>
    Mutator,
    /// <summary>slimepotion/speed.</summary>
    Speed,
    /// <summary>slimepotion/fireproof.</summary>
    Fireproof,
    /// <summary>slimepotion/genderchange.</summary>
    GenderChange,
    /// <summary>slimepotion/renaming.</summary>
    Renaming,
    /// <summary>slimepotion/slimeradio.</summary>
    Radio,
}
