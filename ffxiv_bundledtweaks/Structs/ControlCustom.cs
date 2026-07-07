using InteropGenerator.Runtime.Attributes;
using System.Runtime.InteropServices;

namespace ComplexTweaks.Structs;

[GenerateInterop]
[StructLayout(LayoutKind.Explicit, Size = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.StructSize)]
public unsafe partial struct ControlCustom {
    [FieldOffset(0x7104)] public float CurrentGroundSpeed;
    [FieldOffset(0x7118)] public float GroundSpeedBase;

    [FieldOffset(0x7210)] public Vector3 FlyPosition;
    [FieldOffset(0x7220)] public Vector3 FlyPositionDelta;
    [FieldOffset(0x7240)] public float FlyHeading;
    [FieldOffset(0x7248)] public float CurrentFlySpeed;
    [FieldOffset(0x724C)] public float FlyDistance; // not sure on this one
}
