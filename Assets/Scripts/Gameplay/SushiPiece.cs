using System;
using System.Collections;
using UnityEngine;
using SushiSort.Core;
using SushiSort.ScriptableObjects;

namespace SushiSort.Gameplay
{
    /// <summary>
    /// Represents an individual 3D sushi piece in the puzzle.
    /// Manages its own visual rendering, mystery roll status, and procedural motion coroutines.
    /// Safely handles active state guards and coroutine fallback runners.
    /// </summary>
    [SelectionBase]
    public class SushiPiece : MonoBehaviour
    {
        [Header("Identity & State")]
        [SerializeField] private SushiType sushiType = SushiType.None;
        [SerializeField] private bool isMystery = false;

        [Header("Visual Components")]
        [Tooltip("Root or mesh of the normal revealed sushi piece.")]
        [SerializeField] private GameObject normalModel;

        [Tooltip("MeshRenderer of the normal sushi piece (used for material swapping if needed).")]
        [SerializeField] private MeshRenderer normalRenderer;

        [Tooltip("The dark seaweed/nori wrap GameObject masking the sushi when in mystery mode.")]
        [SerializeField] private GameObject mysteryWrapper;

        [Tooltip("Particle system triggered when a mystery sushi is revealed.")]
        [SerializeField] private ParticleSystem revealParticle;

        // Public Getters (Strict Encapsulation)
        public SushiType SushiType => sushiType;
        public bool IsMystery => isMystery;

        // Coroutine references and active runners
        private Coroutine _movementCoroutine;
        private MonoBehaviour _movementRunner;
        private Coroutine _revealCoroutine;
        private MonoBehaviour _revealRunner;

        /// <summary>
        /// Initializes the sushi piece type, mystery status, and visual materials.
        /// Guaranteed to set this GameObject active, restore scale, and ensure full visual rendering.
        /// </summary>
        public void Initialize(SushiType type, bool mystery, SushiDataSO data)
        {
            // 1. Aktifkan root GameObject
            gameObject.SetActive(true);

            // 2. Pastikan localScale tidak bernilai Vector3.zero
            if (transform.localScale == Vector3.zero || 
                transform.localScale.x <= 0.0001f || 
                transform.localScale.y <= 0.0001f || 
                transform.localScale.z <= 0.0001f)
            {
                transform.localScale = Vector3.one;
            }

            sushiType = type;
            isMystery = mystery;

            // 3. Auto-resolve visual references if missing or pointing to root
            ResolveVisualReferences();

            // 4. Apply custom material if specified in SushiDataSO
            if (data != null && data.SushiMaterial != null && normalRenderer != null)
            {
                normalRenderer.sharedMaterial = data.SushiMaterial;
            }

            // 5. Configure visual states & renderers
            UpdateVisualState();
        }

        private void ResolveVisualReferences()
        {
            // If mysteryWrapper points to root, is null, or is an external prefab asset, find child named "MisteriSushi"
            if (mysteryWrapper == null || mysteryWrapper == gameObject || !mysteryWrapper.transform.IsChildOf(transform))
            {
                Transform mysteryChild = transform.Find("MisteriSushi") ?? FindDeepChild(transform, "MisteriSushi");
                if (mysteryChild != null && mysteryChild.gameObject != gameObject)
                {
                    mysteryWrapper = mysteryChild.gameObject;
                }
                else
                {
                    mysteryWrapper = null;
                }
            }

            // If normalModel points to root or is null, find the normal roll mesh child
            if (normalModel == null || normalModel == gameObject)
            {
                foreach (Transform child in transform)
                {
                    if (mysteryWrapper != null && (child == mysteryWrapper.transform || child.IsChildOf(mysteryWrapper.transform)))
                        continue;
                    normalModel = child.gameObject;
                    break;
                }
            }

            if (normalRenderer == null && normalModel != null)
            {
                normalRenderer = normalModel.GetComponentInChildren<MeshRenderer>(true);
            }
            if (normalRenderer == null)
            {
                Renderer[] allR = GetComponentsInChildren<Renderer>(true);
                foreach (var r in allR)
                {
                    if (mysteryWrapper != null && (r.gameObject == mysteryWrapper || r.transform.IsChildOf(mysteryWrapper.transform)))
                        continue;
                    if (r is MeshRenderer mr)
                    {
                        normalRenderer = mr;
                        break;
                    }
                }
            }
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Sets normal and mystery model active states based on current isMystery flag.
        /// Ensures all non-mystery renderers are active and enabled.
        /// </summary>
        private void UpdateVisualState()
        {
            if (mysteryWrapper != null && mysteryWrapper != gameObject)
            {
                mysteryWrapper.SetActive(isMystery);
            }

            if (normalModel != null && normalModel != gameObject)
            {
                normalModel.SetActive(!isMystery);
            }

            // Pastikan semua child Renderer menyala (kecuali mysteryWrapper jika bukan mystery)
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (mysteryWrapper != null && (r.gameObject == mysteryWrapper || r.transform.IsChildOf(mysteryWrapper.transform)))
                {
                    r.gameObject.SetActive(isMystery);
                    r.enabled = isMystery;
                    continue;
                }

                // If not mystery, ensure normal sushi meshes are fully active and enabled
                if (!isMystery)
                {
                    r.gameObject.SetActive(true);
                    r.enabled = true;
                }
                else
                {
                    r.gameObject.SetActive(false);
                    r.enabled = false;
                }
            }
        }

        /// <summary>
        /// Safely starts a coroutine, ensuring gameObject is active.
        /// If this gameObject remains inactive in hierarchy (e.g. inactive parent transform),
        /// it safely delegates execution to GamePlayManager.Instance as a fallback runner.
        /// </summary>
        private Coroutine SafeStartCoroutine(IEnumerator routine, out MonoBehaviour runner)
        {
            if (routine == null)
            {
                runner = null;
                return null;
            }

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (gameObject.activeInHierarchy)
            {
                runner = this;
                return StartCoroutine(routine);
            }
            else if (GamePlayManager.Instance != null && GamePlayManager.Instance.gameObject.activeInHierarchy)
            {
                runner = GamePlayManager.Instance;
                return GamePlayManager.Instance.StartCoroutine(routine);
            }

            runner = null;
            return null;
        }

        private void SafeStopCoroutine(ref Coroutine coroutine, ref MonoBehaviour runner)
        {
            if (coroutine != null && runner != null)
            {
                if (runner.gameObject.activeInHierarchy)
                {
                    runner.StopCoroutine(coroutine);
                }
            }
            coroutine = null;
            runner = null;
        }

        /// <summary>
        /// Reveals this mystery sushi with a procedural pop and rotation effect.
        /// </summary>
        public void RevealMystery(Action onComplete = null)
        {
            if (!isMystery)
            {
                onComplete?.Invoke();
                return;
            }

            isMystery = false;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            SafeStopCoroutine(ref _revealCoroutine, ref _revealRunner);
            _revealCoroutine = SafeStartCoroutine(AnimateRevealRoutine(onComplete), out _revealRunner);

            // Immediate fallback if coroutine could not be started
            if (_revealCoroutine == null)
            {
                if (mysteryWrapper != null) mysteryWrapper.SetActive(false);
                onComplete?.Invoke();
            }
        }

        private IEnumerator AnimateRevealRoutine(Action onComplete)
        {
            if (revealParticle != null)
            {
                revealParticle.Play();
            }

            if (mysteryWrapper != null)
            {
                Vector3 originalScale = mysteryWrapper.transform.localScale;
                Quaternion originalRot = mysteryWrapper.transform.localRotation;
                float elapsed = 0f;
                float duration = 0.25f;

                // Punch scale up slightly, then shrink to 0
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    // Easing scale out
                    float scaleFactor = Mathf.Sin(t * Mathf.PI);
                    mysteryWrapper.transform.localScale = Vector3.Lerp(originalScale * 1.25f, Vector3.zero, t);
                    mysteryWrapper.transform.Rotate(Vector3.up, 360f * Time.deltaTime * 2f, Space.Self);

                    yield return null;
                }

                mysteryWrapper.SetActive(false);
                mysteryWrapper.transform.localScale = originalScale;
                mysteryWrapper.transform.localRotation = originalRot;
            }

            UpdateVisualState();

            // Normal model subtle pop
            if (normalModel != null)
            {
                Vector3 origScale = normalModel.transform.localScale;
                float elapsed = 0f;
                float popDuration = 0.15f;

                while (elapsed < popDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / popDuration);
                    float bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
                    normalModel.transform.localScale = origScale * bump;
                    yield return null;
                }

                normalModel.transform.localScale = origScale;
            }

            _revealCoroutine = null;
            _revealRunner = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Smooth linear or eased movement to target world position without external dependencies.
        /// </summary>
        public void MoveTo(Vector3 targetPosition, float duration, Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            SafeStopCoroutine(ref _movementCoroutine, ref _movementRunner);

            if (duration <= 0.001f)
            {
                transform.position = targetPosition;
                onComplete?.Invoke();
                return;
            }

            _movementCoroutine = SafeStartCoroutine(AnimateLinearMovementRoutine(targetPosition, duration, onComplete), out _movementRunner);

            // Immediate fallback if coroutine could not be started
            if (_movementCoroutine == null)
            {
                transform.position = targetPosition;
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Parabolic jump motion to target world position (used for leaping between tubes).
        /// </summary>
        public void MoveAlongArc(Vector3 targetPosition, float arcHeight, float duration, Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            SafeStopCoroutine(ref _movementCoroutine, ref _movementRunner);

            if (duration <= 0.001f)
            {
                transform.position = targetPosition;
                onComplete?.Invoke();
                return;
            }

            _movementCoroutine = SafeStartCoroutine(AnimateArcMovementRoutine(targetPosition, arcHeight, duration, onComplete), out _movementRunner);

            // Immediate fallback if coroutine could not be started
            if (_movementCoroutine == null)
            {
                transform.position = targetPosition;
                onComplete?.Invoke();
            }
        }

        private IEnumerator AnimateLinearMovementRoutine(Vector3 targetPosition, float duration, Action onComplete)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            if (duration <= 0.001f)
            {
                transform.position = targetPosition;
                _movementCoroutine = null;
                _movementRunner = null;
                onComplete?.Invoke();
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // SmoothStep easing for clean visual polish
                float smoothT = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, targetPosition, smoothT);
                yield return null;
            }

            transform.position = targetPosition;
            _movementCoroutine = null;
            _movementRunner = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateArcMovementRoutine(Vector3 targetPosition, float arcHeight, float duration, Action onComplete)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            if (duration <= 0.001f)
            {
                transform.position = targetPosition;
                _movementCoroutine = null;
                _movementRunner = null;
                onComplete?.Invoke();
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // SmoothStep horizontal interpolation
                float smoothT = t * t * (3f - 2f * t);
                Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, smoothT);

                // Parabolic vertical arc: 4 * h * t * (1 - t)
                float verticalOffset = 4f * arcHeight * t * (1f - t);
                currentPos.y += verticalOffset;

                transform.position = currentPos;
                yield return null;
            }

            transform.position = targetPosition;
            _movementCoroutine = null;
            _movementRunner = null;
            onComplete?.Invoke();
        }

        private void OnDisable()
        {
            SafeStopCoroutine(ref _movementCoroutine, ref _movementRunner);
            SafeStopCoroutine(ref _revealCoroutine, ref _revealRunner);
        }
    }
}
