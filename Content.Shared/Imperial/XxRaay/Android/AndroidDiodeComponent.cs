using System;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Компонент состояния диода андроида
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AndroidDiodeComponent : Component
{
    /// <summary>
    /// Есть ли ещё диод
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasDiode = true;

    /// <summary>
    /// Идёт ли в данный момент процесс вырезания диода
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsCuttingInProgress;
}

/// <summary>
/// Визуальные данные диода андроида
/// </summary>
[Serializable, NetSerializable]
public enum AndroidDiodeVisuals : byte
{
    DiodeRemoved
}

/// <summary>
/// DoAfter вырезания
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AndroidDiodeCutDoAfterEvent : SimpleDoAfterEvent
{
}

