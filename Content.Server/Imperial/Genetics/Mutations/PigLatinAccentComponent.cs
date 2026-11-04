using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Speech;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Поросячья латынь (мутация piglatin из SS13): начальные согласные слова переносятся в конец, добавляется «ай».
/// </summary>
[RegisterComponent]
public sealed partial class PigLatinAccentComponent : Component;
