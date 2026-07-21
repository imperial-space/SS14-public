using Content.Shared.Hands.Components;

namespace Content.Shared.Storage.Events;

[ByRefEvent]
public record struct StorageInsertFailedEvent(Entity<StorageComponent?> Storage, Entity<HandsComponent?> Player);

// Imperial Medieval Start
[ByRefEvent]
public readonly record struct StorageItemInsertedEvent(EntityUid ItemUid);

[ByRefEvent]
public readonly record struct StorageItemRemovedEvent(EntityUid ItemUid);
// Imperial Medieval End
