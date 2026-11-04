using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

/// <summary>
/// Носитель блоба (datum/antagonist/blob/infection): действие «Pop» и авто-выпуск через 6 минут.
/// </summary>
[RegisterComponent]
public sealed partial class BlobCarrierComponent : Component
{
    [DataField]
    public EntProtoId PopAction = "ActionBlobPop";

    [DataField]
    public EntProtoId OvermindPrototype = "MobBlobOvermind";

    /// <summary>OVERMIND_STARTING_AUTO_PLACE_TIME.</summary>
    [DataField]
    public TimeSpan AutoPopDelay = TimeSpan.FromMinutes(6);

    /// <summary>starting_points_human_blob.</summary>
    [DataField]
    public float StartingPoints = 60f;

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/blob/blobalert.ogg");

    [ViewVariables] public EntityUid? PopActionEntity;
    [ViewVariables] public TimeSpan AutoPopTime;
    [ViewVariables] public EntityUid? Rule;
}
