using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace XPerfect {
    public static class XPerfectAssets {
        private static readonly Dictionary<string, string> _pathCache = new(StringComparer.OrdinalIgnoreCase);
        public static string GameDir { get; set; }

        public class RawAssets {
            public byte[] StraightMeterBytes;
            public byte[] StraightMeterNoOutlineBytes;
            public byte[] CurvedMeterBytes;
            public byte[] CurvedMeterNoOutlineBytes;
            public string FontPath;
        }

        private static Task<RawAssets> _rawAssetsTask;
        public static Task<RawAssets> RawAssetsTask => _rawAssetsTask;

        public static void InitGameDir() {
            try {
                GameDir = Directory.GetParent(Application.dataPath)?.FullName;
            } catch { }
        }

        public static void StartPreloadingRawAssets() {
            if (_rawAssetsTask is not null) return;
            _rawAssetsTask = Task.Run(LoadRawAssetsBackground);
        }

        private static RawAssets LoadRawAssetsBackground() {
            return new RawAssets {
                StraightMeterBytes = LoadRawBytes("XStraightMeter.png"),
                StraightMeterNoOutlineBytes = LoadRawBytes("XStraightMeterNoOutline.png"),
                CurvedMeterBytes = LoadRawBytes("XCurvedMeter.png"),
                CurvedMeterNoOutlineBytes = LoadRawBytes("XCurvedMeterNoOutline.png"),
                FontPath = GetAssetPath("Maplestory OTF Bold.otf")
            };
        }

        private static byte[] LoadRawBytes(string filename) {
            string path = GetAssetPath(filename);
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                try {
                    return File.ReadAllBytes(path);
                } catch { }
            }
            return null;
        }

        public static string GetAssetPath(string filename) {
            if (_pathCache.TryGetValue(filename, out var cached)) return cached;

            string found = ResolveAssetPath(filename);
            if (!string.IsNullOrEmpty(found)) {
                _pathCache[filename] = found;
                return found;
            }
            return Path.Combine("Mods", "XPerfect", filename);
        }

        private static string ResolveAssetPath(string filename) {
            try {
                string gameDir = GameDir;
                if (string.IsNullOrEmpty(gameDir)) {
                    try { gameDir = Directory.GetParent(Application.dataPath)?.FullName; } catch { }
                }
                if (!string.IsNullOrEmpty(gameDir)) {
                    string pGameMods = Path.Combine(gameDir, "Mods", "XPerfect", filename);
                    if (File.Exists(pGameMods)) return pGameMods;

                    string pGameUser = Path.Combine(gameDir, "UserData", "XPerfect", filename);
                    if (File.Exists(pGameUser)) return pGameUser;
                }
            } catch { }

            string asmLocation = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asmLocation)) {
                string asmDir = Path.GetDirectoryName(asmLocation);
                if (!string.IsNullOrEmpty(asmDir)) {
                    string p1 = Path.Combine(asmDir, filename);
                    if (File.Exists(p1)) return p1;

                    string p2 = Path.Combine(asmDir, "XPerfect", filename);
                    if (File.Exists(p2)) return p2;
                }
            }

            if (!string.IsNullOrEmpty(Main.ModPath)) {
                string p = Path.Combine(Main.ModPath, filename);
                if (File.Exists(p)) return p;
            }

            string pMods = Path.Combine("Mods", "XPerfect", filename);
            if (File.Exists(pMods)) return pMods;

            string pUserData = Path.Combine("UserData", "XPerfect", filename);
            if (File.Exists(pUserData)) return pUserData;

            return null;
        }
    }
}
