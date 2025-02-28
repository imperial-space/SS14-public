using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Humanoid;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Examine;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Imperial.IdentityFaction.Components;


namespace Content.Server.IdentityManagement;

/// <summary>
///     Responsible for updating the identity of an entity on init or clothing equip/unequip.
/// </summary>
public sealed class IdentitySystem : SharedIdentitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityComponent, ExaminedEvent>(UpdateIdentityInfo); // Imperial Spellward Identity
        SubscribeLocalEvent<IdentityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IdentityComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs); // Imperial Spellward Identity
    }

    private void OnGetAlternativeVerbs(EntityUid uid, IdentityComponent comp, GetVerbsEvent<AlternativeVerb> verb) // Imperial Spellward Identity
    {
        if (EnsureComp<IdentityComponent>(verb.User).ListEntities.Contains(verb.Target) || verb.User == verb.Target) return;
        verb.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                OnDate(verb.User, verb.Target);
            },
            Category = VerbCategory.Examine,
            Text = Loc.GetString("dateVerbSpellward"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/Spellward/date.rsi"), "date")
        });
    }
    private void OnDate(EntityUid uid, EntityUid second)
    {
        EnsureComp<IdentityComponent>(uid).ListEntities.Add(second);
    }
    /*public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var ent in _queuedIdentityUpdates)
        {
            if (!TryComp<IdentityComponent>(ent, out var identity))
                continue;

            UpdateIdentityInfo(ent, identity);
        }

        _queuedIdentityUpdates.Clear();
    }*/

    // This is where the magic happens
    private void OnMapInit(EntityUid uid, IdentityComponent component, MapInitEvent args)
    {
        var ident = Spawn(null, Transform(uid).Coordinates);

        _metaData.SetEntityName(ident, "identity");
        _container.Insert(ident, component.IdentityEntitySlot);
    }


    #region Private API

    /// <summary>
    ///     Updates the metadata name for the id(entity) from the current state of the character.
    /// </summary>
    private void UpdateIdentityInfo(EntityUid uid, IdentityComponent identity, ExaminedEvent ev)
    {
        if (identity.IdentityEntitySlot.ContainedEntity is not { } ident)
            return;

        var representation = GetIdentityRepresentation(uid);
        var name = GetIdentityName(uid, identity, representation, ev.Examiner);

        // Clone the old entity's grammar to the identity entity, for loc purposes.
        if (TryComp<GrammarComponent>(uid, out var grammar))
        {
            var identityGrammar = EnsureComp<GrammarComponent>(ident);
            identityGrammar.Attributes.Clear();

            foreach (var (k, v) in grammar.Attributes)
            {
                identityGrammar.Attributes.Add(k, v);
            }

            // If presumed name is null and we're using that, we set proper noun to be false ("the old woman")
            if (name != representation.TrueName && representation.PresumedName == null)
                identityGrammar.ProperNoun = false;
        }

        if (name == Name(ident))
            return;

        _metaData.SetEntityName(ident, name);

        _adminLog.Add(LogType.Identity, LogImpact.Medium, $"{ToPrettyString(uid)} changed identity to {name}");
        var identityChangedEvent = new IdentityChangedEvent(uid, ident);
        RaiseLocalEvent(uid, ref identityChangedEvent);
        SetIdentityCriminalIcon(uid);
    }

    public string GetIdentityName(EntityUid target, IdentityComponent comp, IdentityRepresentation representation, EntityUid? examiner) // Imperial Spellward Identity
    {
        if (examiner is null) return representation.ToStringUnknown();
        if (comp.ListEntities.Contains(examiner.Value) || EnsureComp<IdentityFactionComponent>(target).Faction == EnsureComp<IdentityFactionComponent>(examiner.Value).Faction || target == examiner)
        {
            var eve = new SeeIdentityAttemptEvent();

            RaiseLocalEvent(target, eve);
            return representation.ToStringKnown(!eve.Cancelled);
        }
        return representation.ToStringUnknown();
    }

    /// <summary>
    ///     When the identity of a person is changed, searches the criminal records to see if the name of the new identity
    ///     has a record. If the new name has a criminal status attached to it, the person will get the criminal status
    ///     until they change identity again.
    /// </summary>
    private void SetIdentityCriminalIcon(EntityUid uid)
    {
        _criminalRecordsConsole.CheckNewIdentity(uid);
    }

    /// <summary>
    ///     Gets an 'identity representation' of an entity, with their true name being the entity name
    ///     and their 'presumed name' and 'presumed job' being the name/job on their ID card, if they have one.
    /// </summary>

    /*
    private IdentityRepresentation GetIdentityRepresentation(EntityUid target,
        InventoryComponent? inventory=null,
        HumanoidAppearanceComponent? appearance=null)
    {
        int age = 18;
        Gender gender = Gender.Epicene;
        string species = SharedHumanoidAppearanceSystem.DefaultSpecies;

        // Always use their actual age and gender, since that can't really be changed by an ID.
        if (Resolve(target, ref appearance, false))
        {
            gender = appearance.Gender;
            age = appearance.Age;
            species = appearance.Species;
        }

        var ageString = _humanoid.GetAgeRepresentation(species, age);
        var trueName = Name(target);
        if (!Resolve(target, ref inventory, false))
            return new(trueName, gender, ageString, string.Empty);
        string? presumedJob = null;
        string? presumedName = null;
        // Get their name and job from their ID for their presumed name.
            if (_idCard.TryFindIdCard(target, out var id))
            {
                presumedName = string.IsNullOrWhiteSpace(id.Comp.FullName) ? null : id.Comp.FullName;
                presumedJob = id.Comp.LocalizedJobTitle?.ToLowerInvariant();
            }

        // If it didn't find a job, that's fine.
        return new(trueName, gender, ageString, presumedName, presumedJob);
    }
    Wizard's way*/

    public IdentityRepresentation GetIdentityRepresentation(EntityUid target,
        HumanoidAppearanceComponent? appearance = null) // Imperial Spellward Identity
    {
        var age = 18;
        var gender = Gender.Epicene;
        var species = SharedHumanoidAppearanceSystem.DefaultSpecies.ToString();

        if (Resolve(target, ref appearance, false))
        {
            gender = appearance.Gender;
            age = appearance.Age;
            species = appearance.Species;
        }

        var ageString = _humanoid.GetAgeRepresentation(species, age);
        var trueName = Name(target);
        return new(trueName, gender, ageString, null, null);
    }
    #endregion
}
