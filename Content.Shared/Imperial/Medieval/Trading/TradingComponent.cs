using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Trading;

/// <summary>
/// This component manages a store which players can use to purchase different listings
/// through the ui. The currency, listings, and categories are defined in yaml.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TradingComponent : Component
{
    [DataField]
    public int Balance;

    [DataField]
    public ProtoId<CurrencyPrototype> Currency;

    [DataField]
    public EntityUid? AccountOwner = null;

    [DataField]
    public HashSet<ProtoId<GuildTypePrototype>> GuildTypes;

    [DataField]
    public List<Guild> Guilds = new();

    [ViewVariables]
    public HashSet<Guild> LastAvailableGuilds = new();

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool OwnerOnly;

    #region audio
    [DataField]
    public SoundSpecifier BuySuccessSound = new SoundPathSpecifier("/Audio/Effects/kaching.ogg");
    #endregion
}
