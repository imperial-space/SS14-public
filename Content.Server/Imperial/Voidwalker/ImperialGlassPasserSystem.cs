using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>Серверная часть glass_passer: решётка под током (с питанием) не пропускает.</summary>
public sealed class ImperialGlassPasserSystem : SharedImperialGlassPasserSystem
{
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;

    private static readonly System.Reflection.MethodInfo? IsPoweredMethod = typeof(ElectrocutionSystem).GetMethod("IsPowered",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
        new[] { typeof(EntityUid), typeof(ElectrifiedComponent), typeof(TransformComponent) }, null);

    protected override bool IsShocked(EntityUid grille)
    {
        if (!TryComp<ElectrifiedComponent>(grille, out var electrified) || !electrified.Enabled)
            return false;

        // ElectrocutionSystem.IsPowered приватный: та же проверка, что и при ударе током.
        return IsPoweredMethod?.Invoke(_electrocution, new object[] { grille, electrified, Transform(grille) }) is true;
    }
}
