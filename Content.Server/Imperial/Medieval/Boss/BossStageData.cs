using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossStageData
{
    [DataField(required: true)]
    public List<BossAttack> Attacks = new();

    [DataField(required: true)]
    public float StageDelay = 5f;

    [DataField]
    public (int, int) AttacksPerTime = (1, 1);

    [DataField]
    public float Threshold = 100f;

    [DataField]
    public SoundSpecifier? Sound;
}
