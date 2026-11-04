using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>След ритуала: бойцы стороны рядом понемногу теряют или набирают мораль.</summary>
[RegisterComponent]
public sealed partial class CadiaRitualMarkComponent : Component
{
    [DataField(required: true)]
    public CadiaFaction Faction;

    /// <summary>Сколько морали за один такт (раз в две секунды).</summary>
    [DataField(required: true)]
    public float Delta;

    [DataField]
    public float Radius = 4f;
}
