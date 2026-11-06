using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Точка на поле для подсказки цели: клиент не видит далёкие сущности, поэтому координаты шлёт сервер.</summary>
[Serializable, NetSerializable]
public struct CadiaLandmark
{
    public CadiaLandmarkKind Kind;
    public int Number;
    public System.Numerics.Vector2 Position;
    public int Map;
}
