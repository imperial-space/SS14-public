using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>Оперативник протокола «Эпсилон»: получает цель, как только в тело входит игрок.</summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonOperativeComponent : Component
{
    [DataField]
    public string Objective = "ImperialEpsilonKillCrewObjective";
}
