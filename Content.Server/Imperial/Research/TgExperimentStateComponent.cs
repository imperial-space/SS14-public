using System.Linq;
using Content.Server.Arcade.SpaceVillain;
using Content.Server.Botany.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Construction.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Research.Experiments;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Research;

/// <summary>Прогресс экспериментов TG на сервере исследований.</summary>
[RegisterComponent]
public sealed partial class TgExperimentStateComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Progress = new();

    [ViewVariables]
    public HashSet<string> Completed = new();

    /// <summary>Уже засчитанные объекты (каждый сканируется один раз на эксперимент).</summary>
    [ViewVariables]
    public Dictionary<string, HashSet<EntityUid>> Scanned = new();

    /// <summary>Привязанные аркадные автоматы и их запас призов на момент проверки.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, int> Arcades = new();
}
