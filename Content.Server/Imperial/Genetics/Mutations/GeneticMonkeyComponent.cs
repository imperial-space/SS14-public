using System.Linq;
using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Обезьяна, в которую превратился гуманоид из-за обезьяньего гена.</summary>
[RegisterComponent]
public sealed partial class GeneticMonkeyComponent : Component;
