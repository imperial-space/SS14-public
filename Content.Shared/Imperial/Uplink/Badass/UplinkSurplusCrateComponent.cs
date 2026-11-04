using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Ящик излишков Синдиката: при создании набивается случайными позициями аплинка на заданную сумму.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkSurplusCrateComponent : Component
{
    [DataField]
    public int Value = 40;

    /// <summary>Объединённый ящик: содержимое появляется только после ключа.</summary>
    [DataField, AutoNetworkedField]
    public bool United;

    [ViewVariables]
    public List<EntProtoId> Contents = new();

    [ViewVariables, AutoNetworkedField]
    public bool Unlocked;
}
