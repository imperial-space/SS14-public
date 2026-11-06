using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Нар'Си (obj/narsie).</summary>
[RegisterComponent]
public sealed partial class CultNarSieComponent : Component
{
    [DataField] public float ConsumeRange = 12f;
    [DataField] public float Speed = 1f;
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public TimeSpan NextMove;
    [ViewVariables] public TimeSpan NextRetarget;
    [ViewVariables] public TimeSpan NextMesmerize;
    [ViewVariables] public System.Numerics.Vector2 MoveDir;
    [ViewVariables] public TimeSpan SpawnAnimEnd;
    [ViewVariables] public bool Started;

    /// <summary>souls_needed / soul_goal / souls: массовое обращение.</summary>
    [ViewVariables] public HashSet<EntityUid> SoulsNeeded = new();
    [ViewVariables] public int SoulGoal;
    [ViewVariables] public int Souls;

    [DataField]
    public TimeSpan MoveDelay2 = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NarsieActMobDelay = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan MoveDelay = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan EndStageDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public float ChooseDirectionChance = 0.6f;
}
