using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Runtime.InteropServices;
using PlayerController = ComplexTweaks.Utilities.Structs.PlayerController;
#nullable disable

namespace ComplexTweaks.Utilities.Extensions;

public static unsafe class PlayerExtensions {
    extension(Player) {
        public static PlayerController* Controller => (PlayerController*)Svc.SigScanner.GetStaticAddressFromSig(PlayerController);

        public static float Speed { get => Player.Controller->MoveControllerWalk.BaseMovementSpeed; set => SetSpeed(6 * value); }
        public static byte ReviveState => Player.IsDead ? AgentRevive.Instance()->ReviveState : (byte)0;

        public static FlagMapMarker MapFlag => AgentMap.Instance()->FlagMapMarkers[0];
        public static List<MapMarkerData> QuestLocations => [.. FFXIVClientStructs.FFXIV.Client.Game.UI.Map.Instance()->QuestMarkers.ToArray().SelectMany(i => i.MarkerData.ToList())];

        public static void SetPosition(Vector3 destination) => Player.GameObject->SetPosition(destination.X, destination.Y, destination.Z);

        // If this changes again, since this involves relative offsets, if the instruction bytes change count (e.g. F3 0F 59 05 ?? ... = 4 to F3 44 0F 59 0D ?? ... = 5)
        // update the address math: `address = address + <instruction_byte_count> + Marshal.ReadInt32(address + 4) + 4;`
        // or try and find a sig with the same count (ghidra seems better lately over IDA for getting identical sigs?)
        private static void SetSpeed(float speedBase) {
            Svc.SigScanner.TryScanText(PlayerGroundSpeed, out var address);
            address = address + 4 + Marshal.ReadInt32(address + 4) + 4;
            Dalamud.SafeMemory.Write(address + 20, speedBase);
            SetMoveControlData(speedBase);
        }

        private static void SetMoveControlData(float speed)
            => Dalamud.SafeMemory.Write(((delegate* unmanaged[Stdcall]<byte, nint>)Svc.SigScanner.ScanText(MoveController))(1) + 8, speed);
    }

    private const string MoveController = "E8 ?? ?? ?? ?? 48 85 C0 74 AE 83 FD 05";
    private const string PlayerGroundSpeed = "F3 0F 11 05 ?? ?? ?? ?? 40 38 2D";
    private const string PlayerController = "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F 28 F0 45 0F 57 C0"; // bossmod (Client::Game::Control::InputManager)
}
