using System.Collections;
using UnityEngine;

namespace SushiSort.UI
{
    /// <summary>
    /// Procedural ping-pong animation loop for dummy 3D sushi and tubes in the Main Menu.
    /// Animates Pop Out -> Parabolic Arc Move -> Drop -> Wait continuously between tubeA and tubeB.
    /// </summary>
    public class MainMenuAnimator : MonoBehaviour
    {
        [Header("3D Dummy References")]
        [Tooltip("First dummy tube Transform.")]
        [SerializeField] private Transform tubeA;

        [Tooltip("Second dummy tube Transform.")]
        [SerializeField] private Transform tubeB;

        [Tooltip("Dummy 3D sushi piece Transform to animate back and forth.")]
        [SerializeField] private Transform dummySushi;

        [Header("Motion Settings")]
        [Tooltip("Vertical elevation height above the tube when popping out.")]
        [SerializeField] private float popHeight = 2f;

        [Tooltip("Movement speed multiplier for pop, arc, and drop phases.")]
        [SerializeField] private float moveSpeed = 4f;

        [Tooltip("Additional parabola peak height during the horizontal arc transfer.")]
        [SerializeField] private float arcHeight = 0.8f;

        [Tooltip("Vertical offset relative to the tube's pivot when resting inside the tube.")]
        [SerializeField] private float insideTubeOffsetY = 0f;

        [Tooltip("Pause duration in seconds before reversing direction.")]
        [SerializeField] private float waitDuration = 0.5f;

        private Coroutine _loopCoroutine;

        private void OnEnable()
        {
            StartAnimationLoop();
        }

        private void OnDisable()
        {
            StopAnimationLoop();
        }

        /// <summary>
        /// Starts the procedural ping-pong animation coroutine if references are valid.
        /// </summary>
        public void StartAnimationLoop()
        {
            if (tubeA == null || tubeB == null || dummySushi == null)
            {
                Debug.LogWarning("[MainMenuAnimator] Missing tubeA, tubeB, or dummySushi references in Inspector.", this);
                return;
            }

            StopAnimationLoop();
            _loopCoroutine = StartCoroutine(PingPongSushiLoopRoutine());
        }

        /// <summary>
        /// Stops the active animation coroutine.
        /// </summary>
        public void StopAnimationLoop()
        {
            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }
        }

        private IEnumerator PingPongSushiLoopRoutine()
        {
            Transform currentSource = tubeA;
            Transform currentTarget = tubeB;

            // Snap dummySushi initially inside tubeA
            if (dummySushi != null && currentSource != null)
            {
                dummySushi.position = GetInsidePosition(currentSource);
            }

            while (true)
            {
                if (tubeA == null || tubeB == null || dummySushi == null)
                {
                    yield break;
                }

                Vector3 sourceInsidePos = GetInsidePosition(currentSource);
                Vector3 sourceTopPos = GetTopPosition(currentSource);
                Vector3 targetTopPos = GetTopPosition(currentTarget);
                Vector3 targetInsidePos = GetInsidePosition(currentTarget);

                // Fase 1 (Pop Out): Lerp posisi Y dummySushi dari dalam currentSource ke atas currentSource (popHeight)
                yield return LerpLinearRoutine(sourceInsidePos, sourceTopPos, moveSpeed);

                // Fase 2 (Move): Lerp posisi dummySushi membentuk parabola/arc dari atas currentSource ke atas currentTarget
                yield return LerpParabolicArcRoutine(sourceTopPos, targetTopPos, arcHeight, moveSpeed);

                // Fase 3 (Drop): Lerp posisi Y dummySushi masuk ke dalam currentTarget
                yield return LerpLinearRoutine(targetTopPos, targetInsidePos, moveSpeed);

                // Fase 4 (Wait): Tunggu sekian detik
                if (waitDuration > 0f)
                {
                    yield return new WaitForSeconds(waitDuration);
                }
                else
                {
                    yield return null;
                }

                // Fase 5: Balik arah (tubeB kembali ke tubeA secara berulang)
                Transform temp = currentSource;
                currentSource = currentTarget;
                currentTarget = temp;
            }
        }

        private Vector3 GetInsidePosition(Transform tube)
        {
            return tube.position + Vector3.up * insideTubeOffsetY;
        }

        private Vector3 GetTopPosition(Transform tube)
        {
            return tube.position + Vector3.up * (insideTubeOffsetY + popHeight);
        }

        private IEnumerator LerpLinearRoutine(Vector3 startPos, Vector3 endPos, float speed)
        {
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance > 0.001f ? distance / Mathf.Max(0.01f, speed) : 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (dummySushi == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // SmoothStep easing for natural feel
                float smoothT = t * t * (3f - 2f * t);
                dummySushi.position = Vector3.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            if (dummySushi != null)
            {
                dummySushi.position = endPos;
            }
        }

        private IEnumerator LerpParabolicArcRoutine(Vector3 startPos, Vector3 endPos, float peakHeight, float speed)
        {
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance > 0.001f ? distance / Mathf.Max(0.01f, speed) : 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (dummySushi == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 basePos = Vector3.Lerp(startPos, endPos, t);
                float parabola = 4f * peakHeight * t * (1f - t);
                basePos.y += parabola;

                dummySushi.position = basePos;
                yield return null;
            }

            if (dummySushi != null)
            {
                dummySushi.position = endPos;
            }
        }
    }
}

