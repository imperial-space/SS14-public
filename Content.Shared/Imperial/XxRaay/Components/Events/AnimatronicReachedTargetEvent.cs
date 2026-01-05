using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.XxRaay.Components.Events;

/// <summary>
/// Событие, которое вызывается когда аниматроник достигает цели.
/// Содержит EntityUid аниматроника, достигшего цели.
/// </summary>
public sealed class AnimatronicReachedTargetEvent : EntityEventArgs
{
	/// <summary>
	/// EntityUid аниматроника, который достиг цели.
	/// </summary>
	public EntityUid Animatronic { get; }

	public AnimatronicReachedTargetEvent(EntityUid animatronic)
	{
		Animatronic = animatronic;
	}
}

