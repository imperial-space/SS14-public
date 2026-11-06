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

/// <summary>Место ритуала над завесой: круг в часовне флагмана для Хаоса, пилон для Империума.</summary>
[RegisterComponent]
public sealed partial class CadiaVeilRitualComponent : Component
{
    [DataField(required: true)]
    public CadiaFaction Faction;

    /// <summary>Кто может вести ритуал.</summary>
    [DataField(required: true)]
    public List<string> Roles = new();

    /// <summary>На сколько ритуал сдвигает конец подготовки: Хаос — раньше, Империум — позже.</summary>
    [DataField]
    public TimeSpan Shift = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);
}
