using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Выбор из списка в одну строку («◄ Высокое ►»): стрелки влево/вправо листают значение, клик и Enter — вперёд по кругу,
    /// клик в левой трети — назад. Используется для настроек (качество, разрешение, вкл/выкл).
    /// </summary>
    public sealed class CycleSelector : Selectable, IMoveHandler, ISubmitHandler, IPointerClickHandler
    {
        /// <summary>Вызывается с шагом -1 / +1.</summary>
        public System.Action<int> Step;

        public override void OnMove(AxisEventData e)
        {
            switch (e.moveDir)
            {
                case MoveDirection.Left: Step?.Invoke(-1); e.Use(); break;
                case MoveDirection.Right: Step?.Invoke(1); e.Use(); break;
                default: base.OnMove(e); break;
            }
        }

        public void OnSubmit(BaseEventData eventData) => Step?.Invoke(1);

        public void OnPointerClick(PointerEventData e)
        {
            if (!IsInteractable()) return;
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local);
            float k = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
            Step?.Invoke(k < 0.33f ? -1 : 1);
        }
    }
}
