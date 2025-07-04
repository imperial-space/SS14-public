using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.ImperialStore;


/// <summary>
/// This component manages a store which players can use to purchase different listings
/// through the ui. The currency, listings, and categories are defined in yaml.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialStoreComponent : Component
{
    [DataField]
    public LocId Name = "store-ui-default-title";

    [DataField]
    public LocId WithdrawText = "store-ui-default-withdraw-text";

    [DataField]
    public LocId CurrencyTitle = "store-ui-currency-title";

    [DataField]
    public LocId CannotAccessStoreText = "store-not-account-owner";

    /// <summary>
    /// Store's current balance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, Access(typeof(SharedImperialStoreSystem))]
    public Dictionary<string, FixedPoint2> Balance = [];

    /// <summary>
    /// If set and 'DepositCount' is not zero then 'LastDepositSum' will replace the 'Balance', otherwise they will be added together.
    /// </summary>
    public bool BalanceOverride = false;

    /// <summary>
    /// Length of the 'LastDeposits'. If it's equal to zero then 'LastDeposits' will be empty and all of the deposits will be directly added to the balance.
    /// </summary>
    [DataField]
    public int DepositCount
    {
        set => Array.Resize(ref LastDeposits, value);
        get => LastDeposits.Length;
    }

    /// <summary>
    /// Last modified index inside of the 'LastDeposits'. Required to replace old deposits.
    /// </summary>
    public int LastDepositIndex;

    /// <summary>
    /// Last deposits.
    /// </summary>
    public Dictionary<string, FixedPoint2>[] LastDeposits = [];

    /// <summary>
    /// Sum of the last deposits.
    /// </summary>
    public Dictionary<string, FixedPoint2> LastDepositSum = [];

    /// <summary>
    /// All the listing categories that are available on this store.
    /// The available listings are partially based on the categories.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ImperialStoreCategoryPrototype>> Categories = new();

    /// <summary>
    /// The list of currencies that can be inserted into this store.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ImperialCurrencyPrototype>> CurrencyWhitelist = new();

    /// <summary>
    /// The person who "owns" the store/account. Used if you want the listings to be fixed
    /// regardless of who activated it. I.E. role specific items for uplinks.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid? AccountOwner = null;

    /// <summary>
    /// All listings, including those that aren't available to the buyer
    /// </summary>
    [DataField]
    public HashSet<ImperialListingData> Listings = new();

    /// <summary>
    /// All available listings from the last time that it was checked.
    /// </summary>
    [ViewVariables]
    public HashSet<ImperialListingData> LastAvailableListings = new();

    /// <summary>
    /// All current entities bought from this shop. Useful for keeping track of refunds and upgrades.
    /// </summary>
    [ViewVariables, DataField]
    public List<EntityUid> BoughtEntities = new();

    /// <summary>
    /// The total balance spent in this store. Used for refunds.
    /// </summary>
    [ViewVariables, DataField]
    public Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2> BalanceSpent = new();

    /// <summary>
    /// Controls if the store allows refunds
    /// </summary>
    [ViewVariables, DataField]
    public bool RefundAllowed;

    /// <summary>
    /// Checks if store can be opened by the account owner only.
    /// Not meant to be used with uplinks.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool OwnerOnly;

    /// <summary>
    /// The map the store was originally from, used to block refunds if the map is changed
    /// </summary>
    [DataField]
    public EntityUid? StartingMap;

    #region audio
    /// <summary>
    /// The sound played to the buyer when a purchase is succesfully made.
    /// </summary>
    [DataField]
    public SoundSpecifier BuySuccessSound = new SoundPathSpecifier("/Audio/Effects/kaching.ogg");
    #endregion

    /// <summary>
    /// The last tick in which this store opened
    /// </summary>
    [ViewVariables]
    public GameTick LastOpenTick = new();
}
