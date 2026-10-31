using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Гололитический стол командования. Карта рисуется из настоящих этажей поля; нанесённое на неё видят все
/// столы той же стороны. Империум ставит на ней чертежи обороны — они появляются в мире голограммами.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaHoloTableComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction = CadiaFaction.Imperium;

    /// <summary>Доступен ли планировщик обороны (чертежи в мире).</summary>
    [DataField]
    public bool Planner = true;

    /// <summary>Только просмотр (гололитический визор командира): карту видно, править нельзя.</summary>
    [DataField, AutoNetworkedField]
    public bool ReadOnly;
}

/// <summary>Действие надетого визора: открыть голокарту своей стороны.</summary>
public sealed partial class CadiaHoloVisorActionEvent : Content.Shared.Actions.InstantActionEvent;

[Serializable, NetSerializable]
public enum CadiaHoloUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CadiaHoloLayer : byte
{
    Assault,
    Defense,
    Intel,
}

[Serializable, NetSerializable]
public enum CadiaHoloShape : byte
{
    Line,
    Arrow,
    Circle,
    Marker,
    Text,
    Blueprint,
}

[Serializable, NetSerializable]
public enum CadiaHoloMarkerType : byte
{
    Squad,
    Enemy,
    Target,
    Vehicle,
    Landing,
}

[Serializable, NetSerializable]
public enum CadiaBlueprintKind : byte
{
    Sandbags,
    Wall,
    TankTrap,
    RazorWire,
    Hydra,
    Mine,
    Pillbox,
    AtGun,
    BolterNest,
    Stakes,
    AtMine,
    ChaosAltar,
    SpikeBarricade,
    WarpTrap,
}

/// <summary>Какой стороне какие чертежи: общая проверка для сервера и кнопок на голокарте.</summary>
public static class CadiaBlueprintRules
{
    public static bool Allowed(CadiaBlueprintKind kind, CadiaFaction faction)
    {
        return kind switch
        {
            CadiaBlueprintKind.ChaosAltar or CadiaBlueprintKind.SpikeBarricade or CadiaBlueprintKind.WarpTrap => faction == CadiaFaction.Chaos,
            CadiaBlueprintKind.Pillbox or CadiaBlueprintKind.Hydra or CadiaBlueprintKind.Wall => faction == CadiaFaction.Imperium,
            _ => true,
        };
    }
}

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

[Serializable, NetSerializable]
public enum CadiaHoloLiveKind : byte
{
    Friendly,
    Commander,
    Pylon,
    PylonFallen,
    Citadel,
    Shuttle,
    AntiAir,
    Strike,
    EnemyContact,
    SectorImperium,
    SectorChaos,
    SectorNeutral,
    Beacon,
}

[Serializable, NetSerializable]
public readonly record struct CadiaHoloLive(CadiaHoloLiveKind Kind, Vector2 Position, CadiaFlightLevel Level, int Squad = 0, string? Label = null, float Radius = 0f);

[Serializable, NetSerializable]
public sealed class CadiaHoloTableState : BoundUserInterfaceState
{
    public CadiaFaction Faction;
    public bool Commander;
    public bool Planner;
    public bool ReadOnly;
    public Vector2 TablePosition;
    public List<CadiaFlightLevel> Levels = new();
    public List<CadiaHoloElement> Elements = new();
    public List<CadiaHoloLive> Live = new();
    public List<CadiaHoloSquad> Squads = new();
}

/// <summary>Плитки этажа для голокарты (0 — пусто, 1 — пол, 2 — стена/постройка, 3 — дверь).</summary>
[Serializable, NetSerializable]
public sealed class CadiaHoloTilesMessage : BoundUserInterfaceMessage
{
    public CadiaFlightLevel Level;
    public Vector2i Origin;
    public int Width;
    public int Height;
    public byte[] Tiles = Array.Empty<byte>();
}

[Serializable, NetSerializable]
public sealed class CadiaHoloRequestTilesMessage(CadiaFlightLevel level) : BoundUserInterfaceMessage
{
    public readonly CadiaFlightLevel Level = level;
}

[Serializable, NetSerializable]
public sealed class CadiaHoloAddMessage(CadiaHoloElement element) : BoundUserInterfaceMessage
{
    public readonly CadiaHoloElement Element = element;
}

[Serializable, NetSerializable]
public sealed class CadiaHoloEraseMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class CadiaHoloUndoMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CadiaHoloClearMessage(CadiaHoloLayer layer) : BoundUserInterfaceMessage
{
    public readonly CadiaHoloLayer Layer = layer;
}

/// <summary>
/// Голограмма чертежа в мире: силуэт будущей постройки. Сапёр подаёт материал и строит её на месте.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaBlueprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaBlueprintKind Kind;

    /// <summary>Что построится (прототип) — по нему клиент рисует голографический силуэт.</summary>
    [DataField, AutoNetworkedField]
    public string Prototype = string.Empty;

    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    [DataField, AutoNetworkedField]
    public int ElementId;

    [DataField, AutoNetworkedField]
    public int Cost;
}

[Serializable, NetSerializable]
public sealed partial class CadiaBlueprintBuildDoAfterEvent : SimpleDoAfterEvent;
