using Content.Shared.Imperial.ModularShield;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.ModularShield;

public sealed partial class ImperialModularShieldSystem
{
    private void InitializeConsole()
    {
        SubscribeLocalEvent<ImperialModularShieldConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        Subs.BuiEvents<ImperialModularShieldConsoleComponent>(ImperialModularShieldUiKey.Console, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((uid, comp, _) => UpdateConsoleUi((uid, comp)));
            subs.Event<ImperialModularShieldToggleMessage>(OnConsoleToggle);
            subs.Event<ImperialModularShieldRenameMessage>(OnConsoleRename);
        });
    }

    /// <summary>multitool_act(): загрузить генератор из буфера мультитула.</summary>
    private void OnConsoleInteractUsing(Entity<ImperialModularShieldConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, Pulsing) || !TryComp<ImperialModularShieldBufferComponent>(args.Used, out var buffer))
            return;

        args.Handled = true;
        if (!ent.Comp.Generators.Contains(buffer.Generator) && !TerminatingOrDeleted(buffer.Generator))
            ent.Comp.Generators.Add(buffer.Generator);

        RemComp<ImperialModularShieldBufferComponent>(args.Used);
        _popup.PopupEntity(Loc.GetString("imperial-modular-shield-console-uploaded"), ent, args.User);
        UpdateConsoleUi(ent);
    }

    private Entity<ImperialModularShieldGeneratorComponent>? GetConsoleGenerator(Entity<ImperialModularShieldConsoleComponent> ent, int id)
    {
        ent.Comp.Generators.RemoveAll(g => TerminatingOrDeleted(g));
        if (id < 1 || id > ent.Comp.Generators.Count)
            return null;

        var uid = ent.Comp.Generators[id - 1];
        return TryComp<ImperialModularShieldGeneratorComponent>(uid, out var comp) ? (uid, comp) : null;
    }

    private void OnConsoleToggle(Entity<ImperialModularShieldConsoleComponent> ent, ref ImperialModularShieldToggleMessage args)
    {
        if (GetConsoleGenerator(ent, args.Id) is { } generator)
            ToggleShields(generator);
        UpdateConsoleUi(ent);
    }

    private void OnConsoleRename(Entity<ImperialModularShieldConsoleComponent> ent, ref ImperialModularShieldRenameMessage args)
    {
        var name = args.Name.Trim();
        if (name.Length == 0 || name.Length > 32 || GetConsoleGenerator(ent, args.Id) is not { } generator)
            return;

        generator.Comp.DisplayName = name;
        UpdateConsoleUi(ent);
    }

    private void UpdateConsoleUi(Entity<ImperialModularShieldConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialModularShieldUiKey.Console))
            return;

        ent.Comp.Generators.RemoveAll(g => TerminatingOrDeleted(g));
        var list = new List<ImperialModularShieldConsoleEntry>();
        for (var i = 0; i < ent.Comp.Generators.Count; i++)
        {
            var uid = ent.Comp.Generators[i];
            if (!TryComp<ImperialModularShieldGeneratorComponent>(uid, out var gen))
                continue;

            list.Add(new ImperialModularShieldConsoleEntry
            {
                Id = i + 1,
                Name = gen.DisplayName,
                MaxStrength = gen.MaxStrength,
                CurrentStrength = gen.StoredStrength,
                CurrentRegeneration = gen.CurrentRegeneration,
                Active = gen.Active,
                Recovering = gen.Recovering || gen.Initiating,
                Inactive = !_power.IsPowered(uid),
            });
        }

        _ui.SetUiState(ent.Owner, ImperialModularShieldUiKey.Console, new ImperialModularShieldConsoleState(list));
    }

    private void UpdateConsoles()
    {
        var query = EntityQueryEnumerator<ImperialModularShieldConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateConsoleUi((uid, comp));
    }
}
