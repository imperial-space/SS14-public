using Content.Shared.CartridgeLoader;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Stack;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Chat;
using Robust.Shared.Utility;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Interaction;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Личные счета экономики TG (code/modules/economy/account.dm, SSeconomy): счёт на ID-карте,
/// стартовые зарплаты, зарплата каждые 5 минут со счёта отдела, снятие и внесение наличных.
/// </summary>
public sealed class ImperialBankSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly Content.Server.CartridgeLoader.CartridgeLoaderSystem _loader = default!;
    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;

    private static readonly ProtoId<StackPrototype> Credit = "Credit";
    private const string NtPayProgram = "ImperialNtPayCartridge";
    private static readonly SoundSpecifier CardBeep = new SoundPathSpecifier("/Audio/Imperial/Cargo/twobeep.ogg");

    /// <summary>PAYCHECK_*.</summary>
    public const int PaycheckLower = 25;
    public const int PaycheckCrew = 50;
    public const int PaycheckCommand = 100;

    private static readonly ProtoId<ImperialPayrollPrototype> Payroll = "Default";

    /// <summary>STARTING_PAYCHECKS.</summary>
    private const int StartingPaychecks = 5;

    /// <summary>DEBT_COLLECTION_COEFF.</summary>
    private const float DebtCollection = 0.75f;

    private static readonly TimeSpan PaydayInterval = TimeSpan.FromMinutes(5);

    public sealed class Account
    {
        public int Id;
        public string Holder = string.Empty;
        public int Balance;
        public int Debt;
        public string? Job;
        public int Paycheck;

        /// <summary>payday_modifier: множитель расы.</summary>
        public float Modifier = 1f;
        public ProtoId<CargoAccountPrototype>? Department;
        public EntityUid? Station;
        public readonly List<EntityUid> Cards = new();
        public readonly List<(TimeSpan Time, int Amount, string Reason)> History = new();
    }

    private readonly Dictionary<int, Account> _accounts = new();
    private TimeSpan? _nextPayday;

    public IReadOnlyDictionary<int, Account> Accounts => _accounts;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<ImperialBankCardComponent, ExaminedEvent>(OnExamined);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _accounts.Clear();
        _nextPayday = null;
    }

    #region Счета

    public Account? Get(int id) => _accounts.GetValueOrDefault(id);

    /// <summary>setup_unique_account_id().</summary>
    public Account Create(string holder, string? job, EntityUid? station)
    {
        var id = 0;
        for (var i = 0; i < 1000; i++)
        {
            id = _random.Next(111111, 1000000);
            if (!_accounts.ContainsKey(id))
                break;
        }

        var (paycheck, department) = JobPay(job);
        var account = new Account
        {
            Id = id,
            Holder = holder,
            Job = job,
            Paycheck = paycheck,
            Department = department,
            Station = station,
        };
        _accounts[id] = account;
        return account;
    }

    /// <summary>
    /// paycheck и paycheck_department должности из ведомости payroll.yml.
    /// Должности вне ведомости: главы 100, ЦК и силиконы без счёта, остальные 50.
    /// </summary>
    public (int Paycheck, ProtoId<CargoAccountPrototype>? Department) JobPay(string? job)
    {
        if (job == null)
            return (0, null);

        if (_proto.TryIndex(Payroll, out var payroll) && payroll.Jobs.TryGetValue(job, out var pay))
            return (pay.Paycheck, pay.Department);

        var departments = _proto.EnumeratePrototypes<DepartmentPrototype>().Where(d => d.Roles.Any(r => r.Id == job)).ToList();
        if (departments.Any(d => d.ID is "CentralCommand" or "Silicon"))
            return (0, null);

        var command = departments.Any(d => d.ID == "Command");
        var paycheck = command ? PaycheckCommand : PaycheckCrew;

        // Капитан платит из оборонного бюджета, ГП — из бюджета обслуживания (как в TG).
        if (job == "Captain")
            return (paycheck, "Security");

        foreach (var dep in departments.Where(d => d.ID != "Command"))
        {
            switch (dep.ID)
            {
                case "Cargo": return (paycheck, "Cargo");
                case "Engineering": return (paycheck, "Engineering");
                case "Medical": return (paycheck, "Medical");
                case "Science": return (paycheck, "Science");
                case "Security": return (paycheck, "Security");
                case "Civilian": return (paycheck, "Service");
            }
        }

        // ACCOUNT_CIV — гражданский бюджет; в SS14 его роль играет бюджет обслуживания.
        return (paycheck, "Service");
    }

    /// <summary>Счёт по ID-карте в руке, в слоте ID или в КПК.</summary>
    public Account? AccountOf(EntityUid user)
    {
        if (_idCard.TryFindIdCard(user, out var card) && TryComp<ImperialBankCardComponent>(card, out var bank)
            && Get(bank.AccountId) is { } byCard)
            return byCard;

        // КПК с NT Pay в руках или в слоте.
        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            if (_loader.TryGetProgram<ImperialNtPayCartridgeComponent>(item, out _, out var app) && Get(app.AccountId) is { } byApp)
                return byApp;
        }

        return null;
    }

    /// <summary>adjust_money(): списание только при достатке, при пополнении сначала гасится долг.</summary>
    public bool Adjust(Account account, int amount, string? reason = null)
    {
        if (amount < 0 && account.Balance < -amount)
            return false;

        var debt = 0;
        if (account.Debt > 0 && amount > 0)
            debt = Math.Min((int) Math.Ceiling(amount * DebtCollection), account.Debt);

        account.Balance += amount - debt;
        account.Debt -= debt;
        if (reason != null)
            account.History.Add((_timing.CurTime, amount, reason));
        return true;
    }

    /// <summary>bank_card_talk(): сообщение владельцу карты.</summary>
    public void CardTalk(Account account, string message)
    {
        var told = new HashSet<EntityUid>();
        foreach (var card in account.Cards.ToList())
        {
            if (TerminatingOrDeleted(card))
            {
                account.Cards.Remove(card);
                continue;
            }

            var holder = card;
            while (_container.TryGetContainingContainer((holder, null, null), out var container))
            {
                holder = container.Owner;
            }

            if (!TryComp<ActorComponent>(holder, out var actor) || !told.Add(holder))
                continue;

            _audio.PlayEntity(CardBeep, Filter.Entities(holder), holder, true);
            var wrapped = Loc.GetString("imperial-bank-card-talk", ("message", FormattedMessage.EscapeText(message)));
            _chat.ChatMessageToOne(ChatChannel.Notifications, message, wrapped, EntityUid.Invalid, false, actor.PlayerSession.Channel);
        }
    }

    #endregion

    #region Зарплата

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_idCard.TryFindIdCard(ev.Mob, out var card))
            return;

        // if(equipping.paycheck_department): без бюджета отдела счёт не открывается.
        if (JobPay(ev.JobId).Department == null)
            return;

        var account = Create(Name(ev.Mob), ev.JobId, ev.Station);
        account.Modifier = SpeciesModifier(ev.Mob);
        var bank = EnsureComp<ImperialBankCardComponent>(card);
        bank.AccountId = account.Id;
        Dirty(card, bank);
        account.Cards.Add(card);

        // NT Pay ставится в КПК владельца.
        foreach (var item in _inventory.GetHandOrInventoryEntities(ev.Mob))
        {
            if (!HasComp<CartridgeLoaderComponent>(item) || !_loader.InstallProgram(item, NtPayProgram, deinstallable: false))
                continue;

            if (_loader.TryGetProgram<ImperialNtPayCartridgeComponent>(item, out var appUid, out var app))
            {
                app.AccountId = account.Id;
                account.Cards.Add(appUid.Value);
            }

            break;
        }

        // payday(STARTING_PAYCHECKS, free = TRUE).
        Payday(account, StartingPaychecks, free: true);
    }

    /// <summary>dna.species.payday_modifier.</summary>
    private float SpeciesModifier(EntityUid mob)
    {
        if (!TryComp<Content.Shared.Humanoid.HumanoidProfileComponent>(mob, out var humanoid) || !_proto.TryIndex(Payroll, out var payroll))
            return 1f;

        return payroll.Species.GetValueOrDefault(humanoid.Species, 1f);
    }

    /// <summary>
    /// Get a job. Job reassignment changes your paycheck now: должность берётся с привязанной ID-карты,
    /// поэтому смена должности в консоли ID меняет ставку и бюджет.
    /// </summary>
    private void RefreshJob(Account account)
    {
        foreach (var card in account.Cards)
        {
            if (TerminatingOrDeleted(card) || !TryComp<Content.Shared.Access.Components.IdCardComponent>(card, out var id) || id.JobPrototype is not { } job)
                continue;

            if (job.Id == account.Job)
                return;

            account.Job = job.Id;
            (account.Paycheck, account.Department) = JobPay(job.Id);
            return;
        }
    }

    /// <summary>payday().</summary>
    public bool Payday(Account account, int paychecks, bool free = false)
    {
        if (paychecks <= 0)
            return false;

        RefreshJob(account);
        if (account.Job == null)
            return false;

        // round() в BYOND с одним аргументом округляет вниз.
        var money = (int) Math.Floor(account.Paycheck * account.Modifier * paychecks);
        if (paychecks == 1)
            money = Math.Clamp(money, 0, PaycheckCrew);

        if (free)
        {
            Adjust(account, money, Loc.GetString("imperial-bank-reason-shift-pay"));
            return true;
        }

        var eventName = Loc.GetString("imperial-bank-payday");
        if (account.Department is not { } department
            || account.Station is not { } station
            || !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            CardTalk(account, Loc.GetString("imperial-bank-payday-no-department", ("event", eventName)));
            return false;
        }

        if (_cargo.GetBalanceFromAccount((station, bank), department) < money)
        {
            CardTalk(account, Loc.GetString("imperial-bank-payday-no-funds", ("event", eventName)));
            return false;
        }

        _cargo.UpdateBankAccount((station, bank), -money, department);
        Adjust(account, money, Loc.GetString("imperial-bank-reason-salary"));
        CardTalk(account, Loc.GetString("imperial-bank-payday-success", ("event", eventName), ("balance", account.Balance)));
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_accounts.Count == 0)
            return;

        var now = _timing.CurTime;
        _nextPayday ??= now + PaydayInterval;
        if (now < _nextPayday)
            return;

        _nextPayday = now + PaydayInterval;
        foreach (var account in _accounts.Values.ToList())
        {
            Payday(account, 1);
        }
    }

    #endregion

    #region Карта

    private void OnExamined(Entity<ImperialBankCardComponent> ent, ref ExaminedEvent args)
    {
        if (Get(ent.Comp.AccountId) is not { } account)
            return;

        args.PushMarkup(Loc.GetString("imperial-bank-card-examine", ("id", account.Id), ("holder", account.Holder)));
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("imperial-bank-card-examine-balance", ("balance", account.Balance)));
        if (account.Debt > 0)
            args.PushMarkup(Loc.GetString("imperial-bank-card-examine-debt", ("debt", account.Debt)));
    }

    /// <summary>Снятие наличных в банкомате.</summary>
    public bool Withdraw(Account account, EntityUid user, int amount)
    {
        if (amount <= 0 || !Adjust(account, -amount, Loc.GetString("imperial-bank-reason-withdraw")))
            return false;

        var cash = _stack.SpawnAtPosition(amount, Credit, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, cash);
        return true;
    }

    /// <summary>Внесение наличных в банкомате: вся пачка кредитов уходит на счёт.</summary>
    public bool Deposit(Account account, EntityUid cash)
    {
        if (!TryComp<StackComponent>(cash, out var stack) || stack.StackTypeId != Credit)
            return false;

        var amount = stack.Count;
        Adjust(account, amount, Loc.GetString("imperial-bank-reason-deposit"));
        QueueDel(cash);
        CardTalk(account, Loc.GetString("imperial-bank-deposit", ("amount", amount), ("balance", account.Balance)));
        return true;
    }

    /// <summary>Перевод другому счёту (NT Pay).</summary>
    public bool Transfer(Account from, Account to, int amount)
    {
        if (amount <= 0 || from == to || from.Balance < amount)
            return false;

        Adjust(from, -amount, Loc.GetString("imperial-bank-reason-transfer-to", ("holder", to.Holder)));
        Adjust(to, amount, Loc.GetString("imperial-bank-reason-transfer-from", ("holder", from.Holder)));
        CardTalk(to, Loc.GetString("imperial-bank-transfer-received", ("amount", amount), ("holder", from.Holder), ("balance", to.Balance)));
        return true;
    }

    #endregion
}
