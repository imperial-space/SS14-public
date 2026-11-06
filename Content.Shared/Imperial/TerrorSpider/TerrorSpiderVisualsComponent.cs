using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

/// <summary>Спрайты по стадиям: Alive[stage] при жизни, Dead[stage] после смерти.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderVisualsComponent : Component
{
    [DataField(required: true)]
    public List<string> Alive = new();

    [DataField(required: true)]
    public List<string> Dead = new();
}
