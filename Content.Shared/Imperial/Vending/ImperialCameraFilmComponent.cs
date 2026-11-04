using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>camera_film TG: добавляет снимки в камеру.</summary>
[RegisterComponent]
public sealed partial class ImperialCameraFilmComponent : Component
{
    [DataField]
    public int Pictures = 10;
}
