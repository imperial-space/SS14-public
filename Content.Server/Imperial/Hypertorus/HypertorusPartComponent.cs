using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Деталь гиперторуса вокруг ядра: порт, интерфейс или угол.</summary>
[RegisterComponent]
public sealed partial class HypertorusPartComponent : Component
{
    [DataField(required: true)]
    public HypertorusPartType PartType;

    /// <summary>Коробка, в которую деталь разбирается ломом.</summary>
    [DataField(required: true)]
    public EntProtoId Box;

    [ViewVariables] public bool Active;
    [ViewVariables] public bool FusionStarted;
    [ViewVariables] public bool PanelOpen;
    [ViewVariables] public bool Cracked;
    [ViewVariables] public EntityUid? Core;

    /// <summary>Интерфейс выдаёт памятку при первой активации.</summary>
    [ViewVariables] public bool Activated;
}
