using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Отметка на радаре: борт над позицией (смещение от позиции в тайлах, этаж, размер).</summary>
[Serializable, NetSerializable]
public readonly record struct CadiaAirContact(NetEntity Grid, Vector2 Offset, int Level, float Size, CadiaAirContactKind Kind, string Name, int SilhouetteWidth, int SilhouetteHeight, byte[] Silhouette);
