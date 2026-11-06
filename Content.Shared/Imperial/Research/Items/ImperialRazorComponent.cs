using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Бритва (razor): сбривает бороду, а без неё — волосы.</summary>
[RegisterComponent]
public sealed partial class ImperialRazorComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3f);
}
