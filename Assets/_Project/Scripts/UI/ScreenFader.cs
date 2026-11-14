using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Затемнение экрана между сценами (меню ↔ игра): один на игру, живёт под GameManager, считает по реальному времени.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        public static readonly Color DarkColor = new Color(0.02f, 0.03f, 0.06f, 1f);
        public static readonly Color WhiteColor = new Color(1f, 1f, 1f, 1f);

        private Color _color = DarkColor;

        private Image _image;
        private float _alpha;

        public float Alpha => _alpha;

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas(transform, "FadeCanvas", 500);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            _image = UiKit.Panel(canvas.transform, "Fade", DarkColor);
            _image.raycastTarget = false;
            var rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Set(0f);
        }

        public void Set(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);
            if (_image == null) return;
            var c = _color;
            c.a = _alpha;
            _image.color = c;
            _image.enabled = _alpha > 0.001f;
        }

        /// <summary>Цвет затемнения (тёмный между сценами, белый в финале). Меняется сразу — вызывать при прозрачном экране или при полном.</summary>
        public void SetColor(Color color)
        {
            _color = color;
            Set(_alpha);
        }

        /// <summary>Сменить цвет, только если экран сейчас прозрачен (чтобы не мигнуть цветом посреди затемнения).</summary>
        public void PrepareColor(Color color)
        {
            if (_alpha < 0.01f) SetColor(color);
        }

        /// <summary>Плавно довести затемнение до <paramref name="target"/> (1 — чёрный экран, 0 — прозрачно).</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            float start = _alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            Set(target);
        }
    }
}
