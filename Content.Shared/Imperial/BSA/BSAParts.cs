using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

/// <summary>
/// Части блюспейс-артиллерии для MultipartMachine.
/// </summary>
[Serializable, NetSerializable]
public enum BSAParts : byte
{
    Front,
    Back,
}
