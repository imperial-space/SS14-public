using Content.Shared.Imperial.Cadia;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cadia;

/// <summary>Боец ещё не получил вводную — отдадим, когда в тело войдёт игрок.</summary>
[RegisterComponent]
public sealed partial class CadiaBriefingPendingComponent : Component;
