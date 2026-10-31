using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Z-уровень как в SS13: вся карта — один этаж. Карты с одинаковым <see cref="Stack"/> складываются в стопку по
/// <see cref="Level"/> (0 — нижний). Этажи совмещены по мировым координатам: клетка над клеткой — та же точка мира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelMapComponent : Component
{
    /// <summary>Имя стопки этажей. Пустое — карта ни с чем не связана.</summary>
    [DataField, AutoNetworkedField]
    public string Stack = string.Empty;

    [DataField, AutoNetworkedField]
    public int Level;

    /// <summary>Карта этажом ниже (выставляется системой при связывании стопки).</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Below;

    /// <summary>Карта этажом выше.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Above;

    /// <summary>
    /// На сколько этажей ниже лежит <see cref="Below"/>. Между ними может быть пустой воздух (небо Кадии — 7 этажей
    /// над поверхностью): нижний этаж рисуется дальше, падение сквозь разрыв считается за все этажи.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int BelowGap = 1;

    /// <summary>Пол этого этажа не обрушается без опоры (поле боя Кадии: небо, орбита, крейсеры).</summary>
    [DataField]
    public bool NoCollapse;
}

/// <summary>
/// На нижней (основной) карте стопки: файлы остальных этажей. При инициализации карты они подгружаются
/// и связываются с ней; маппер пишет их командой <c>zlevel save</c>.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelStackComponent : Component
{
    /// <summary>Этаж → файл карты этого этажа.</summary>
    [DataField]
    public Dictionary<int, ResPath> Levels = new();

    /// <summary>Этажи уже подгружены (чтобы не грузить дважды).</summary>
    [ViewVariables]
    public bool Loaded;
}

/// <summary>
/// Флаги obj_flags SS13, влияющие на движение между этажами: BLOCK_Z_OUT_DOWN, BLOCK_Z_IN_UP и т. д.
/// Решётка (lattice) и трап — <see cref="OutDown"/>; мостик (catwalk) — <see cref="OutDown"/> и <see cref="InUp"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ZBlockComponent : Component
{
    /// <summary>Не даёт упасть с этой клетки вниз.</summary>
    [DataField]
    public bool OutDown;

    /// <summary>Не даёт подняться с этой клетки вверх.</summary>
    [DataField]
    public bool OutUp;

    /// <summary>Не даёт упасть сверху на эту клетку.</summary>
    [DataField]
    public bool InDown;

    /// <summary>Не даёт подняться снизу на эту клетку.</summary>
    [DataField]
    public bool InUp;
}

/// <summary>
/// Невидимая метка клетки открытого пространства: через неё срабатывает падение (StepTrigger).
/// Расставляется системой сама по тайлам открытого пространства и не сохраняется в карту.
/// </summary>
[RegisterComponent]
public sealed partial class ZOpenSpaceComponent : Component;

/// <summary>
/// Трап (/obj/structure/ladder): сам связывается с трапом ровно этажом выше и ниже. Клик — вверх, Alt-клик — вниз.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLadderComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Up;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Down;

    /// <summary>travel_time = 1 SECONDS.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(1);

    /// <summary>Связываются только проходы одного вида: трап с трапом, вентиляция с вентиляцией.</summary>
    [DataField]
    public string Kind = "ladder";

    /// <summary>Начало ключей локализации для подсказок и сообщений (zlevel-ladder-climb-up и т.д.).</summary>
    [DataField]
    public string LocPrefix = "zlevel-ladder";

    /// <summary>Тянуть ли за собой тех, кого тащишь (ZMOVE_INCLUDE_PULLED). В вентиляцию никого не протащишь.</summary>
    [DataField]
    public bool IncludePulled = true;

    /// <summary>Делит ли газ своей клетки с клеткой прохода этажом ниже.</summary>
    [DataField]
    public bool PassGas;
}

[Serializable, NetSerializable]
public enum ZLadderVisuals : byte
{
    /// <summary>Состояние спрайта ladderXY: X — есть трап выше, Y — есть трап ниже.</summary>
    State,
}

/// <summary>
/// Лестница (/obj/structure/stairs): направление подъёма — куда она повёрнута. Подъём происходит на последней
/// ступени цепочки, когда уходишь с неё вперёд; спуск — шагом в открытое пространство над ней.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZStairsComponent : Component
{
    /// <summary>Есть ли у лестницы левый/средний/правый спрайты (stairs-l/m/r).</summary>
    [DataField]
    public bool MergedSprites = true;

    /// <summary>Базовое имя состояния спрайта.</summary>
    [DataField]
    public string BaseState = "stairs";

    /// <summary>Площадка наверху: метка на клетке над ступенью, шаг с которой назад спускает на лестницу (только сервер).</summary>
    [ViewVariables]
    public EntityUid? Top;
}

/// <summary>Метка площадки над лестницей: зашёл на неё со стороны этажа, идя вниз, — оказался на лестнице.</summary>
[RegisterComponent]
public sealed partial class ZStairsTopComponent : Component
{
    [ViewVariables]
    public EntityUid? Stairs;
}

[Serializable, NetSerializable]
public enum ZStairsVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public sealed partial class ZLadderClimbDoAfterEvent : DoAfterEvent
{
    [DataField]
    public bool GoingUp;

    private ZLadderClimbDoAfterEvent()
    {
    }

    public ZLadderClimbDoAfterEvent(bool goingUp)
    {
        GoingUp = goingUp;
    }

    public override DoAfterEvent Clone() => this;
}

public sealed partial class ZLookUpActionEvent : InstantActionEvent;

public sealed partial class ZLookDownActionEvent : InstantActionEvent;

/// <summary>
/// Игрок смотрит на соседний этаж (lookup/lookdown SS13). Камера остаётся на игроке, а клиент рисует этаж
/// <see cref="Map"/> поверх своего; сервер держит на том этаже точку взгляда, чтобы игроку приходили его сущности.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLookingComponent : Component
{
    /// <summary>Карта этажа, на который смотрят.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public bool Up;

    /// <summary>Точка взгляда на том этаже (только сервер).</summary>
    [ViewVariables]
    public EntityUid Holder;
}

/// <summary>
/// Грузовой лифт: платформа Size×Size клеток с центром в этой сущности. Ездит по шахте (открытое пространство
/// или пустота над и под платформой) и везёт всё незакреплённое, что на ней стоит. С платформы не падают.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZElevatorComponent : Component
{
    [DataField]
    public int Size = 3;

    /// <summary>Время на один этаж.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(4);

    /// <summary>Карта этажа, куда едет лифт (только сервер).</summary>
    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public TimeSpan NextMove;
}

/// <summary>Кнопка вызова лифта: зовёт ближайший лифт стопки на свой этаж.</summary>
[RegisterComponent]
public sealed partial class ZElevatorButtonComponent : Component
{
    /// <summary>Насколько далеко от края платформы может стоять кнопка.</summary>
    [DataField]
    public float Range = 4f;
}

/// <summary>
/// Межэтажная связь коммуникаций (multi-z cable hub, multi-deck adapter, мусоропровод вверх/вниз): сама находит
/// такую же сущность того же вида ровно этажом выше и ниже в той же клетке.
/// </summary>
[RegisterComponent]
public sealed partial class ZLinkComponent : Component
{
    /// <summary>Связываются только одинаковые виды: кабель HV с кабелем HV, труба с трубой и т.д.</summary>
    [DataField(required: true)]
    public string Kind = string.Empty;

    [ViewVariables]
    public EntityUid? Up;

    [ViewVariables]
    public EntityUid? Down;
}

/// <summary>Переходник труб между этажами: труба наверху и внизу — одна сеть.</summary>
[RegisterComponent]
public sealed partial class ZPipeAdapterComponent : Component
{
    /// <summary>Имя узла трубы в NodeContainer.</summary>
    [DataField]
    public string Node = "pipe";

    /// <summary>С какими переходниками уже соединены узлы (только сервер).</summary>
    [ViewVariables]
    public HashSet<EntityUid> Linked = new();
}

/// <summary>Труба мусоропровода вверх/вниз: что в неё попало из сети — выходит из такой же трубы на другом этаже.</summary>
[RegisterComponent]
public sealed partial class ZDisposalLiftComponent : Component
{
    /// <summary>Куда отправлять: true — наверх, false — вниз (если на том этаже есть парная труба).</summary>
    [DataField]
    public bool GoesUp = true;
}

/// <summary>Моб под управлением ИИ: открытое пространство для него — стена, он обходит дыры (как NPC в SS13).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZAvoidOpenSpaceComponent : Component;

/// <summary>Крюк-кошка (climbing hook с Icebox): забросить в проём над собой и вылезти на этаж выше.</summary>
[RegisterComponent]
public sealed partial class ZClimbingHookComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);
}

[Serializable, NetSerializable]
public sealed partial class ZClimbHookDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Летающее существо: не падает в проёмы между этажами.</summary>
[RegisterComponent]
public sealed partial class ZFlyerComponent : Component;

/// <summary>Подняться или опуститься на этаж: призраки, камера ИИ, летящие на джетпаке.</summary>
public sealed partial class ZMoveUpActionEvent : InstantActionEvent;

public sealed partial class ZMoveDownActionEvent : InstantActionEvent;

/// <summary>Радиальное меню лестницы, когда можно и вверх, и вниз (как в SS13).</summary>
[Serializable, NetSerializable]
public enum ZLadderUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ZLadderChoiceMessage(bool up) : BoundUserInterfaceMessage
{
    public readonly bool Up = up;
}
