using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.CustomChaplain;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.CustomChaplain.UI
{
    public sealed class GodSelectionBoundUserInterface : BoundUserInterface
    {
        private GodSelectionWindow? _window;

        public GodSelectionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = new GodSelectionWindow();
            _window.OnGodSelected += OnGodSelected;
            _window.OnClose += () => Close();
            _window.OpenCentered();
        }

        private void OnGodSelected(string godName, bool isCustom)
        {
            SendMessage(new GodSelectionChooseGodMessage(godName, isCustom));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is GodSelectionBuiState msg)
            {
                if (_window != null)
                {
                    if (msg.GodSelected)
                    {
                        _window.Close();
                        _window = null;
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (_window != null)
                {
                    _window.OnGodSelected -= OnGodSelected;
                    _window.Dispose();
                    _window = null;
                }
            }
        }
    }
}
