using HarmonyLib;
using TMPro;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Text;

namespace XPerfect {
    public static class CounterDisplay {
        private static Material _cachedMaterial;

        private static string _cachedSpace = "";
        private static int _lastSpacing = -1;

        private static Vector2 _lastAppliedPos = Vector2.zero;
        private static int _lastAppliedFontSize = -1;

        private static GameObject _canvasObj;
        private static TMP_Text _text;
        private static TMP_FontAsset _cachedFont;
        private static Font _sourceFont;

        private static Material _xPerfectBorderMaterial;
        private static bool _borderCaptureAttempted;
        private static TMP_Text _xPerfectOverlay;
        private static RectTransform _xPerfectOverlayRect;

        private static readonly StringBuilder _counterBuilder = new(64);

        public static void Create() {
            if (_canvasObj is not null) return;

            try { ModLogger.Log("[XPerfect] CounterDisplay.Create called"); } catch { }

            _canvasObj = new("XPerfect_Canvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasObj);

            var canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new(Screen.width, Screen.height);

            _text = CreateLabel(_canvasObj);
            _xPerfectOverlay = CreateOverlayLabel(_canvasObj);

            if (_cachedFont is not null)
                _text.font = _cachedFont;
        }

        public static void Destroy() {
            if (_canvasObj is null) return;
            try { ModLogger.Log("[XPerfect] CounterDisplay.Destroy called"); } catch { }
            UnityEngine.Object.Destroy(_canvasObj);
            _canvasObj = null;
            _text = null;
            _xPerfectOverlay = null;
            _xPerfectOverlayRect = null;
            _lastAppliedPos = new(float.NaN, float.NaN);
            _lastAppliedFontSize = -1;
            _borderCaptureAttempted = false;

            if (_xPerfectBorderMaterial is not null) {
                try { UnityEngine.Object.Destroy(_xPerfectBorderMaterial); } catch { }
                _xPerfectBorderMaterial = null;
            }

            if (_cachedFont is not null) {
                UnityEngine.Object.Destroy(_cachedFont);
                _cachedFont = null;
            }
            if (_sourceFont is not null) {
                UnityEngine.Object.Destroy(_sourceFont);
                _sourceFont = null;
            }
            if (_cachedMaterial is not null) {
                try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                _cachedMaterial = null;
            }
        }

        private static TMP_Text CreateLabel(GameObject parent) {
            GameObject go = new("CounterText");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new(0.5f, 0.5f);
            rect.anchorMax = new(0.5f, 0.5f);
            rect.pivot = new(0.5f, 0.5f);
            rect.sizeDelta = new(400f, 100f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = Main.Settings.CounterFontSize;
            tmp.text = "0 0 0";
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;

            if (_cachedMaterial is not null) {
                try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                _cachedMaterial = null;
            }

            Material mat = new(tmp.fontSharedMaterial);
            _cachedMaterial = mat;
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Underlay);

            try {
                mat.SetFloat("_OutlineWidth", 0f);
                mat.SetColor("_OutlineColor", new(0f, 0f, 0f, 0f));
            } catch { }
            tmp.fontSharedMaterial = mat;

            return tmp;
        }

        private static TMP_Text CreateOverlayLabel(GameObject parent) {
            GameObject go = new("XPerfectBorderOverlay");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new(0.5f, 0.5f);
            rect.anchorMax = new(0.5f, 0.5f);
            rect.pivot = new(0.5f, 0.5f);
            rect.sizeDelta = new(200f, 100f);
            _xPerfectOverlayRect = rect;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Baseline;
            tmp.richText = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.text = "";
            tmp.gameObject.SetActive(false);

            return tmp;
        }

        public static void ApplyFont() {
            try {
                if (_cachedFont is null) {
                    _cachedFont = LoadFont();
                    if (_cachedFont is null) return;
                }

                if (_text is not null) {
                    _text.font = _cachedFont;

                    try {
                        if (_cachedMaterial is not null) {
                            try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                            _cachedMaterial = null;
                        }

                        Material mat = new(_text.fontSharedMaterial);
                        mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
                        mat.SetFloat("_OutlineWidth", 0f);
                        mat.SetColor("_OutlineColor", new(0f, 0f, 0f, 0f));
                        _cachedMaterial = mat;
                        _text.fontSharedMaterial = mat;
                    } catch { }
                }
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] ApplyFont error: {ex}");
            }
        }

        private static TMP_FontAsset LoadFont() {
            string path = XPerfectAssets.RawAssetsTask?.IsCompleted == true && !string.IsNullOrEmpty(XPerfectAssets.RawAssetsTask.Result.FontPath)
                ? XPerfectAssets.RawAssetsTask.Result.FontPath
                : XPerfectAssets.GetAssetPath("Maplestory OTF Bold.otf");

            try {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                    ModLogger.Warn($"[XPerfect] font not found: {path}");
                    return null;
                }

                _sourceFont = new(path);
                var asset = TMP_FontAsset.CreateFontAsset(_sourceFont);
                asset.isMultiAtlasTexturesEnabled = true;
                return asset;
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] LoadFont error: {ex}");
                return null;
            }
        }

        public static void ApplySettings() {
            if (_text is null) return;

            Vector2 pos = new(Main.Settings.CounterX, Main.Settings.CounterY);
            if (pos != _lastAppliedPos) {
                _text.rectTransform.anchoredPosition = pos;
                _lastAppliedPos = pos;
            }

            if (Main.Settings.CounterFontSize != _lastAppliedFontSize) {
                _text.fontSize = Main.Settings.CounterFontSize;
                _lastAppliedFontSize = Main.Settings.CounterFontSize;
            }

            int xPerfectCharStart = CountDigits(AccuracyState.GetMinusPerfectCount());
            int xPerfectCharLen = CountDigits(AccuracyState.GetXPerfectCount());
            UpdateXPerfectOverlay(xPerfectCharStart, xPerfectCharLen);
        }

        public static void TryCaptureXPerfectBorderMaterial() {
            if (_xPerfectBorderMaterial is not null) return;
            if (_borderCaptureAttempted) return;
            if (scrController.instance is null) return;
            if (_cachedFont is null) return;

            _borderCaptureAttempted = true;

            GameObject temp = null;
            try {
                GameObject prefab = RDConstants.data.hitTextPrefab;
                if (prefab is null) {
                    ModLogger.Log("[XPerfect] hitTextPrefab is null, cannot capture border material.");
                    _borderCaptureAttempted = false;
                    return;
                }

                temp = UnityEngine.Object.Instantiate(prefab);
                var mesh = temp.GetComponentInChildren<scrHitTextMesh>(true);
                if (mesh is null) {
                    ModLogger.Log("[XPerfect] scrHitTextMesh not found on hitTextPrefab.");
                    _borderCaptureAttempted = false;
                    return;
                }

                mesh.Init(HitMargin.XPerfect);

                var borderTmp = mesh.xPerfectBorder;
                if (borderTmp is not null && borderTmp.fontMaterial is not null) {
                    _xPerfectBorderMaterial = new(borderTmp.fontMaterial);

                    Material maplestoryDefaultMat = _cachedFont.material;

                    _xPerfectBorderMaterial.SetTexture(TMPro.ShaderUtilities.ID_MainTex, _cachedFont.atlasTexture);
                    _xPerfectBorderMaterial.SetFloat(TMPro.ShaderUtilities.ID_GradientScale,
                        maplestoryDefaultMat.GetFloat(TMPro.ShaderUtilities.ID_GradientScale));
                    _xPerfectBorderMaterial.SetFloat(TMPro.ShaderUtilities.ID_TextureWidth,
                        maplestoryDefaultMat.GetFloat(TMPro.ShaderUtilities.ID_TextureWidth));
                    _xPerfectBorderMaterial.SetFloat(TMPro.ShaderUtilities.ID_TextureHeight,
                        maplestoryDefaultMat.GetFloat(TMPro.ShaderUtilities.ID_TextureHeight));

                    if (_xPerfectOverlay is not null) {
                        _xPerfectOverlay.font = _cachedFont;
                        _xPerfectOverlay.fontSharedMaterial = _xPerfectBorderMaterial;
                    }
                } else {
                    ModLogger.Log("[XPerfect] xPerfectBorder or its fontMaterial is null.");
                    _borderCaptureAttempted = false;
                }
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] TryCaptureXPerfectBorderMaterial error: {ex}");
                _borderCaptureAttempted = false;
            } finally {
                if (temp is not null)
                    UnityEngine.Object.Destroy(temp);
            }
        }

        public static void Refresh() {
            if (_canvasObj is null) return;

            var ctrl = scrController.instance;
            var cdt = scrConductor.instance;

            bool isPlaying = ctrl is not null && cdt is not null && !ctrl.paused && cdt.isGameWorld;

            bool visible = Main.Enabled && Main.Settings.ShowCounter && isPlaying && CompetitiveModePrefs.enableCompetitiveMode;
            _canvasObj.SetActive(visible);
            if (!visible) return;

            if (_xPerfectBorderMaterial is null)
                TryCaptureXPerfectBorderMaterial();

            string space = GetSpace();

            var sb = _counterBuilder;
            sb.Length = 0;

            sb.Append("<color=#60FF4E>");
            AppendInt(sb, AccuracyState.GetMinusPerfectCount());
            sb.Append("</color>");
            sb.Append(space);

            sb.Append("<color=#FFFFFF>");
            AppendInt(sb, AccuracyState.GetXPerfectCount());
            sb.Append("</color>");
            sb.Append(space);

            sb.Append("<color=#60FF4E>");
            AppendInt(sb, AccuracyState.GetPlusPerfectCount());
            sb.Append("</color>");

            _text.text = sb.ToString();

            ApplySettings();
        }

        private static void UpdateXPerfectOverlay(int charStart, int charLen) {
            if (_xPerfectOverlay is null || _xPerfectOverlayRect is null) return;

            if (_xPerfectBorderMaterial is null || charLen <= 0) {
                _xPerfectOverlay.gameObject.SetActive(false);
                return;
            }

            _text.ForceMeshUpdate();
            var info = _text.textInfo;

            int lastIndex = charStart + charLen - 1;
            if (charStart < 0 || lastIndex >= info.characterCount) {
                _xPerfectOverlay.gameObject.SetActive(false);
                return;
            }

            var firstChar = info.characterInfo[charStart];
            var lastChar = info.characterInfo[lastIndex];

            if (!firstChar.isVisible && !lastChar.isVisible) {
                _xPerfectOverlay.gameObject.SetActive(false);
                return;
            }

            float centerXLocal = (firstChar.bottomLeft.x + lastChar.topRight.x) * 0.5f;
            float baselineYLocal = firstChar.baseLine;

            Vector3 worldPos = _text.transform.TransformPoint(new(centerXLocal, baselineYLocal, 0f));
            Vector3 localPos = _canvasObj.transform.InverseTransformPoint(worldPos);

            _xPerfectOverlayRect.anchoredPosition = new(localPos.x, localPos.y);

            _xPerfectOverlay.fontSize = _text.fontSize;
            _xPerfectOverlay.text = AccuracyState.GetXPerfectCount().ToString();

            _xPerfectOverlay.gameObject.SetActive(true);
        }

        private static int CountDigits(int value) {
            if (value >= 10000) return 5;
            if (value >= 1000) return 4;
            if (value >= 100) return 3;
            if (value >= 10) return 2;
            return 1;
        }

        private static void AppendInt(StringBuilder sb, int value) {
            if (value >= 10000) {
                sb.Append((char)('0' + (value / 10000) % 10));
                sb.Append((char)('0' + (value / 1000) % 10));
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            } else if (value >= 1000) {
                sb.Append((char)('0' + (value / 1000) % 10));
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            } else if (value >= 100) {
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            } else if (value >= 10) {
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            } else {
                sb.Append((char)('0' + value));
            }
        }

        private static string GetSpace() {
            int s = Main.Settings.CounterSpacing * 2;
            if (s != _lastSpacing) {
                if (s <= 0) {
                    _cachedSpace = string.Empty;
                } else {
                    _cachedSpace = "<space=" + s.ToString() + "px>";
                }
                _lastSpacing = s;
            }
            return _cachedSpace;
        }
    }

    public class CounterRunner : MonoBehaviour {
        private bool _wasPaused = false;
        private bool _lastCompetitive;
        private bool _competitiveInitialized;

        private void Update() {
            var ctrl = scrController.instance;
            if (ctrl is null) return;
            try {
                bool comp = CompetitiveModePrefs.enableCompetitiveMode;
                if (!_competitiveInitialized) {
                    _lastCompetitive = comp;
                    _competitiveInitialized = true;
                } else if (comp != _lastCompetitive) {
                    _lastCompetitive = comp;
                    if (!comp) {
                        try { CounterDisplay.Destroy(); } catch { }
                    } else {
                        try { CounterDisplay.Create(); } catch { }
                        try { CounterDisplay.ApplyFont(); } catch { }
                        try { CounterDisplay.Refresh(); } catch { }
                    }
                    try { MeterVisualPatch.RefreshAllMeters(); } catch { }
                }
            } catch { }

            bool isPaused = ctrl.paused;
            if (isPaused != _wasPaused) {
                _wasPaused = isPaused;
                CounterDisplay.Refresh();
            }
        }

        private void Awake() {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy() {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(
            UnityEngine.SceneManagement.Scene _,
            UnityEngine.SceneManagement.Scene __) {
            StartCoroutine(DelayedRefresh());
        }

        private IEnumerator DelayedRefresh() {
            yield return null;
            CounterDisplay.ApplyFont();
            CounterDisplay.TryCaptureXPerfectBorderMaterial();
            CounterDisplay.Refresh();
        }
    }

    public static class EditorSwitchToEditModePatch {
        public static void Postfix() {
            try { CounterDisplay.Refresh(); } catch (Exception ex) { ModLogger.Error($"[XPerfect] SwitchToEditMode error: {ex}"); }
        }
    }
}
