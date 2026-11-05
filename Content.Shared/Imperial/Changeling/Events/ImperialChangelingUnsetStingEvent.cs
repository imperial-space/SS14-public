using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/atom/movable/screen/ling/sting/Click(): убрать жало щелчком по иконке.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingUnsetStingEvent : EntityEventArgs;
