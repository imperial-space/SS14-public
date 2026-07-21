using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Ships;

public static class MedievalShipSounds
{
    public static readonly SoundPathSpecifier Drown1 = new("/Audio/Imperial/Medieval/Ships/drown1.ogg");
    public static readonly SoundPathSpecifier Drown2 = new("/Audio/Imperial/Medieval/Ships/drown2.ogg");
    public static readonly SoundPathSpecifier OarUse = new("/Audio/Imperial/Medieval/Ships/oar_use.ogg");
    public static readonly SoundPathSpecifier PumpUse = new("/Audio/Imperial/Medieval/Ships/pomp_use.ogg");
    public static readonly SoundPathSpecifier SailClose = new("/Audio/Imperial/Medieval/Ships/sail_close.ogg");
    public static readonly SoundPathSpecifier SailOpen = new("/Audio/Imperial/Medieval/Ships/sail_open.ogg");
    public static readonly SoundPathSpecifier SailRotate1 = new("/Audio/Imperial/Medieval/Ships/sail_rotate1.ogg");
    public static readonly SoundPathSpecifier SailRotate2 = new("/Audio/Imperial/Medieval/Ships/sail_rotate2.ogg");
    public static readonly SoundPathSpecifier AnchorUse = new("/Audio/Imperial/Medieval/gates_use.ogg");
    public static readonly SoundPathSpecifier HammerUse = new("/Audio/Imperial/Medieval/Ships/hammer.ogg");

    public static readonly SoundPathSpecifier[] Drown = { Drown1, Drown2 };
    public static readonly SoundPathSpecifier[] SailRotate = { SailRotate1, SailRotate2 };
}
