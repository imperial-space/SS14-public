using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/datum/antagonist/bitrunning_glitch: антагонист домена.</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchComponent : Component
{
    /// <summary>threat: минимальная угроза сервера; половина вычитается при появлении.</summary>
    [DataField]
    public int Threat;

    /// <summary>Боевое искусство (киберполиция — Спящий карп). ID прототипа Imperial/MartialArts, выдаётся через отражение.</summary>
    [DataField]
    public string? MartialArt;

    /// <summary>preview_outfit: снаряжение, которое возвращается при прорыве на станцию (reset_equipment).</summary>
    [DataField]
    public Robust.Shared.Prototypes.ProtoId<Content.Shared.Roles.StartingGearPrototype>? Gear;

    [ViewVariables]
    public EntityUid? Server;

    /// <summary>Последняя клетка вне сейфхауса — сюда возвращают на границе (virtual_entity).</summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? LastOutside;

    /// <summary>Уже идёт прорыв на станцию.</summary>
    [ViewVariables]
    public bool Breaching;

    /// <summary>Глюк вырвался на станцию (component/glitch).</summary>
    [ViewVariables]
    public bool OnStation;
}
