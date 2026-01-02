using Content.Shared.Imperial.XxRaay.Components;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.XxRaay.UI;

public sealed class AnimatronicControllerBoundUserInterface : BoundUserInterface
{
	[ViewVariables]
	private AnimatronicControllerWindow? _window;

	public AnimatronicControllerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
	{
	}

	protected override void Open()
	{
		base.Open();

		_window = this.CreateWindow<AnimatronicControllerWindow>();

		_window.OnAnimatronicWaypointSelected += (animEntity, wpEntity) =>
		{
			SendMessage(new SetAnimatronicTargetEvent(animEntity, wpEntity));
		};

		_window.OnAnimatronicClearTarget += (animEntity) =>
		{
			SendMessage(new SetAnimatronicTargetEvent(animEntity, true));
		};

		_window.OnAnimatronicObserving += (animEntity) =>
		{
			SendMessage(new SetAnimatronicObservingEvent(animEntity));
		};

		SendMessage(new RequestAnimDataEvent());
	}

	protected override void UpdateState(BoundUserInterfaceState state)
	{
		base.UpdateState(state);

		if (state is AnimDataStateEvent animState)
		{
			_window?.UpdateState(animState);
		}
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			_window?.Dispose();
		}
	}
}

