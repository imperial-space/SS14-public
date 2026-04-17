using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Компонент game rule для крови культа Нар'Си.
/// </summary>
[RegisterComponent, Access(typeof(CultRuleSystem))]
public sealed partial class CultRuleComponent : Component
{
    // ─── Победные условия ───

    /// <summary>
    /// Умы, выбранные Нар'Си как обязательные жертвы.
    /// </summary>
    [DataField]
    public List<EntityUid> SacrificeTargets = new();

    /// <summary>
    /// Уже принесённые жертвы из списка SacrificeTargets.
    /// </summary>
    [DataField]
    public List<EntityUid> CompletedSacrificeTargets = new();

    /// <summary>
    /// Количество жертв, необходимое для начала финального ритуала.
    /// </summary>
    [DataField]
    public int RequiredSacrifices = 2;

    /// <summary>
    /// Выбраны ли уже цели жертвоприношения для этого раунда.
    /// </summary>
    [DataField]
    public bool TargetsInitialized;

    /// <summary>
    /// Было ли уже отправлено сообщение об ослаблении вуали.
    /// </summary>
    [DataField]
    public bool VeilWeakensAnnounced;

    /// <summary>
    /// Было ли уже отправлено сообщение о полном раскрытии культа.
    /// </summary>
    [DataField]
    public bool VeilBrokenAnnounced;

    /// <summary>
    /// Выбранные на раунд маяки, у которых можно начать ритуал Нар'Си.
    /// </summary>
    [DataField]
    public List<EntityUid> NarSieBeaconTargets = new();

    /// <summary>
    /// Отображаемые названия выбранных маяков Нар'Си.
    /// </summary>
    [DataField]
    public List<string> NarSieBeaconLabels = new();

    /// <summary>
    /// Был ли Нар'Си уже вызван (победа культа).
    /// </summary>
    public bool NarSieSummoned;

    // ─── Стартовое снаряжение ───

    /// <summary>
    /// Прототип предмета, который выдаётся каждому начальному культисту.
    /// </summary>
    [DataField]
    public EntProtoId StartingDagger = "CultDagger";

    /// <summary>
    /// Количество рунного металла у каждого начального культиста.
    /// </summary>
    [DataField]
    public int StartingRunedMetal = 10;
}
