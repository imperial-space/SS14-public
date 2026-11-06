using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>dish_drive TG: всасывает посуду поблизости и отправляет мусор в утилизатор.</summary>
[RegisterComponent]
public sealed partial class ImperialDishDriveComponent : Component
{
    [DataField]
    public List<EntProtoId> Collectable = new();

    [DataField]
    public List<EntProtoId> Disposable = new();

    [DataField]
    public string Container = "dish_drive";

    /// <summary>2 + уровень сервопривода.</summary>
    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan TransmitInterval = TimeSpan.FromMinutes(1);

    [DataField]
    public float DisposalRange = 7f;

    [DataField]
    public bool Suction = true;

    [DataField]
    public bool Transmit = true;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    [ViewVariables]
    public TimeSpan NextSuck;

    [ViewVariables]
    public TimeSpan NextTransmit;

    [DataField]
    public float UpdateRange = 1.5f;

    [DataField]
    public float UpdateBaseThrowSpeed = 3f;
}
