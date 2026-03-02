using System.Numerics;
using System.Runtime.InteropServices;

namespace WebRadarSender
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PawnData
    {
        [FieldOffset(0x3F3)] public int Team;
        [FieldOffset(0x354)] public int Health;
        [FieldOffset(0x1588)] public Vector3 Position;
    }
}