using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;

namespace WebRadarSender
{
    // The Packet structure sent to Django
    public class RadarPacket
    {
        public string MapName { get; set; }
        public List<Player> Players { get; set; }
    }

    public class Player
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Team { get; set; }
    }

    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static bool isServerConnected = true;

        static void Main(string[] args)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(1);
            KernelDriver driver = null;

            try
            {
                driver = new KernelDriver("cartidriver");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Driver initialized successfully.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[-] DRIVER INIT FAILED: {ex.Message}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Process targetProcess = null;
            uint pid = 0;
            ulong clientBase = 0;

            Console.WriteLine("[*] Searching for cs2.exe...");
            while (true)
            {
                Process[] processes = Process.GetProcessesByName("cs2");
                if (processes.Length > 0)
                {
                    targetProcess = processes[0];
                    pid = (uint)targetProcess.Id;

                    foreach (ProcessModule module in targetProcess.Modules)
                    {
                        if (module.ModuleName == "client.dll")
                        {
                            clientBase = (ulong)module.BaseAddress;
                            break;
                        }
                    }
                    if (clientBase != 0) break;
                }
                Thread.Sleep(1000);
                Console.Write(".");
            }

            Console.WriteLine($"\n[+] Found CS2 (PID: {pid})");
            Console.WriteLine($"[+] Client.dll: 0x{clientBase:X}");

            // --- OFFSETS ---
            int dwEntityList = 0x24AB1B8;
            int dwGlobalVars = 0x205A580; // Offset to find current map
            int m_hPlayerPawn = 0x90C;
            int m_iszPlayerName = 0x6F8;
            int m_entitySpottedState = 0x26E0;
            int m_bSpotted = 0x8;

            int m_iHealth = 0x354;
            int m_iTeamNum = 0x3F3;
            int m_vOldOrigin = 0x1588;

            while (!targetProcess.HasExited)
            {
                try
                {
                    // 1. Get Map Name safely
                    ulong globalVars = driver.ReadMemory<ulong>(pid, clientBase + (ulong)dwGlobalVars);
                    string currentMap = "unknown";
                    if (globalVars != 0)
                    {
                        ulong mapNamePtr = driver.ReadMemory<ulong>(pid, globalVars + 0x180);
                        if (mapNamePtr != 0)
                        {
                            byte[] mapBytes = driver.ReadMemory(pid, mapNamePtr, 32);
                            if (mapBytes != null && mapBytes.Length > 0)
                            {
                                currentMap = Encoding.UTF8.GetString(mapBytes).Split('\0')[0];
                            }
                        }
                    }

                    // 2. Gather Player Data
                    var webPlayerData = new List<Player>();
                    ulong entityList = driver.ReadMemory<ulong>(pid, clientBase + (ulong)dwEntityList);

                    if (entityList != 0)
                    {
                        ulong listEntry = driver.ReadMemory<ulong>(pid, entityList + 0x10);
                        for (int i = 0; i < 64; i++)
                        {
                            if (listEntry == 0) continue;

                            // Note: 0x70 (112) is the correct stride for newer CS2 versions. 
                            ulong currentController = driver.ReadMemory<ulong>(pid, listEntry + (ulong)(i * 0x70));
                            if (currentController == 0) continue;

                            int pawnHandle = driver.ReadMemory<int>(pid, currentController + (ulong)m_hPlayerPawn);
                            if (pawnHandle == 0) continue;

                            ulong listEntry2 = driver.ReadMemory<ulong>(pid, entityList + (ulong)(0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10));
                            if (listEntry2 == 0) continue;

                            ulong currentPawn = driver.ReadMemory<ulong>(pid, listEntry2 + (ulong)(0x70 * (pawnHandle & 0x1FF)));
                            if (currentPawn == 0) continue;

                            // Read Health FIRST before writing to memory to double-check if it's a valid entity
                            int health = driver.ReadMemory<int>(pid, currentPawn + (ulong)m_iHealth);
                            if (health <= 0 || health > 100) continue;

                            // Force in-game radar safely (Only execute if currentPawn is a validated pointer!)
                            ulong spottedAddress = currentPawn + (ulong)m_entitySpottedState + (ulong)m_bSpotted;
                            driver.WriteMemory<bool>(pid, spottedAddress, true);

                            // Read Team & Pos
                            int team = driver.ReadMemory<int>(pid, currentPawn + (ulong)m_iTeamNum);
                            Vector3 pos = driver.ReadMemory<Vector3>(pid, currentPawn + (ulong)m_vOldOrigin);

                            // Read Name safely
                            string name = "Unknown";
                            byte[] nameBytes = driver.ReadMemory(pid, currentController + (ulong)m_iszPlayerName, 16);
                            if (nameBytes != null && nameBytes.Length > 0)
                            {
                                name = Encoding.UTF8.GetString(nameBytes).Split('\0')[0];
                            }

                            webPlayerData.Add(new Player
                            {
                                Name = string.IsNullOrEmpty(name) ? "Unknown" : name,
                                X = pos.X,
                                Y = pos.Y,
                                Z = pos.Z,
                                Team = team
                            });
                        }
                    }

                    // 3. Send Packet
                    var packet = new RadarPacket
                    {
                        MapName = currentMap,
                        Players = webPlayerData
                    };

                    _ = SendToDjango(packet);

                    Console.Clear();
                    Console.WriteLine($"[+] Map: {currentMap}");
                    Console.WriteLine($"[+] Players Spotted: {webPlayerData.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error: {ex.Message}");
                }
                Thread.Sleep(20);
            }

            static async Task SendToDjango(RadarPacket data)
            {
                try
                {
                    string json = JsonSerializer.Serialize(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await httpClient.PostAsync("http://98.89.6.230/api/radar/update/", content);
                    isServerConnected = true;
                }
                catch { isServerConnected = false; }
            }
        }
    }
}