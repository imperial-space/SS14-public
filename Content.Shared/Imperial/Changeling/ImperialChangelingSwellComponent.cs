using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>headcrab: animate(user, transform = user.transform * 1.5, color = COLOR_RED, time = 1 SECONDS).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialChangelingSwellComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public float Scale = 1.5f;
}
