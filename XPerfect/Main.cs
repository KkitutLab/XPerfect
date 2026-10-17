using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

#if UMM
using UnityModManagerNet;
#endif

namespace XPerfect {
    public static class Main {
        public static XPerfectSettings Settings { get; set; } = new();
        public static string ModPath { get; set; } = string.Empty;
        public static bool Enabled { get; private set; }

        private static HarmonyLib.Harmony _harmony;
        private static GameObject _runnerGo;

#if UMM
        public static bool Load(UnityModManager.ModEntry modEntry) {
            try {
                ModPath = modEntry.Path;
                Settings = XPerfectSettings.Load();

                if (!Initialize()) {
                    return false;
                }

                modEntry.OnToggle = (entry, value) => OnToggle(value);
                modEntry.OnGUI = (entry) => DrawGUI();
                modEntry.OnSaveGUI = (entry) => Settings.Save();
                modEntry.OnUnload = (entry) => Dispose();

                return true;
            } catch (Exception ex) {
                ModLogger.Error($"UMM Load failed: {ex}");
                Dispose();
                return false;
            }
        }
#endif

#if ML
        public static bool InitializeML() {
            try {
                ModPath = Path.Combine("UserData", "XPerfect");
                Settings = XPerfectSettings.Load();

                if (!Initialize()) {
                    return false;
                }

                ModMenu.ModMenuApi.Register(new ModMenu.ModEntry {
                    Id = "XPerfect",
                    DisplayName = "XPerfect",
                    Version = "3.1.0",
                    Author = "8100print & Kkitut",
                    Enabled = Enabled,
                    OnToggle = (val) => OnToggle(val),
                    OnGUI = () => DrawGUI(),
                    OnSaveGUI = () => Settings.Save()
                });

                return true;
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] ML Initialize failed: {ex}");
                Dispose();
                return false;
            }
        }
#endif

        public static bool Initialize() {
            try {
                _harmony = new("com.xperfect.mod");
                if (!SafePatchManager.ApplyAll(_harmony)) {
                    Dispose();
                    return false;
                }

                Enabled = true;
                AccuracyState.Reset();
                MeterVisualPatch.SelfCheck();
                DetailedResultsPatch.SelfCheck();

                if (_runnerGo is null) {
                    _runnerGo = new("XPerfect_Runner");
                    UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
                    var runner = _runnerGo.AddComponent<CounterRunner>();
                    runner.StartCoroutine(InitialCreate(runner));
                }

                ModLogger.Log("[XPerfect] Mod initialized successfully.");
                return true;
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] Initialization failed: {ex.Message}");
                Dispose();
                return false;
            }
        }

        private static IEnumerator InitialCreate(CounterRunner runner) {
            yield return null;
            CounterDisplay.Create();
            yield return null;
            CounterDisplay.ApplyFont();
            CounterDisplay.Refresh();
            try { MeterVisualPatch.RefreshAllMeters(); } catch { }
        }

        public static bool OnToggle(bool value) {
            Enabled = value;
            CompetitiveModePrefs.runtimeDisabled = !value;
            AccuracyState.Reset();
            if (!value) {
                try { CounterDisplay.Destroy(); } catch { }
                try { MeterVisualPatch.RestoreRDConstants(); } catch { }
                try { MeterVisualPatch.RefreshAllMeters(); } catch { }
            } else {
                try { CounterDisplay.Create(); } catch { }
                try { CounterDisplay.ApplyFont(); } catch { }
                try { CounterDisplay.Refresh(); } catch { }
                try { MeterVisualPatch.PatchRDConstants(); } catch { }
                try { MeterVisualPatch.RefreshAllMeters(); } catch { }
            }
            return true;
        }

        public static bool Dispose() {
            try {
                Enabled = false;
                try { CompetitiveModePrefs.runtimeDisabled = false; } catch { }

                try { MeterVisualPatch.RestoreRDConstants(); } catch { }
                try { MeterVisualPatch.RefreshAllMeters(); } catch { }
                try { CounterDisplay.Destroy(); } catch { }

                if (_runnerGo is not null) {
                    UnityEngine.Object.Destroy(_runnerGo);
                    _runnerGo = null;
                }

                if (_harmony is not null) {
                    try { _harmony.UnpatchSelf(); } catch { }
                    _harmony = null;
                }

#if ML
                ModMenu.ModMenuApi.Unregister("XPerfect");
#endif
            } catch { }
            return true;
        }

        public static void DrawGUI() {
            Settings.XPerfectOnly = GUILayout.Toggle(Settings.XPerfectOnly, "XPerfect Only");
            Settings.MergePerfects = GUILayout.Toggle(Settings.MergePerfects, "Merge Perfects");
            bool prevMeter = Settings.CustomErrorMeter;
            Settings.CustomErrorMeter = GUILayout.Toggle(Settings.CustomErrorMeter, "Custom Error Meter");
            if (Settings.CustomErrorMeter != prevMeter) {
                if (Settings.CustomErrorMeter) {
                    MeterVisualPatch.PatchRDConstants();
                    MeterVisualPatch.RefreshAllMeters();
                } else {
                    MeterVisualPatch.RestoreRDConstants();
                    MeterVisualPatch.RefreshAllMeters();
                }
            }
            bool prevShow = Settings.ShowCounter;
            Settings.ShowCounter = GUILayout.Toggle(Settings.ShowCounter, "Show Counter");
            if (Settings.ShowCounter != prevShow)
                CounterDisplay.Refresh();

            if (Settings.ShowCounter) {
                GUILayout.BeginVertical();

                int prevSize = Settings.CounterFontSize;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Font Size:", GUILayout.Width(90));
                int sliderFontSize = Mathf.Clamp(Settings.CounterFontSize, 0, 100);
                sliderFontSize = (int)GUILayout.HorizontalSlider(sliderFontSize, 0, 100, GUILayout.Width(180));
                if (sliderFontSize != Mathf.Clamp(prevSize, 0, 100))
                    Settings.CounterFontSize = sliderFontSize;

                string sizeStr = GUILayout.TextField(Settings.CounterFontSize.ToString(), GUILayout.Width(60));
                if (int.TryParse(sizeStr, out int parsedSize)) {
                    Settings.CounterFontSize = parsedSize;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterFontSize != prevSize)
                    CounterDisplay.ApplySettings();

                int prevSpacing = Settings.CounterSpacing;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Spacing:", GUILayout.Width(90));
                int sliderSpacing = Mathf.Clamp(Settings.CounterSpacing, 0, 50);
                sliderSpacing = (int)GUILayout.HorizontalSlider(sliderSpacing, 0f, 50f, GUILayout.Width(180));
                if (sliderSpacing != Mathf.Clamp(prevSpacing, 0, 50))
                    Settings.CounterSpacing = sliderSpacing;

                string spacingStr = GUILayout.TextField(Settings.CounterSpacing.ToString(), GUILayout.Width(60));
                if (int.TryParse(spacingStr, out int parsedSpacingInt)) {
                    Settings.CounterSpacing = parsedSpacingInt;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterSpacing != prevSpacing)
                    CounterDisplay.Refresh();

                float prevX = Settings.CounterX;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Position X:", GUILayout.Width(90));
                float sliderX = Mathf.Clamp(Settings.CounterX, -Screen.width, Screen.width);
                sliderX = GUILayout.HorizontalSlider(sliderX, -Screen.width, Screen.width, GUILayout.Width(180));
                if (!Mathf.Approximately(sliderX, Mathf.Clamp(prevX, -Screen.width, Screen.width)))
                    Settings.CounterX = sliderX;

                string xStr = GUILayout.TextField(Settings.CounterX.ToString("F0"), GUILayout.Width(60));
                if (float.TryParse(xStr, out float parsedX)) {
                    Settings.CounterX = parsedX;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterX != prevX)
                    CounterDisplay.ApplySettings();

                float prevY = Settings.CounterY;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Position Y:", GUILayout.Width(90));
                float sliderY = Mathf.Clamp(Settings.CounterY, -Screen.height, Screen.height);
                sliderY = GUILayout.HorizontalSlider(sliderY, -Screen.height, Screen.height, GUILayout.Width(180));
                if (!Mathf.Approximately(sliderY, Mathf.Clamp(prevY, -Screen.height, Screen.height)))
                    Settings.CounterY = sliderY;

                string yStr = GUILayout.TextField(Settings.CounterY.ToString("F0"), GUILayout.Width(60));
                if (float.TryParse(yStr, out float parsedY)) {
                    Settings.CounterY = parsedY;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterY != prevY)
                    CounterDisplay.ApplySettings();

                GUILayout.EndVertical();
            }
        }
    }

    public class XPerfectSettings {
        public bool XPerfectOnly = false;
        public bool MergePerfects = true;
        public bool CustomErrorMeter = true;
        public bool ShowCounter = false;
        public int CounterFontSize = 40;
        public int CounterSpacing = 20;
        public float CounterX = 0f;
        public float CounterY = 0f;

        private static string GetSettingsPath(bool forSaving = false) {
            string userDataPath = Path.Combine("UserData", "XPerfect", "Settings.json");
            string modsPath = Path.Combine("Mods", "XPerfect", "Settings.json");

            if (!string.IsNullOrEmpty(Main.ModPath)) {
                string modPathFile = Path.Combine(Main.ModPath, "Settings.json");
                if (File.Exists(modPathFile)) return modPathFile;
            }

            if (File.Exists(userDataPath)) return userDataPath;
            if (File.Exists(modsPath)) return modsPath;

            if (forSaving) {
#if UMM
                if (!string.IsNullOrEmpty(Main.ModPath)) {
                    return Path.Combine(Main.ModPath, "Settings.json");
                }
                return modsPath;
#else
                return userDataPath;
#endif
            }

            return userDataPath;
        }

        public void Save() {
            try {
                string file = GetSettingsPath(forSaving: true);
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(file, json);
            } catch (Exception ex) {
                ModLogger.Error($"Failed to save settings: {ex.Message}");
            }
        }

        public static XPerfectSettings Load() {
            try {
                string file = GetSettingsPath(forSaving: false);
                if (File.Exists(file)) {
                    string json = File.ReadAllText(file);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<XPerfectSettings>(json) ?? new();
                }
            } catch (Exception ex) {
                ModLogger.Error($"Failed to load settings: {ex.Message}");
            }
            return new();
        }
    }
}
