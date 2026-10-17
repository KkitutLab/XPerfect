using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using ADOFAI;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace XPerfect {
    public static class MeterVisualPatch {
        private static Sprite straightSprite;
        private static Sprite curvedSprite;
        private static Sprite straightNoOutlineSprite;
        private static Sprite curvedNoOutlineSprite;

        private static Sprite originalStraightSprite;
        private static Sprite originalCurvedSprite;
        private static Sprite[] originalRDStraightSprites;
        private static Sprite[] originalRDCurvedSprites;

        public static void UpdateLayoutPostfix(
            scrHitErrorMeter __instance,
            ErrorMeterSize size = ErrorMeterSize.Normal,
            ErrorMeterShape shape = ErrorMeterShape.Straight) {
            try {
                if (__instance is null)
                    return;

                if (__instance.straightMeter is not null)
                    __instance.straightMeter.gameObject.SetActive(shape == ErrorMeterShape.Straight);

                if (__instance.curvedMeter is not null)
                    __instance.curvedMeter.gameObject.SetActive(shape == ErrorMeterShape.Curved);

                ApplyCurrentVisualState(__instance);
            } catch (Exception ex) {
                ModLogger.Error($"[MeterVisualPatch/UpdateLayout] {ex}");
            }
        }

        public static void HitMarginPerfectTickSetterPostfix() {
            RefreshAllMeters();
        }

        private static void CaptureOriginalSprites(scrHitErrorMeter meter) {
            if (meter is null) return;

            if (meter.straightMeter is not null && meter.straightMeter.sprite is not null && !IsXPerfectSprite(meter.straightMeter.sprite))
                originalStraightSprite = meter.straightMeter.sprite;

            if (meter.curvedMeter is not null && meter.curvedMeter.sprite is not null && !IsXPerfectSprite(meter.curvedMeter.sprite))
                originalCurvedSprite = meter.curvedMeter.sprite;
        }

        private static void RestoreOriginalSprites(scrHitErrorMeter meter) {
            if (meter is null) return;

            Sprite defStraight = originalStraightSprite;
            if (defStraight is null && originalRDStraightSprites != null && originalRDStraightSprites.Length > 0) {
                int tick = (int)Persistence.hitMarginPerfectTick;
                if (tick >= 0 && tick < originalRDStraightSprites.Length)
                    defStraight = originalRDStraightSprites[tick];
            }
            if (meter.straightMeter is not null && defStraight is not null) {
                meter.straightMeter.sprite = defStraight;
                meter.straightMeter.overrideSprite = null;
                meter.straightMeter.SetAllDirty();
            }

            Sprite defCurved = originalCurvedSprite;
            if (defCurved is null && originalRDCurvedSprites != null && originalRDCurvedSprites.Length > 0) {
                int tick = (int)Persistence.hitMarginPerfectTick;
                if (tick >= 0 && tick < originalRDCurvedSprites.Length)
                    defCurved = originalRDCurvedSprites[tick];
            }
            if (meter.curvedMeter is not null && defCurved is not null) {
                meter.curvedMeter.sprite = defCurved;
                meter.curvedMeter.overrideSprite = null;
                meter.curvedMeter.SetAllDirty();
            }
        }

        private static void EnsureSpritesLoaded(bool forceReload = false) {
            try {
                if (forceReload) {
                    straightSprite = null;
                    straightNoOutlineSprite = null;
                    curvedSprite = null;
                    curvedNoOutlineSprite = null;
                }

                var raw = XPerfectAssets.RawAssetsTask?.IsCompleted == true
                    ? XPerfectAssets.RawAssetsTask.Result
                    : null;

                if (straightSprite is null) {
                    string straightPath = XPerfectAssets.GetAssetPath("XStraightMeter.png");
                    straightSprite = LoadSprite(straightPath, raw?.StraightMeterBytes);
                }

                if (straightNoOutlineSprite is null) {
                    string straightNoOutlinePath = XPerfectAssets.GetAssetPath("XStraightMeterNoOutline.png");
                    straightNoOutlineSprite = LoadSprite(straightNoOutlinePath, raw?.StraightMeterNoOutlineBytes);
                }

                if (curvedSprite is null) {
                    string curvedPath = XPerfectAssets.GetAssetPath("XCurvedMeter.png");
                    curvedSprite = LoadSprite(curvedPath, raw?.CurvedMeterBytes);
                }

                if (curvedNoOutlineSprite is null) {
                    string curvedNoOutlinePath = XPerfectAssets.GetAssetPath("XCurvedMeterNoOutline.png");
                    curvedNoOutlineSprite = LoadSprite(curvedNoOutlinePath, raw?.CurvedMeterNoOutlineBytes);
                }
            } catch (Exception ex) {
                ModLogger.Error($"[MeterVisualPatch/EnsureSpritesLoaded] {ex}");
            }
        }

        private static Sprite LoadSprite(string filePath, byte[] preloadedBytes = null) {
            byte[] bytes = preloadedBytes;
            if (bytes is null || bytes.Length == 0) {
                if (string.IsNullOrEmpty(filePath)) return null;
                if (!File.Exists(filePath)) {
                    ModLogger.Error($"[MeterVisualPatch/LoadSprite] File not found: {filePath}");
                    return null;
                }
                try {
                    bytes = File.ReadAllBytes(filePath);
                } catch (Exception ex) {
                    ModLogger.Error($"[MeterVisualPatch/LoadSprite] ReadAllBytes failed on {filePath}: {ex}");
                    return null;
                }
            }

            try {
                Texture2D texture = new(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, bytes)) {
                    ModLogger.Error($"[MeterVisualPatch/LoadSprite] LoadImage failed: {filePath}");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Rect rect = new(0f, 0f, texture.width, texture.height);
                Vector2 pivot = new(0.5f, 0.5f);

                // ADOFAI native meter sprites use 1.0 pixelsPerUnit (ppu)
                var spr = Sprite.Create(texture, rect, pivot, 1.0f);
                spr.name = Path.GetFileNameWithoutExtension(filePath ?? "XMeter");
                ModLogger.Log($"[MeterVisualPatch/LoadSprite] Loaded {spr.name}");
                return spr;
            } catch (Exception ex) {
                ModLogger.Error($"[MeterVisualPatch/LoadSprite] Exception on {filePath}: {ex}");
                return null;
            }
        }

        public static void RefreshAllMeters() {
            try {
                PatchRDConstants();
                scrHitErrorMeter[] meters = UnityEngine.Object.FindObjectsByType<scrHitErrorMeter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (meters is null || meters.Length == 0)
                    return;

                foreach (var meter in meters) {
                    if (meter is null) continue;
                    try {
                        ApplyCurrentVisualState(meter);
                    } catch (Exception ex) {
                        ModLogger.Error($"[MeterVisualPatch/RefreshAllMeters] {ex}");
                    }
                }
            } catch (Exception ex) {
                ModLogger.Error($"[MeterVisualPatch/RefreshAllMeters/Outer] {ex}");
            }
        }

        public static void PatchRDConstants() {
            try {
                if (!Main.Enabled || !Main.Settings.CustomErrorMeter) {
                    RestoreRDConstants();
                    return;
                }

                EnsureSpritesLoaded();
                var data = RDConstants.data;
                if (data is null) return;

                if (originalRDStraightSprites is null && data.straightErrorMeterSprites != null)
                    originalRDStraightSprites = (Sprite[])data.straightErrorMeterSprites.Clone();

                if (originalRDCurvedSprites is null && data.curvedErrorMeterSprites != null)
                    originalRDCurvedSprites = (Sprite[])data.curvedErrorMeterSprites.Clone();

                var straight = GetStraightSprite();
                if (straight != null && data.straightErrorMeterSprites != null) {
                    for (int i = 0; i < data.straightErrorMeterSprites.Length; i++) {
                        data.straightErrorMeterSprites[i] = straight;
                    }
                }

                var curved = GetCurvedSprite();
                if (curved != null && data.curvedErrorMeterSprites != null) {
                    for (int i = 0; i < data.curvedErrorMeterSprites.Length; i++) {
                        data.curvedErrorMeterSprites[i] = curved;
                    }
                }
            } catch (Exception ex) {
                ModLogger.Error($"[MeterVisualPatch/PatchRDConstants] {ex}");
            }
        }

        public static void RestoreRDConstants() {
            try {
                var data = RDConstants.data;
                if (data is null) return;

                if (originalRDStraightSprites != null && data.straightErrorMeterSprites != null)
                    Array.Copy(originalRDStraightSprites, data.straightErrorMeterSprites, originalRDStraightSprites.Length);

                if (originalRDCurvedSprites != null && data.curvedErrorMeterSprites != null)
                    Array.Copy(originalRDCurvedSprites, data.curvedErrorMeterSprites, originalRDCurvedSprites.Length);
            } catch { }
        }

        private static void ApplyCurrentVisualState(scrHitErrorMeter meter) {
            if (meter is null)
                return;

            CaptureOriginalSprites(meter);

            if (!Main.Enabled || !Main.Settings.CustomErrorMeter) {
                RestoreOriginalSprites(meter);
                return;
            }

            EnsureSpritesLoaded();

            var straight = GetStraightSprite();
            if (meter.straightMeter is not null && straight is not null) {
                meter.straightMeter.sprite = straight;
                meter.straightMeter.overrideSprite = straight;
                meter.straightMeter.SetAllDirty();
            }

            var curved = GetCurvedSprite();
            if (meter.curvedMeter is not null && curved is not null) {
                meter.curvedMeter.sprite = curved;
                meter.curvedMeter.overrideSprite = curved;
                meter.curvedMeter.SetAllDirty();
            }
        }

        private static Sprite GetStraightSprite() {
            return straightSprite ?? straightNoOutlineSprite;
        }

        private static Sprite GetCurvedSprite() {
            return curvedSprite ?? curvedNoOutlineSprite;
        }

        private static bool IsXPerfectSprite(Sprite sprite) {
            if (sprite is null) return false;
            return sprite == straightSprite || sprite == curvedSprite ||
                   sprite == straightNoOutlineSprite || sprite == curvedNoOutlineSprite;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal static void SelfCheck() {
            System.Diagnostics.Debug.Assert(LoadSprite(null) is null);
            System.Diagnostics.Debug.Assert(!IsXPerfectSprite(null));
            System.Diagnostics.Debug.Assert(Main.Settings.CustomErrorMeter);
        }
    }
}
