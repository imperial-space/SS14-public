using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Труба мусоропровода вверх/вниз: что в неё попало из сети — выходит из такой же трубы на другом этаже.</summary>
[RegisterComponent]
public sealed partial class ZDisposalLiftComponent : Component
{
    /// <summary>Куда отправлять: true — наверх, false — вниз (если на том этаже есть парная труба).</summary>
    [DataField]
    public bool GoesUp = true;
}
