using Content.Shared.Imperial.Medieval.Ships;

public sealed class CaptainShipMegaphoneClientSystem : EntitySystem
{
    private CaptainShipMegaphoneRadialMenu? _activeMenu;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CaptainShipMegaphoneOpenMessage>(OnOpenMenu);
    }

    private void OnOpenMenu(CaptainShipMegaphoneOpenMessage msg, EntitySessionEventArgs args)
    {
        if (_activeMenu != null && _activeMenu.IsOpen)
        {
            _activeMenu.Close();
            _activeMenu = null;
            return;
        }

        _activeMenu = new CaptainShipMegaphoneRadialMenu(msg.Megaphone, this);
        _activeMenu.OpenCentered();

        _activeMenu.OnClose += () =>
        {
            _activeMenu = null;
        };
    }

    public void SendOrderToServer(NetEntity megaphone, string text)
    {
        RaiseNetworkEvent(new CaptainShipMegaphoneSelectedCommandMessage(megaphone, text));
    }
}
