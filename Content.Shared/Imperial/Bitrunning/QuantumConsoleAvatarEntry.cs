using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

[Serializable, NetSerializable]
public sealed class QuantumConsoleAvatarEntry(string name, string pilot, float health, float brute, float burn, float toxin, float airloss)
{
    public string Name = name;
    public string Pilot = pilot;
    public float Health = health;
    public float Brute = brute;
    public float Burn = burn;
    public float Toxin = toxin;
    public float Airloss = airloss;
}
