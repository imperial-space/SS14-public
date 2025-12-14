using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Myrmex
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class MyrmexHungerComponent : Component
    {
        [DataField, AutoNetworkedField] public TimeSpan? LastEaten;
        [DataField, AutoNetworkedField] public float EatCooldownSeconds = 180;
        [DataField, AutoNetworkedField] public float SecondsToHungry = 1500;
        [DataField, AutoNetworkedField] public float HungrySpeedModifier = 0.3f;
        [DataField, AutoNetworkedField] public List<MyrmexBuff> Buffs = [];
        [DataField, AutoNetworkedField] public EntProtoId IconPrototype = "HungerIconStarving";
    }
}
