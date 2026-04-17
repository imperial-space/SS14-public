using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(TerrorSpiderRuleSystem))]
public sealed partial class TerrorSpiderRuleComponent : Component
{
    [DataField]
    public EntProtoId QueenPrototype = "MobQueenSpider";
}