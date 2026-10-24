using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.CombatMode;
using Content.Shared.Construction.Components;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Stack;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Wires;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.MachineParts;

/// <summary>
/// Быстрый заменитель деталей (RPED): меняет детали машины на лучшие из своего хранилища (exchange_parts)
/// и заполняет рамку машины. Блюспейс-версия работает на расстоянии и с закрытой панелью.
/// </summary>
public sealed class ImperialPartReplacerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedWiresSystem _wires = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MachineFrameSystem _frame = default!;
    [Dependency] private readonly ImperialMachinePartsSystem _parts = default!;

    private static readonly SoundPathSpecifier RpedSound = new("/Audio/Imperial/MachineParts/rped.ogg");
    private static readonly SoundPathSpecifier PshoomSound = new("/Audio/Imperial/MachineParts/pshoom.ogg");
    private static readonly SoundPathSpecifier PshoomRareSound = new("/Audio/Imperial/MachineParts/pshoom_2.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialPartReplacerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private BaseContainer? Storage(EntityUid rped)
    {
        return _container.TryGetContainer(rped, StorageComponent.ContainerId, out var container) ? container : null;
    }

    /// <summary>get_sorted_parts(): детали по убыванию уровня.</summary>
    private List<(EntityUid Uid, ImperialStockPartComponent Part)> SortedParts(EntityUid rped)
    {
        var result = new List<(EntityUid, ImperialStockPartComponent)>();
        if (Storage(rped) is not { } storage)
            return result;

        foreach (var uid in storage.ContainedEntities)
        {
            if (TryComp<ImperialStockPartComponent>(uid, out var part))
                result.Add((uid, part));
        }

        return result.OrderByDescending(p => p.Item2.Tier).ToList();
    }

    private void OnAfterInteract(Entity<ImperialPartReplacerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || _combat.IsInCombatMode(args.User))
            return;

        if (!args.CanReach && !ent.Comp.Bluespace)
            return;

        if (TryComp<MachineFrameComponent>(target, out var frame))
        {
            if (!args.CanReach)
                return;

            args.Handled = true;
            if (FillFrame(ent, (target, frame)))
                PlayEffect(ent);
            return;
        }

        if (!HasComp<MachineComponent>(target))
            return;

        args.Handled = true;
        ExchangeParts(ent, target, args.User);
    }

    /// <summary>exchange_parts(): каждую деталь машины заменить лучшей того же типа.</summary>
    private void ExchangeParts(Entity<ImperialPartReplacerComponent> rped, EntityUid machine, EntityUid user)
    {
        var panelOpen = !HasComp<WiresPanelComponent>(machine) || _wires.IsPanelOpen(machine);
        if (!panelOpen && !rped.Comp.Bluespace || rped.Comp.Bluespace)
            DisplayParts(machine, user);

        if (!panelOpen && !rped.Comp.Bluespace)
            return;

        if (!_container.TryGetContainer(machine, MachineFrameComponent.PartContainerName, out var machineParts) || Storage(rped) is not { } storage)
            return;

        var available = SortedParts(rped);
        if (available.Count == 0)
            return;

        var exchanged = false;
        foreach (var installed in machineParts.ContainedEntities.ToList())
        {
            if (available.Count == 0)
                break;

            if (TryComp<ImperialStockPartComponent>(installed, out var current))
            {
                var better = available.FirstOrDefault(p => p.Part.Type == current.Type && p.Part.Tier > current.Tier);
                if (better.Uid == default)
                    continue;

                available.Remove(better);
                Swap(machine, machineParts, storage, installed, better.Uid);
                exchanged = true;
                continue;
            }

            // Универсальные детали меняются на детали нужных машине типов (req_components платы TG).
            if (!TryComp<StackComponent>(installed, out var stack) || stack.StackTypeId != ImperialMachinePartsSystem.GenericStack)
                continue;

            var typed = machineParts.ContainedEntities.Count(HasComp<ImperialStockPartComponent>);
            var replacements = PickByProfile(available, _parts.GetProfile(Prototype(machine)), stack.Count, typed);
            var count = replacements.Count;
            if (count == 0)
                continue;

            EntityUid toStore = installed;
            if (count < stack.Count)
            {
                if (_stack.Split((installed, stack), count, Transform(machine).Coordinates) is not { } split)
                    continue;
                toStore = split;
            }
            else
            {
                _container.Remove(installed, machineParts);
            }

            if (!_container.Insert(toStore, storage))
                _container.TryRemoveFromContainer(toStore);

            foreach (var replacement in replacements)
            {
                _container.Remove(replacement.Uid, storage);
                _container.Insert(replacement.Uid, machineParts);
            }

            exchanged = true;
        }

        if (!exchanged)
            return;

        _parts.Refresh(machine);
        PlayEffect(rped);
        _popup.PopupEntity(Loc.GetString("imperial-rped-exchanged"), machine, user);
    }

    /// <summary>
    /// Выбрать до count лучших деталей по профилю машины (типы по кругу, начиная с offset).
    /// Без профиля — любые лучшие. Выбранные детали убираются из available.
    /// </summary>
    private static List<(EntityUid Uid, ImperialStockPartComponent Part)> PickByProfile(
        List<(EntityUid Uid, ImperialStockPartComponent Part)> available, ImperialStockPartType[] profile, int count, int offset)
    {
        var result = new List<(EntityUid, ImperialStockPartComponent)>();
        for (var i = 0; i < count && available.Count > 0; i++)
        {
            var index = profile.Length == 0
                ? 0
                : available.FindIndex(p => p.Part.Type == profile[(offset + i) % profile.Length]);
            if (index < 0)
                continue;

            result.Add(available[index]);
            available.RemoveAt(index);
        }

        return result;
    }

    private void Swap(EntityUid machine, BaseContainer machineParts, BaseContainer storage, EntityUid oldPart, EntityUid newPart)
    {
        _container.Remove(oldPart, machineParts);
        _container.Remove(newPart, storage);
        _container.Insert(newPart, machineParts);
        if (!_container.Insert(oldPart, storage))
            _container.TryRemoveFromContainer(oldPart);
    }

    /// <summary>display_parts(): список деталей машины.</summary>
    private void DisplayParts(EntityUid machine, EntityUid user)
    {
        var ratings = _parts.GetRatings(machine);
        var lines = new List<string>();
        foreach (var (type, tiers) in ratings.Tiers)
        {
            foreach (var group in tiers.GroupBy(t => t).OrderByDescending(g => g.Key))
            {
                lines.Add(Loc.GetString("imperial-rped-part-line", ("count", group.Count()),
                    ("name", Loc.GetString($"imperial-stock-part-type-{type.ToString().ToLowerInvariant()}")), ("tier", group.Key)));
            }
        }

        if (ratings.Generic > 0)
            lines.Add(Loc.GetString("imperial-rped-generic-line", ("count", ratings.Generic)));

        var text = lines.Count == 0 ? Loc.GetString("imperial-rped-no-parts") : string.Join("\n", lines);
        _popup.PopupEntity(Loc.GetString("imperial-rped-parts-header") + "\n" + text, machine, user);
    }

    /// <summary>RPED на рамке: вставить лучшие детали и универсальные детали в счёт требований платы.</summary>
    private bool FillFrame(Entity<ImperialPartReplacerComponent> rped, Entity<MachineFrameComponent> frame)
    {
        if (!frame.Comp.HasBoard || Storage(rped) is not { } storage)
            return false;

        var inserted = false;
        var available = SortedParts(rped);

        // Требования по тегам (платы Imperial).
        foreach (var (uid, _) in available.ToList())
        {
            foreach (var (tag, info) in frame.Comp.TagRequirements)
            {
                if (frame.Comp.TagProgress.GetValueOrDefault(tag) >= info.Amount || !HasTag(uid, tag))
                    continue;

                _container.Remove(uid, storage);
                if (_container.Insert(uid, frame.Comp.PartContainer))
                {
                    frame.Comp.TagProgress[tag] = frame.Comp.TagProgress.GetValueOrDefault(tag) + 1;
                    available.RemoveAll(p => p.Uid == uid);
                    inserted = true;
                }

                break;
            }
        }

        // Универсальные детали — по профилю будущей машины.
        if (frame.Comp.MaterialRequirements.TryGetValue(ImperialMachinePartsSystem.GenericStack, out var required))
        {
            var needed = required - frame.Comp.MaterialProgress.GetValueOrDefault(ImperialMachinePartsSystem.GenericStack);
            EntityPrototype? machineProto = null;
            if (frame.Comp.BoardContainer.ContainedEntities.FirstOrDefault() is { Valid: true } board
                && TryComp<MachineBoardComponent>(board, out var boardComp))
                _prototypes.TryIndex(boardComp.Prototype, out machineProto);

            foreach (var (uid, _) in PickByProfile(available, _parts.GetProfile(machineProto), needed, 0))
            {
                if (_parts.TryInsertIntoFrame(frame, uid))
                    inserted = true;
            }
        }

        if (inserted && _frame.IsComplete(frame.Comp))
            _popup.PopupEntity(Loc.GetString("machine-frame-component-on-complete"), frame);

        return inserted;
    }

    private bool HasTag(EntityUid uid, Robust.Shared.Prototypes.ProtoId<Content.Shared.Tag.TagPrototype> tag)
    {
        return EntityManager.System<Content.Shared.Tag.TagSystem>().HasTag(uid, tag);
    }

    private void PlayEffect(Entity<ImperialPartReplacerComponent> rped)
    {
        if (!rped.Comp.Bluespace)
        {
            _audio.PlayPvs(RpedSound, rped);
            return;
        }

        _audio.PlayPvs(_random.Prob(0.01f) ? PshoomRareSound : PshoomSound, rped);
    }
}
