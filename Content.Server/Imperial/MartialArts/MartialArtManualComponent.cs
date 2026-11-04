using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>book/granter/martial: учебник или свиток.</summary>
[RegisterComponent]
public sealed partial class MartialArtManualComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    /// <summary>martial_name.</summary>
    [DataField(required: true)]
    public LocId MartialName;

    /// <summary>greet.</summary>
    [DataField(required: true)]
    public LocId Greet;

    /// <summary>remarks: что бормочет читатель над страницами.</summary>
    [DataField]
    public List<LocId> Remarks = new();

    /// <summary>uses.</summary>
    [DataField]
    public int Uses = 1;

    /// <summary>pages_to_mastery.</summary>
    [DataField]
    public int Pages = 3;

    /// <summary>reading_time на страницу.</summary>
    [DataField]
    public TimeSpan PageTime = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public int PagesRead;

    /// <summary>recoil(): что будет, если читать исписанный учебник.</summary>
    [DataField]
    public MartialManualRecoil Recoil = MartialManualRecoil.None;

    /// <summary>Во что превращается свиток после прочтения (blankscroll).</summary>
    [DataField]
    public EntProtoId? SpentProto;
}
