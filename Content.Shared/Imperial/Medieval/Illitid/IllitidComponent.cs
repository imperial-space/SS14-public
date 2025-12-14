using System.Numerics;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Illitid;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class IllitidComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public int PsiLevel = 5;

    [DataField("maxPsiLevel")]
    public int PsiRegenCap = 5;

    [DataField("timeToNextPsi")]
    public float TimeToNextPsiLevel = 30f;

    [ViewVariables]
    public float Accumulator = 0;
    
    [DataField]
    public ProtoId<AlertPrototype> PsiAlert = "MedievalIllitidPsi";


    [DataField] public EntityUid? ThoughtAction;
    [DataField] public EntityUid? MassThoughtAction;
    [DataField] public EntityUid? ForceTalkAction;
    [DataField] public EntityUid? BlindnessAction;
}
