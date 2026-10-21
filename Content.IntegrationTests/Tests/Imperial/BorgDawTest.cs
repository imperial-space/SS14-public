using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Instruments;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using ServerInstrumentComponent = Content.Server.Instruments.InstrumentComponent;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
[NonParallelizable]
public sealed class BorgDawTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("generic", "BorgChassisGeneric")]
    [TestCase("engineering", "BorgChassisEngineer")]
    [TestCase("mining", "BorgChassisMining")]
    [TestCase("janitor", "BorgChassisJanitor")]
    [TestCase("medical", "BorgChassisMedical")]
    [TestCase("service", "BorgChassisService")]
    [TestCase("security", "BorgChassisSecurity")]
    public async Task DawSurvivesTypeSelectionAndTogglesForItsOwner(string borgType, string chassisPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid borg = default;

        await Server.WaitAssertion(() =>
        {
            // Ready-made chassis must inherit the ability too, including MapInit type replacement.
            var readyChassis = SEntMan.SpawnEntity(chassisPrototype, map.GridCoords);
            AssertDaw(readyChassis, borgType);
            SEntMan.DeleteEntity(readyChassis);

            borg = SEntMan.SpawnEntity("PlayerBorgBattery", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession, borg);
            AssertDaw(borg, null);
            Assert.That(SEntMan.GetComponent<ServerInstrumentComponent>(borg).InstrumentPlayer, Is.EqualTo(borg));
        });
        await Pair.RunTicksSync(5);

        // Exercise the granted action rather than opening the interface directly.
        await Server.WaitAssertion(() => ToggleAndAssert(borg, true));
        await Pair.RunTicksSync(5);
        await AssertClientUi(borg, true);
        await Server.WaitAssertion(() => ToggleAndAssert(borg, false));
        await Pair.RunTicksSync(5);
        await AssertClientUi(borg, false);

        await Server.WaitAssertion(() =>
        {
            // Use the type-selection BUI event: medical/security replace UI components here.
            var select = new BorgSelectTypeMessage(borgType)
            {
                Actor = borg,
                UiKey = BorgSwitchableTypeUiKey.SelectBorgType,
            };
            SEntMan.EventBus.RaiseLocalEvent(borg, select);
            Assert.That(SEntMan.GetComponent<BorgSwitchableTypeComponent>(borg).SelectedBorgType?.Id,
                Is.EqualTo(borgType));
            AssertDaw(borg, borgType);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() => ToggleAndAssert(borg, true));
        await Pair.RunTicksSync(5);
        await AssertClientUi(borg, true);
        await Server.WaitAssertion(() => ToggleAndAssert(borg, false));
        await Pair.RunTicksSync(5);
        await AssertClientUi(borg, false);
    }

    private void AssertDaw(EntityUid borg, string borgType)
    {
        var instrument = SEntMan.GetComponent<ServerInstrumentComponent>(borg);
        Assert.That(instrument.AllowPercussion, Is.True);
        Assert.That(instrument.AllowProgramChange, Is.True);

        var ui = Server.System<SharedUserInterfaceSystem>();
        Assert.That(ui.HasUi(borg, InstrumentUiKey.Key), Is.True);
        Assert.That(ui.HasUi(borg, BorgUiKey.Key), Is.True);
        Assert.That(ui.HasUi(borg, BorgSwitchableTypeUiKey.SelectBorgType), Is.True);

        var intrinsic = SEntMan.GetComponent<IntrinsicUIComponent>(borg);
        Assert.That(intrinsic.UIs[InstrumentUiKey.Key].ToggleActionEntity, Is.Not.Null);
        var actions = Server.System<SharedActionsSystem>().GetActions(borg);
        Assert.That(actions.Count(action => SEntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID
            == "ActionBorgOpenDaw"), Is.EqualTo(1), "DAW action must not disappear or be duplicated.");

        if (borgType == "medical")
            Assert.That(intrinsic.UIs.Count, Is.EqualTo(2), "Crew monitoring must be retained.");
        if (borgType == "security")
            Assert.That(intrinsic.UIs.Count, Is.EqualTo(3), "Security records and cameras must be retained.");
    }

    private void ToggleAndAssert(EntityUid borg, bool open)
    {
        var action = Server.System<SharedActionsSystem>().GetActions(borg).Single(action =>
            SEntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == "ActionBorgOpenDaw");
        Server.System<SharedActionsSystem>().PerformAction(borg, action);
        Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(borg, InstrumentUiKey.Key, borg),
            Is.EqualTo(open));
        Assert.That(SEntMan.HasComponent<ActiveInstrumentComponent>(borg), Is.EqualTo(open));
    }

    private async Task AssertClientUi(EntityUid borg, bool open)
    {
        await Client.WaitAssertion(() =>
        {
            var clientBorg = Pair.ToClientUid(borg);
            Assert.That(Client.System<SharedUserInterfaceSystem>().IsUiOpen(clientBorg, InstrumentUiKey.Key, clientBorg),
                Is.EqualTo(open));
        });
    }
}
