using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed class WantedData
{
    public string Job = "";
    public string FlavorText = "";
    public ProtoId<MedievalFactionPrototype> Faction;
    public HumanoidCharacterProfile Profile;
    public string Performer = "";
    public string Details = "";

    public WantedData(HumanoidCharacterProfile profile, string job, ProtoId<MedievalFactionPrototype> faction, string performer, string flavorText, string details)
    {
        Profile = profile;
        Job = job;
        FlavorText = flavorText;
        Details = details;
        Faction = faction;
        Performer = performer;
    }
}
