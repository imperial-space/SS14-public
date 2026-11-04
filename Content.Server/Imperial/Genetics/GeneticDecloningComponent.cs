using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics;

/// <summary>Медленный клеточный распад после срыва. Лечится мутадоном.</summary>
[RegisterComponent]
public sealed partial class GeneticDecloningComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Cellular"] = 1 } };

    [ViewVariables]
    public TimeSpan NextDamage;
}
