using System.IO;
using System.Reflection;

namespace XPerfect {
    public static class XPerfectAssets {
        public static string GetAssetPath(string filename) {
            try {
                string gameDir = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
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

            return Path.Combine("Mods", "XPerfect", filename);
        }
    }
}
