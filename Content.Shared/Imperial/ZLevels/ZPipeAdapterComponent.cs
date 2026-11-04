using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

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
