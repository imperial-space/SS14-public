using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Нанесённое на голокарту. A/B — мировые координаты (начало и конец линии, центр и точка на окружности,
/// точка значка/подписи/чертежа). Kind — тип значка или чертежа.
/// </summary>
[Serializable, NetSerializable]
public sealed class CadiaHoloElement
{
    public int Id;
    public CadiaHoloShape Shape;
    public CadiaHoloLayer Layer;
    public CadiaFlightLevel Level;
    public Color Color;
    public Vector2 A;
    public Vector2 B;
    public byte Kind;
    public string Text = string.Empty;
    public string Author = string.Empty;
    public Guid AuthorId;
    public bool Done;
}
