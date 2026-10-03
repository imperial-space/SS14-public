using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Medical.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// ДНК-инфузер (dna_infuser из SS13): пациент в капсуле получает черты мёртвого существа, лежащего рядом.
/// Каждая инфузия даёт стадию набора, после порога — бонус набора.
/// </summary>
public sealed class DnaInfuserSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier InfuseSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaInfuserComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DnaInfuserComponent, DnaInfuseDoAfterEvent>(OnInfuse);
    }

    private EntityUid? GetOccupant(EntityUid infuser)
    {
        return TryComp<MedicalScannerComponent>(infuser, out var scanner) ? scanner.BodyContainer.ContainedEntity : null;
    }

    /// <summary>Мёртвое существо-источник рядом с машиной и подходящий ему набор.</summary>
    private (EntityUid Source, GeneticInfusionPrototype Infusion)? FindSource(Entity<DnaInfuserComponent> infuser)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(infuser).Coordinates, infuser.Comp.SourceRange))
        {
            if (!_mobState.IsDead(mob, mob.Comp) || MetaData(mob).EntityPrototype?.ID is not { } protoId)
                continue;

            var infusion = _proto.EnumeratePrototypes<GeneticInfusionPrototype>()
                .FirstOrDefault(i => i.Sources.Any(s => s.Id == protoId));
            if (infusion != null)
                return (mob.Owner, infusion);
        }

        return null;
    }

    private void OnGetVerbs(Entity<DnaInfuserComponent> infuser, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetOccupant(infuser) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dna-infuser-verb"),
            Act = () => StartInfusion(infuser, user),
        });
    }

    private void StartInfusion(Entity<DnaInfuserComponent> infuser, EntityUid user)
    {
        if (GetOccupant(infuser) is not { } occupant || _mobState.IsDead(occupant))
        {
            _popup.PopupEntity(Loc.GetString("dna-infuser-no-occupant"), infuser, user);
            return;
        }

        if (FindSource(infuser) == null)
        {
            _popup.PopupEntity(Loc.GetString("dna-infuser-no-source"), infuser, user);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, infuser.Comp.InfuseTime, new DnaInfuseDoAfterEvent(), infuser, infuser)
        {
            BreakOnMove = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("dna-infuser-start"), infuser, PopupType.MediumCaution);
    }

    private void OnInfuse(Entity<DnaInfuserComponent> infuser, ref DnaInfuseDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (GetOccupant(infuser) is not { } occupant || FindSource(infuser) is not { } found)
            return;

        var (source, infusion) = found;
        QueueDel(source);

        var infusions = EnsureComp<GeneticInfusionsComponent>(occupant);
        var count = infusions.Counts.GetValueOrDefault(infusion.ID) + 1;
        infusions.Counts[infusion.ID] = count;

        if (count <= infusion.Stages.Count)
            AddMissing(occupant, infusion.Stages[count - 1]);

        if (count == infusion.Threshold)
        {
            AddMissing(occupant, infusion.Bonus);
            if (infusion.BonusText is { } text)
                _popup.PopupEntity(Loc.GetString(text), occupant, occupant, PopupType.Large);
        }

        _damageable.TryChangeDamage(occupant, new DamageSpecifier { DamageDict = { ["Cellular"] = 10 } }, true);
        _audio.PlayPvs(InfuseSound, infuser);
        _popup.PopupEntity(Loc.GetString("dna-infuser-done", ("set", Loc.GetString(infusion.Name)),
            ("count", count), ("threshold", infusion.Threshold)), occupant, occupant, PopupType.MediumCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} infused {ToPrettyString(occupant):target} with {infusion.ID} ({count}/{infusion.Threshold})");
    }

    private void AddMissing(EntityUid uid, ComponentRegistry registry)
    {
        var missing = new ComponentRegistry();
        foreach (var (name, entry) in registry)
        {
            if (!HasComp(uid, _compFactory.GetRegistration(name).Type))
                missing[name] = entry;
        }

        EntityManager.AddComponents(uid, missing, removeExisting: false);
    }
}
