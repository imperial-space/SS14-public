using Content.Shared.Imperial.CustomChaplain;

namespace Content.Client.Imperial.CustomChaplain.UI
{
    public sealed class GodSelectionBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
    {
        private GodSelectionWindow? _window;

        protected override void Open()
        {
            base.Open();

            _window = new GodSelectionWindow();
            _window.OnGodSelected += OnGodSelected;
            _window.OnClose += Close;
            _window.OpenCentered();
        }

        private void OnGodSelected(string godName, bool isCustom)
        {
            SendMessage(new GodSelectionChooseGodMessage(godName, isCustom));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not GodSelectionBuiState msg)
                return;
            if (_window == null)
                return;

            if (!msg.GodSelected)
                return;

            _window.Close();
            _window = null;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;
            if (_window == null)
                return;

            _window.OnGodSelected -= OnGodSelected;
            _window.Dispose();
            _window = null;
        }
    }
}
