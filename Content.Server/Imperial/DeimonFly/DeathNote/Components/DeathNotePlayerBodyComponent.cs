namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Marks an entity that has been directly controlled by a player.
/// The marker remains after disconnecting or leaving the body so round-wide
/// Death Note rules can distinguish former player bodies from ordinary NPCs.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNotePlayerBodyComponent : Component;
