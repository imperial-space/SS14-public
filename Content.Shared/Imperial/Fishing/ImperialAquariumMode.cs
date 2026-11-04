using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Режим аквариума (AQUARIUM_MODE_*).</summary>
[Serializable, NetSerializable]
public enum ImperialAquariumMode : byte
{
    /// <summary>Полный ручной контроль среды и температуры, без стазиса.</summary>
    Manual,

    /// <summary>Встроенный процессор подбирает среду под население, может включить стазис.</summary>
    Auto,

    /// <summary>Рыбы не умирают от среды и голода, но не растут и не размножаются.</summary>
    Safe,
}
