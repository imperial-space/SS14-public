using Content.Shared.Coordinates;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.SoundOnInteract;

public sealed partial class MedievalSoundOnInteractSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSoundOnInteractComponent, GettingPickedUpAttemptEvent>(OnPick);
        SubscribeLocalEvent<MedievalSoundOnInteractComponent, DroppedEvent>(OnPut);
    }
    private void OnPick(EntityUid uid, MedievalSoundOnInteractComponent comp, GettingPickedUpAttemptEvent ev) // idk why doesn't exists PickedEvent like 26 line
    {
        if (comp.OnPick == null || ev.Cancelled) return;
        var newSound = new SoundPathSpecifier(comp.OnPick.Path)
        {
            Params = new AudioParams
            {
                MaxDistance = 3.5f,
            }
        };
        _audio.PlayPvs(newSound, uid.ToCoordinates());
    }
    private void OnPut(EntityUid uid, MedievalSoundOnInteractComponent comp, DroppedEvent ev)
    {
        if (comp.OnPut == null) return;
        var newSound = new SoundPathSpecifier(comp.OnPut.Path)
        {
            Params = new AudioParams
            {
                MaxDistance = 3.5f,
            }
        };
        _audio.PlayPvs(newSound, uid.ToCoordinates());
    }
}
