// this content is under ICLA licence, read more on https://wiki.imperialspace.net/icla
// Copyright: @crookielv

using Content.Shared.Access;
using Content.Shared.Damage;
using Content.Shared.Imperial.Crook.Visuals;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Crook.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MetalDetectorComponent : Component
{
    [DataField("allowedAccess")]
    public List<ProtoId<AccessLevelPrototype>> AllowedAccess = new() { "Security", "Command" };

    [DataField("ignoredPrototypes")]
    public HashSet<EntProtoId> IgnoredPrototypes = new();

    [DataField("maxRecursionDepth")]
    public int MaxRecursionDepth = 5;

    [DataField("dischargedSlots")]
    public List<string> DischargedSlots = new()
    {
        "pocket1",
        "pocket2",
        "belt",
        "backpack",
        "idCard",
        "suitStorage",
        "outerClothing",
    };

    [DataField("stateResetDelay")]
    public TimeSpan StateResetDelay = TimeSpan.FromSeconds(2);

    [DataField("nextStateReset", customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStateReset;

    [DataField("shockDamage")]
    public DamageSpecifier ShockDamage = new()
    {
        DamageDict = new()
        {
            { "Shock", 5 },
        },
    };

    [DataField("shockDuration")]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(1);

    [DataField("shockSound")]
    public SoundSpecifier ShockSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    [DataField("clearSound")]
    public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");

    [DataField("alertSound")]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    [ViewVariables]
    public MetalDetectorVisualState State = MetalDetectorVisualState.Off;

    [ViewVariables]
    public HashSet<EntityUid> CollidingEntities = new();
}
