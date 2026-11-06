using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Шанс заблокировать атаку, пока оружие в руке (block_chance из SS13).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodBlockComponent : Component
{
    [DataField]
    public float Chance = 0.3f;

    /// <summary>Блокирует только удары ближнего боя. Иначе — любую атаку, у которой есть атакующий.</summary>
    [DataField]
    public bool MeleeOnly = true;

    /// <summary>Блокирует только удары мехов (высокочастотный клинок).</summary>
    [DataField]
    public bool MechsOnly;

    /// <summary>Во сколько раз растёт шанс, когда оружие держат двумя руками.</summary>
    [DataField]
    public float WieldedMultiplier = 1f;

    /// <summary>Блокирует только включённое оружие (энергомечи).</summary>
    [DataField]
    public bool RequiresToggle;

    [DataField]
    public SoundSpecifier? BlockSound = new SoundPathSpecifier("/Audio/Imperial/heretic/parry.ogg");
}
