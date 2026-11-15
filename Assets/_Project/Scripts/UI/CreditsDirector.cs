using System.Collections;
using LastRide.Core;
using LastRide.Narrative;
using LastRide.Save;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastRide.UI
{
    /// <summary>
    /// Финал игры (ТЗ 3, этап 12.3): <c>GameManager.Instance.Credits</c>. По <see cref="FinaleCompleted"/>:
    /// Ending — экран заливается белым (<c>finale.whiteFadeSeconds</c>) → Credits — камера переезжает на полянку
    /// (<see cref="CreditsStage"/>), белое уходит, из снега пробивается и раскрывается цветок (<see cref="FlowerGrowth"/>),
    /// строки титров (<c>finale.creditsLines</c>) появляются по очереди → белый → сохранение удаляется → главное меню.
    /// Пропуск — Esc / Enter / Space после <c>finale.skipAfterSeconds</c>.
    /// </summary>
    public sealed class CreditsDirector : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.16f, 0.22f, 0.32f, 1f);
        private static readonly Color32 OutlineColor = new Color32(236, 242, 250, 255);

        private bool _running;
        private TextMeshProUGUI[] _lines = new TextMeshProUGUI[0];
        private TextMeshProUGUI _skipHint;
        private GameObject _canvas;

        public bool Running => _running;

        private void OnEnable() => EventBus<FinaleCompleted>.Subscribe(OnFinaleCompleted);
        private void OnDisable() => EventBus<FinaleCompleted>.Unsubscribe(OnFinaleCompleted);

        private void OnFinaleCompleted(FinaleCompleted e) => Begin();

        /// <summary>Запустить конец игры (по событию; из отладки — вручную).</summary>
        public bool Begin()
        {
            var gm = GameManager.Instance;
            if (_running || gm == null || !gm.IsGameplayActive) return false;
            _running = true;
            StartCoroutine(Run(gm));
            return true;
        }

        private IEnumerator Run(GameManager gm)
        {
            var c = gm.Balance != null ? gm.Balance.finale : new GameBalance.FinaleSettings();
            var fader = gm.Fader;

            // ---- Ending: белая вспышка ----
            gm.DebugTimeMultiplier = 1f;
            gm.RequestState(GameState.Ending);
            if (fader != null)
            {
                fader.PrepareColor(ScreenFader.WhiteColor);
                yield return fader.FadeTo(1f, c.whiteFadeSeconds);
            }

            // ---- Credits: переезжаем на полянку под белым экраном ----
            var stage = FindStage();
            var cam = Camera.main;
            if (stage != null && cam != null)
            {
                stage.gameObject.SetActive(true);
                stage.flower.SetGrowth(0f);
                foreach (var l in stage.GetComponentsInChildren<Light>(true)) l.intensity = c.stageLightIntensity;
                var brain = cam.GetComponent<CinemachineBrain>();
                if (brain != null) brain.enabled = false;
                cam.fieldOfView = stage.fieldOfView;
                cam.nearClipPlane = 0.03f;
                PlaceCamera(cam, stage, 0f);
            }
            else Debug.LogWarning("[Credits] Нет CreditsStage или камеры — титры без цветка (пересобери «Level/Build Finale»).");

            gm.Weather?.SetProgress01(1f);
            gm.RequestState(GameState.Credits);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            BuildUi(c);

            if (fader != null) StartCoroutine(fader.FadeTo(0f, c.clearFadeSeconds));

            // ---- Титры ----
            int n = c.creditsLines != null ? c.creditsLines.Length : 0;
            float total = c.creditsStartDelaySeconds + n * c.creditsLineSeconds + c.creditsHoldSeconds;
            total = Mathf.Max(total, c.flowerGrowSeconds);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                if (stage != null && cam != null)
                {
                    stage.flower.SetGrowth(Ease(t / c.flowerGrowSeconds));
                    PlaceCamera(cam, stage, t / total);
                }
                UpdateLines(c, t);
                if (_skipHint != null) _skipHint.alpha = t > c.skipAfterSeconds ? 0.55f : 0f;
                if (t > c.skipAfterSeconds && SkipPressed()) break;
                yield return null;
            }

            // ---- Конец: белый → сохранение очищается → меню ----
            if (fader != null)
            {
                fader.SetColor(ScreenFader.WhiteColor);
                yield return fader.FadeTo(1f, c.endFadeSeconds);
            }
            if (_canvas != null) Destroy(_canvas);
            SaveSystem.Delete();
            gm.Checkpoints.Clear();
            _running = false;
            if (gm.Flow != null) gm.Flow.ToMainMenu();
        }

        private static CreditsStage FindStage()
        {
            foreach (var s in FindObjectsByType<CreditsStage>(FindObjectsInactive.Include))
                if (s != null && s.flower != null && s.cameraStart != null && s.cameraEnd != null) return s;
            return null;
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        private static void PlaceCamera(Camera cam, CreditsStage stage, float k)
        {
            k = Ease(k);
            Vector3 pos = Vector3.Lerp(stage.cameraStart.position, stage.cameraEnd.position, k);
            Vector3 look = stage.lookTarget != null ? stage.lookTarget.position : stage.flower.transform.position + Vector3.up * 0.12f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }

        private static bool SkipPressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        }

        // ------------------------------------------------------------------ строки титров

        private void BuildUi(GameBalance.FinaleSettings c)
        {
            var canvas = UiKit.CreateCanvas(transform, "CreditsCanvas", 130);
            _canvas = canvas.gameObject;
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            int n = c.creditsLines != null ? c.creditsLines.Length : 0;
            _lines = new TextMeshProUGUI[n];
            for (int i = 0; i < n; i++)
            {
                bool title = i == 0;
                bool signature = i == n - 1 && n > 2; // последняя строка — подпись студии, крупнее и жирнее
                var t = UiKit.Label(canvas.transform, c.creditsLines[i], title ? 104f : (signature ? 76f : 56f), TextColor, TextAlignmentOptions.Center,
                    title || signature ? FontStyles.Bold : FontStyles.Normal);
                t.outlineColor = OutlineColor;
                t.outlineWidth = 0.14f;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 330f);
                rt.sizeDelta = new Vector2(1500f, 220f);
                t.alpha = 0f;
                _lines[i] = t;
            }

            _skipHint = UiKit.Label(canvas.transform, "Esc — пропустить", 26f, TextColor, TextAlignmentOptions.BottomRight);
            _skipHint.outlineColor = OutlineColor;
            var hrt = _skipHint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-40f, 30f);
            hrt.sizeDelta = new Vector2(500f, 50f);
            _skipHint.alpha = 0f;
        }

        private void UpdateLines(GameBalance.FinaleSettings c, float t)
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                float local = (t - c.creditsStartDelaySeconds - i * c.creditsLineSeconds) / c.creditsLineSeconds;
                float a = local <= 0f || local >= 1f ? 0f : Mathf.Clamp01(Mathf.Min(local / 0.22f, (1f - local) / 0.22f));
                _lines[i].alpha = a;
            }
        }
    }
}
