using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Акромегалия: носитель иногда бьётся головой о притолоку шлюза.</summary>
[RegisterComponent]
public sealed partial class GeneticHeadBonkComponent : Component
{
    [DataField]
    public float Chance = 0.08f;

    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = new() { ["Blunt"] = 5 } };

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/bang.ogg");

    [ViewVariables]
    public Vector2i? LastTile;
}
