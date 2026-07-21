using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly IBanManager _ban = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
        InitializeStrength();
        InitializeAgility();
        InitializeVitality();
        InitializeIntelligence();

        SubscribeLocalEvent<SkillsComponent, SkillLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeNetworkEvent<SetSkillLevelMessage>(OnSetSkillLevel);
    }
    public bool TryGetSkill(EntityUid uid, string skillId, out int level)
    {
        level = 0;
        if (!TryComp<SkillsComponent>(uid, out var skills))
            return false;

        return skills.Levels.TryGetValue(skillId, out level);
    }

    private void OnLevelChanged(EntityUid uid, SkillsComponent comp, ref SkillLevelChangedEvent args)
    {
        switch (args.Id)
        {
            case VitalityId:
                VitalityLevelSet(uid, args.Level, args.OldLevel);
                break;
            case IntelligenceId:
                IntelligenceLevelSet(uid, args.Level, args.OldLevel);
                break;
            case AgilityId:
                AgilityLevelSet(uid, args.Level, args.OldLevel);
                break;
            case StrengthId:
                StrengthLevelSet(uid, args.Level, args.OldLevel);
                break;
            default:
                break;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_proto.TryIndex<JobPrototype>(args.JobId ?? string.Empty, out var protoJob) ||
            !protoJob.ApplySkills)
            return;

        var sum = Points + 1;
        foreach (var skill in args.Profile.Skills)
        {
            sum += GetPointsCost(skill.Value);
        }
        if (sum < 0)
        {
            _ban.CreateServerBan(args.Player.UserId, args.Player.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, Loc.GetString("skills-autoban-points"));
            return;
        }

        SetSkills(args.Mob, args.Profile.Skills);
    }

    private void OnSetSkillLevel(SetSkillLevelMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
        {
            _ban.CreateServerBan(args.SenderSession.UserId, args.SenderSession.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, Loc.GetString("skills-autoban-set"));
            return;
        }
        var uid = GetEntity(msg.Target);
        var comp = EnsureComp<SkillsComponent>(uid);

        var dict = comp.Levels;
        dict[msg.Skill] = msg.Level;
        SetSkills(uid, dict);
    }

    public void SetSkills(EntityUid uid, Dictionary<string, int> skills)
    {
        var comp = EnsureComp<SkillsComponent>(uid);

        foreach (var skill in _proto.EnumeratePrototypes<SkillPrototype>())
        {
            var oldLevel = comp.Levels.GetValueOrDefault(skill.ID, 10);

            comp.Levels[skill.ID] = Math.Clamp(skills.GetValueOrDefault(skill.ID, 10), 1, 20);
            var ev = new SkillLevelChangedEvent(skill.ID, comp.Levels[skill.ID], oldLevel);
            RaiseLocalEvent(uid, ref ev);
        }

        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1f);

        UpdateAgility(frameTime);
        UpdateVitality(frameTime);
    }
}
