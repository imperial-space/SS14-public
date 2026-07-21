using System.Windows.Input;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Construction;
using Content.Shared.Imperial.Medieval.Illitid;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string IntelligenceId = "Intelligence";

    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, GetConstructionSpeedModifiersEvent>(OnGetConstructionSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, PaperWriteAttemptEvent>(OnCanWrite);
        SubscribeLocalEvent<SkillsComponent, GetVerbsEvent<ExamineVerb>>(OnSkillsExamined);
    }

    private void OnGetConstructionSpeedModifiers(EntityUid uid, SkillsComponent comp, ref GetConstructionSpeedModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveConstructionSpeedModifier"] : proto.Modifiers["NegativeConstructionSpeedModifier"]) * diff;
    }

    private void OnCanWrite(EntityUid uid, SkillsComponent comp, ref PaperWriteAttemptEvent args)
    {
        if (CanRead(uid))
            return;

        args.Cancelled = true;
        args.FailReason = "Вы слишком глупы.";
    }


    private void OnSkillsExamined(EntityUid uid, SkillsComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var user = args.User;
        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

        if (uid != user)
        {
            var verbDiff = new ExamineVerb()
            {
                Text = Loc.GetString("examine-skills-differance"),
                Category = VerbCategory.Examine,
                Disabled = !detailsRange,

                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/plus.svg.192dpi.png")),

                Act = () =>
                {
                    var message = new FormattedMessage();

                    foreach (var level in component.Levels)
                    {
                        message.AddText($"{Loc.GetString($"skill-{level.Key.ToLower()}-name")}: ");

                        string hex = GetColorForDiff(0);

                        Dictionary<string, int> levels = new()
                        {
                            ["Agility"] = 10,
                            ["Strength"] = 10,
                            ["Vitality"] = 10,
                            ["Endurance"] = 10,
                            ["Intelligence"] = 10
                        };

                        if (TryComp<SkillsComponent>(user, out var examinerComp))
                            levels = examinerComp.Levels;

                        var diff = component.Levels[level.Key] - levels[level.Key];

                        hex = GetColorForDiff(diff);
                        message.PushColor(Color.FromHex(hex));
                        message.AddText(Loc.GetString(GetTextForDiff(diff)));
                        message.Pop();
                        message.AddText($"\n");
                    }

                    _examineSystem.SendExamineTooltip(user, uid, message, false, false);
                }
            };
            args.Verbs.Add(verbDiff);
        }

        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var (_, self) = GetSkill(args.User, IntelligenceId);
        var (_, otherLevel) = GetSkill(uid, IntelligenceId);

        // в идеале конечно для резонатов перенести в их систему, ведь иначе будет путаница, но
        if ((self >= 20 || HasComp<IllitidComponent>(args.User)) && otherLevel < 14)
        {
            var verb = new ExamineVerb
            {
                Act = () =>
                {
                    if (_netMan.IsClient)
                        return;

                    var ev = new GetEnteredChatMessageMessage(GetNetEntity(uid), GetNetEntity(args.User));
                    RaiseNetworkEvent(ev, session);
                },
                Text = Loc.GetString("Заглянуть в голову"),
                Category = VerbCategory.Examine,
                Disabled = !args.CanAccess,
                Message = args.CanAccess ? null : Loc.GetString("detail-examinable-verb-disabled"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Imperial/Medieval/Interface/Brain.png"))
            };
            args.Verbs.Add(verb);
        }
    }

    public bool CanRead(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level < 5)
            return false;

        return true;
    }
    public bool CanOpenDoorKey(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level > 5)
            return false;

        return true;
    }

    public bool IntelligenceMin(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level == 1)
            return true;

        return false;
    }

    private string GetColorForDiff(int diff)
    {
        return diff switch
        {
            <= -10 => "#0dff00",
            <= -7 => "#42c0fe",
            <= -3 => "#7afcd5",
            <= 2 => "#d1d1d1",
            >= 10 => "#ff0000",
            >= 7 => "#ff9100",
            >= 3 => "#ffea00"
        };
    }
    private string GetTextForDiff(int diff)
    {
        return diff switch
        {
            <= -10 => "examine-skills-much-lower",
            <= -7 => "examine-skills-lower",
            <= -3 => "examine-skills-slightly-lower",
            <= 2 => "examine-skills-similar",
            >= 10 => "examine-skills-much-higher",
            >= 7 => "examine-skills-higher",
            >= 3 => "examine-skills-slightly-higher"
        };
    }
}
