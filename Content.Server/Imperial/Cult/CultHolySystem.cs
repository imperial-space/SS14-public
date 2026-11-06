using Content.Server.Chat.Systems;
using Content.Server.Imperial.Chaplain;
using Content.Server.Imperial.NullRod;
using Content.Shared.Chat;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Культ против святого: святая вода, обряды священника и нуллрод.
/// Священник поднимает события, а что они делают с культом, решается здесь.
/// </summary>
public sealed class CultHolySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private static readonly TimeSpan SpellClearCooldown = TimeSpan.FromSeconds(3);
    private static readonly EntProtoId RuneDestructionEffect = "CultRuneDestructionEffect";

    private static readonly string[] CultPhrases =
    {
        "Av'te Nar'Sie", "Pa'lid Mors", "INO INO ORA ANA", "SAT ANA!",
        "Daim'niodeis Arc'iai Le'eones", "R'ge Na'sie", "Diabo us Vo'iscum", "Eld' Mon Nobis",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultistComponent, ImperialUnholyCheckEvent>(OnUnholyCheck);
        SubscribeLocalEvent<CultistComponent, ImperialHolyWaterStartedEvent>(OnHolyWaterStarted);
        SubscribeLocalEvent<CultistComponent, ImperialHolyWaterTickEvent>(OnHolyWaterTick);
        SubscribeLocalEvent<CultistComponent, ImperialHolyWaterPurgedEvent>(OnHolyWaterPurged);
        SubscribeLocalEvent<ImperialHolyRevealEvent>(OnHolyReveal);
        SubscribeLocalEvent<CultRuneComponent, ImperialNullRodEraseAttemptEvent>(OnRuneEraseAttempt);
        SubscribeLocalEvent<CultRuneComponent, ImperialNullRodErasedEvent>(OnRuneErased);
    }

    private void OnUnholyCheck(Entity<CultistComponent> ent, ref ImperialUnholyCheckEvent args)
    {
        args.Unholy = true;
    }

    private void OnHolyWaterStarted(Entity<CultistComponent> ent, ref ImperialHolyWaterStartedEvent args)
    {
        _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-start"), ent, ent, PopupType.LargeCaution);
    }

    /// <summary>Святая вода стирает подготовленные заклинания крови, а набравшись — вызывает выкрики и припадки.</summary>
    private void OnHolyWaterTick(Entity<CultistComponent> ent, ref ImperialHolyWaterTickEvent args)
    {
        var holy = args.Holy.Comp;
        if (args.Now >= holy.NextSpellClear && _cult.ClearPreparedSpells(ent))
        {
            _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-spells"), ent, ent, PopupType.LargeCaution);
            holy.NextSpellClear = args.Now + SpellClearCooldown;
        }

        if (!args.Stuttering || !_random.Prob(holy.TickChance))
            return;

        _chat.TrySendInGameICMessage(ent, _random.Pick(CultPhrases), InGameICChatType.Speak, false, ignoreActionBlocker: true);
        if (!_random.Prob(holy.TickChance))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-seizure", ("target", ent)), ent, PopupType.LargeCaution);
        _stun.TryUpdateParalyzeDuration(ent, holy.TickDuration);
    }

    /// <summary>Минута святой воды очищает от власти Нар'Си.</summary>
    private void OnHolyWaterPurged(Entity<CultistComponent> ent, ref ImperialHolyWaterPurgedEvent args)
    {
        _cult.RemoveCultist(ent);
        _stun.TryUpdateParalyzeDuration(ent, args.Holy.Comp.TickDuration2);
        _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-purged"), ent, ent, PopupType.LargeCaution);
    }

    private void OnHolyReveal(ref ImperialHolyRevealEvent args)
    {
        args.Revealed += _cult.RevealConcealed(args.Center, args.Range);
    }

    private void OnRuneEraseAttempt(Entity<CultRuneComponent> ent, ref ImperialNullRodEraseAttemptEvent args)
    {
        args.Erasable = true;
    }

    /// <summary>Как при обычном разрушении руны: эффект, затем удаление.</summary>
    private void OnRuneErased(Entity<CultRuneComponent> ent, ref ImperialNullRodErasedEvent args)
    {
        if (ent.Comp.DestructionState != null)
            Spawn(RuneDestructionEffect, Transform(ent).Coordinates);
    }
}
