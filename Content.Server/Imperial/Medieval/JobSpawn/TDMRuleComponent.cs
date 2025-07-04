using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(TDMRuleSystem))]
public sealed partial class TDMRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> PlayersRed = new();

    [ViewVariables]
    public List<EntityUid> PlayersBlue = new();

    [DataField]
    public EntProtoId SpawnPointProtoRed = "SpawnPointTDMRed";

    [DataField]
    public EntProtoId SpawnPointProtoBlue = "SpawnPointTDMBlue";


    [ViewVariables]
    public EntityUid PirateShip = EntityUid.Invalid;
    [ViewVariables]
    public HashSet<EntityUid> InitialItems = new();
    [ViewVariables]
    public double InitialShipValue;


    public MapId? NukiePlanet;

    // TODO: use components, don't just cache entity UIDs
    // There have been (and probably still are) bugs where these refer to deleted entities from old rounds.
    public EntityUid? NukieOutpost;

    [DataField]
    public bool SpawnOutpost = false;
    [DataField]
    public ResPath[] Sector0 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector1 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector2 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector3 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector4 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector5 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector6 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector7 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector7Cave = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector9 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public ResPath[] Sector10 = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/Medieval/Sector0V03.yml")
    };

    [DataField]
    public String[] Name = new string[] //Imperal additional maps
    {
        new string("1"),
    };

    [DataField]
    public Dictionary<string, StartingGearPrototype> StartingGearPrototypes = new ();

    [DataField]
    public ProtoId<StartingGearPrototype> MedievalTDMRed = "MedievalTDMRed";

    [DataField]
    public ProtoId<StartingGearPrototype> MedievalTDMBlue = "MedievalTDMBlue";

    /// <summary>
    ///     Path to antagonist alert sound.
    /// </summary>
    [DataField("pirateAlertSound")]
    public SoundSpecifier PirateAlertSound = new SoundPathSpecifier(
        "/Audio/Ambience/Antag/pirate_start.ogg",
        AudioParams.Default.WithVolume(4));
}
