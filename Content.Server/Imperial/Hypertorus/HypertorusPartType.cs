using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Роль детали гиперторуса.</summary>
public enum HypertorusPartType : byte
{
    FuelInput,
    ModeratorInput,
    WasteOutput,
    Interface,
    Corner,
}
