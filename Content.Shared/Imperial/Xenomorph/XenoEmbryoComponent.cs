using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Эмбрион ксеноморфа в теле носителя (body_egg/alien_embryo). Стадия видна ксеноморфам.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoEmbryoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage;

    /// <summary>growth_time: время на одну стадию.</summary>
    [DataField]
    public TimeSpan GrowthTime = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextStage;

    [ViewVariables]
    public bool Bursting;

    [ViewVariables]
    public EntityUid? BurstSpawner;

    [ViewVariables]
    public TimeSpan BurstDeadline;

    /// <summary>attempt_grow(gib_on_success = FALSE): личинка выползает, не разрывая носителя.</summary>
    [ViewVariables]
    public bool NoGibBurst;
}
