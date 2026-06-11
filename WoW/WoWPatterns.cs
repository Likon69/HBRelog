using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.CleanPattern;

namespace HighVoltz.HBRelog.WoW
{
    static public class WoWPatterns
    {
        // WotLK 3.3.5a: sub_8C6330 returns g_ClientConnectionState || (eventId == 10)
        // g_ClientConnectionState (0xBD0792) is set to 1 in CGGameUI__EnterWorld and 0 in CGGameUI__LeaveWorld.
        // Pattern is unique in the binary; AddModifier(2) + LeaModifier resolves to absolute address 0xBD0792,
        // Find() subtracts the image base to return RVA 0x7D0792.
        static public readonly Pattern GameStatePattern = Pattern.FromTextstyle("GameState", "80 3D 92 07 BD 00 00 75 0C 83 3D 38 AA B6 00 0A 74 03 33 C0 C3",
            new AddModifier(2), new LeaModifier());
        static public readonly Pattern Dx9DevicePattern = Pattern.FromTextstyle("Dx9Device", "55 8B EC 8B 55 0C 8B 0D ? ? ? ? 8B 01 8B 80 ? ? ? ? 52 8B 55 08 52 FF D0 5D C3",
            new AddModifier(8), new LeaModifier());
        // WotLK 3.3.5a: sub_68F3D0 — IDirect3DDevice9* stored at CGxDeviceD3d+0x397C (this+3679*4)
        // Pattern: mov ecx,[eax] / neg bl / lea edi,[esi+397Ch] / push edi — AddModifier(6) lands on the 7C 39 00 00 bytes
        static public readonly Pattern Dx9DeviceInxPattern = Pattern.FromTextstyle("Dx9DeviceInx", "8B 08 F6 DB 8D BE 7C 39 00 00 57",
            new AddModifier(6));
        static public readonly Pattern FrameScriptExecutePattern = Pattern.FromTextstyle("FrameScriptExecute", "55 8B EC 51 83 05 ? ? ? ? 01 A1 ? ? ? ? 89 45 FC 74 12 83 3D ? ? ? ? 00");
        static public readonly Pattern LastHardwareEventPattern = Pattern.FromTextstyle("LastHardwareEvent", "53 8B 1D ? ? ? ? 57 8D BE F8 00 00 00 7E 3F 8B 86 00 01 00 00 8B 80 B0 00 00 00 85 C0 74 06 F6 40 20 80 74 29 8B CE",
            new AddModifier(3), new LeaModifier());
        static public readonly Pattern PerformanceCounterPattern = Pattern.FromTextstyle("PerformanceCounter", "2B 15 ? ? ? ? 83 3D ? ? ? ? 00 A3 ? ? ? ? 74 13 8B 0D ? ? ? ?",
            new AddModifier(2), new LeaModifier());
        // WotLK 3.3.5a: sub_4D82C0 — push offset byte_B6A9E0 (screen name string: "login"/"charselect"/"charcreate")
        static public readonly Pattern GlueStatePattern = Pattern.FromTextstyle("GlueState", "55 8B EC 68 FF FF FF 7F 68 ? ? ? ? 68 ? ? ? ? E8 ? ? ? ? 85 C0 75 07",
            new AddModifier(14), new LeaModifier());
    }
}