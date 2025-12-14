using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Imperial.ImperialLightning;
using Content.Server.Imperial.Medieval.Magic.MedievalHomingProjectile;
using Content.Server.Imperial.MouseInput;
using Content.Server.Imperial.TargetOverlay;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Prometheus;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// Server part of the <see cref="SharedMedievalMagicSystem" />
/// <para>
/// Responsible for the words spoken when casting spells and for the spawn of the projectile
/// </para>
/// </summary>
public sealed partial class MedievalMagicSystem : SharedMedievalMagicSystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly MouseInputSystem _mouseInputSystem = default!;
    [Dependency] private readonly MedievalHomingProjectileSystem _homingProjectileSystem = default!;
    [Dependency] private readonly TargetOverlaySystem _targetOverlaySystem = default!;
    [Dependency] private readonly ImperialLightningSystem _lightningSystem = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly Gauge SpellCastedMetrics = Metrics.CreateGauge(
        "imperial_medieval_spell_casted",
        "Dictionary of casting spells"
    );
    private static readonly Gauge SpellSuccessCastedMetrics = Metrics.CreateGauge(
        "imperial_medieval_spell_success_casted",
        "Dictionary of casting spells"
    );


    public override void Initialize()
    {
        base.Initialize();


        InitializeTargetSpells();
        InitializeInstantSpells();
        InitializeEntityAimingSpells();
    }

    TimeSpan StartTime = TimeSpan.FromSeconds(0f);
    TimeSpan EndTime = TimeSpan.FromSeconds(0f);
    TimeSpan ReloadTime = TimeSpan.FromSeconds(0.25f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime > EndTime)
        {
            EndTime = _timing.CurTime + ReloadTime;

            var enumerator = EntityQueryEnumerator<MedievalSpellCasterComponent>();

            while (enumerator.MoveNext(out var uid, out var component))
            {
                foreach (var speakPoint in component.SpellWordsStack.ToList())
                {
                    if (speakPoint.Item1 > _timing.CurTime) continue;

                    var spellSpeech = speakPoint.Item2;

                    var ev = new MedievalSpeakSpellEvent(uid, spellSpeech.Speech);
                    RaiseLocalEvent(ref ev);

                    component.SpellWordsStack.Remove(speakPoint);
                    Dirty(uid, component);

                    if (ev.Cancelled) continue;

                    _chatSystem.TrySendInGameICMessage(
                        uid,
                        Loc.GetString(spellSpeech.Speech),
                        TransformToChatEnum(spellSpeech.SpeechType),
                        spellSpeech.HideChat,
                        color: spellSpeech.Color,
                        language: _proto.Index(SharedLanguageSystem.Universal)
                    );
                }
            }
        }
    }

    protected override void OnSpellDoAfterCast(EntityUid uid, MedievalSpellCasterComponent component, MedievalSpellDoAfterEvent args)
    {
        base.OnSpellDoAfterCast(uid, component, args);

        var spellData = GetSpellData(args);
        //SpellCastedMetrics.WithLabels(MetaData(GetEntity(spellData.Action)).EntityName).Inc();
    }

    protected override void CastSpell(MedievalSpellDoAfterEvent args)
    {
        base.CastSpell(args);

        var spellData = GetSpellData(args);
        //SpellSuccessCastedMetrics.WithLabels(MetaData(GetEntity(spellData.Action)).EntityName).Inc();
    }

    #region Helpers

    protected override void AddToStack(EntityUid uid, Dictionary<TimeSpan, MedievalSpellSpeech>? el)
    {
        if (el == null) return;

        var casterComponent = EnsureComp<MedievalSpellCasterComponent>(uid);

        foreach (var speakPoint in el)
        {
            var speakPointTime = _timing.CurTime + speakPoint.Key;

            casterComponent.SpellWordsStack.Add((speakPointTime, speakPoint.Value));
        }

        Dirty(uid, casterComponent);
    }

    private InGameICChatType TransformToChatEnum(SpellSpeechType? speechType)
    {
        return speechType switch
        {
            SpellSpeechType.Speak => InGameICChatType.Speak,
            SpellSpeechType.Emote => InGameICChatType.Emote,
            SpellSpeechType.Whisper => InGameICChatType.Whisper,
            _ => InGameICChatType.Speak
        };
    }

    #endregion
}
