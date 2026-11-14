using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Сцена титров (ТЗ 3, финал): крошечная полянка вдали от уровня (ставит <c>FinaleBuilder</c>) — снег, сугробик, цветок
    /// и тёплый свет. Выключена, пока не начнутся титры; <see cref="UI.CreditsDirector"/> переносит туда камеру.
    /// Камера плавно уходит от <see cref="cameraStart"/> к <see cref="cameraEnd"/>, глядя на <see cref="lookTarget"/>.
    /// </summary>
    public sealed class CreditsStage : MonoBehaviour
    {
        public FlowerGrowth flower;
        public Transform cameraStart;
        public Transform cameraEnd;
        public Transform lookTarget;
        [Tooltip("Поле зрения камеры титров, градусов.")]
        public float fieldOfView = 38f;
    }
}
