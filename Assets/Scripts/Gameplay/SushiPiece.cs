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
        private Coroutine _peekCoroutine;
        private MonoBehaviour _peekRunner;

        // Cached original scale configured in prefab
        private Vector3 _originalMysteryScale = Vector3.one;
        private bool _isMysteryScaleCached = false;

        private void Awake()
        {
            CacheOriginalMysteryScale();
        }

        /// <summary>
        /// Reads and preserves the original mysteryWrapper localScale configured in the prefab before any hierarchy modifications.
        /// </summary>
        private void CacheOriginalMysteryScale()
        {
            if (_isMysteryScaleCached) return;

            if (mysteryWrapper == null)
            {
                Transform mysteryChild = transform.Find("MisteriSushi") 
                    ?? transform.Find("Mystery") 
                    ?? FindDeepChild(transform, "MisteriSushi") 
                    ?? FindDeepChild(transform, "Mystery");

                if (mysteryChild != null && mysteryChild.gameObject != gameObject)
                {
                    mysteryWrapper = mysteryChild.gameObject;
                }
            }

            if (mysteryWrapper != null)
            {
                _originalMysteryScale = mysteryWrapper.transform.localScale;
                // Jika skala terbaca 100 (atau kelipatan besar), normalisasi ke Vector3.one (skala 1 yang pas)
                if (_originalMysteryScale.x > 10f || _originalMysteryScale == Vector3.zero)
                {
                    _originalMysteryScale = Vector3.one;
                }
                _isMysteryScaleCached = true;
            }
        }

        /// <summary>
        /// Initializes the sushi piece type, mystery status, and visual materials.
        /// Preserves prefab transform scale and ensures proper visual rendering.
        /// </summary>
        public void Initialize(SushiType type, bool mystery, SushiDataSO data)
        {
            // 1. Pastikan root GameObject selalu aktif
            gameObject.SetActive(true);

            // 2. Simpan skala asli yang sudah diatur di prefab jika belum tercatat
            CacheOriginalMysteryScale();

            sushiType = type;
            isMystery = mystery;

            // 3. Auto-resolve visual references if missing, pointing to root, or nested
            ResolveVisualReferences();

            // 4. Pastikan mysteryWrapper mempertahankan skala asli prefab
            if (mysteryWrapper != null && _originalMysteryScale != Vector3.zero)
            {
                mysteryWrapper.transform.localScale = _originalMysteryScale;
            }

            // 5. Apply custom material if specified in SushiDataSO
            if (data != null && data.SushiMaterial != null && normalRenderer != null)
            {
                normalRenderer.sharedMaterial = data.SushiMaterial;
            }

            // 6. Configure visual states & renderers based on isMystery
            UpdateVisualState();
        }

        private void ResolveVisualReferences()
        {
            // 1. Identify mysteryWrapper child
            if (mysteryWrapper == null || mysteryWrapper == gameObject || !mysteryWrapper.transform.IsChildOf(transform))
            {
                Transform mysteryChild = transform.Find("MisteriSushi") 
                    ?? transform.Find("Mystery") 
                    ?? FindDeepChild(transform, "MisteriSushi") 
                    ?? FindDeepChild(transform, "Mystery");

                if (mysteryChild != null && mysteryChild.gameObject != gameObject)
                {
                    mysteryWrapper = mysteryChild.gameObject;
                    if (!_isMysteryScaleCached)
                    {
                        _originalMysteryScale = mysteryWrapper.transform.localScale;
                        if (_originalMysteryScale.x > 10f || _originalMysteryScale == Vector3.zero)
                        {
                            _originalMysteryScale = Vector3.one;
                        }
                        _isMysteryScaleCached = true;
                    }
                }
                else
                {
                    mysteryWrapper = null;
                }
            }

            // 2. Identify normalModel (the sushi roll mesh child that is NOT mysteryWrapper)
            if (normalModel == null || normalModel == gameObject || normalModel == mysteryWrapper)
            {
                foreach (Transform child in transform)
                {
                    if (mysteryWrapper != null && (child == mysteryWrapper.transform || child.IsChildOf(mysteryWrapper.transform)))
                        continue;
                    normalModel = child.gameObject;
                    break;
                }
            }

            // 3. Reparent mysteryWrapper directly under this root transform jika tersarang di dalam model lain
            // agar jika normalModel dinonaktifkan, mysteryWrapper tetap aktif
            if (mysteryWrapper != null)
            {
                if (mysteryWrapper.transform.parent != transform)
                {
                    mysteryWrapper.transform.SetParent(transform, false);
                }

                // Reset offset posisi dan rotasi lokal tepat di tengah
                mysteryWrapper.transform.localPosition = Vector3.zero;
                mysteryWrapper.transform.localRotation = Quaternion.identity;

                // Pertahankan skala asli prefab, jangan pernah mengalikannya dengan angka 100f atau me-reset sembarangan
                if (_originalMysteryScale != Vector3.zero)
                {
                    mysteryWrapper.transform.localScale = _originalMysteryScale;
                }

                // Clean up any stray SushiPiece script accidentally attached to mysteryWrapper
                if (mysteryWrapper.TryGetComponent<SushiPiece>(out var strayPiece) && strayPiece != this)
                {
                    strayPiece.enabled = false;
                    Destroy(strayPiece);
                }

                // Disable child collider so it doesn't conflict with root BoxCollider
                if (mysteryWrapper.TryGetComponent<Collider>(out var childCollider))
                {
                    childCollider.enabled = false;
                }
            }

            // 4. Resolve renderer references
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
                if (child.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0) return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Sets normal and mystery model active states based on current isMystery flag.
        /// Ensures correct renderers are active and visible from the first frame.
        /// </summary>
        public void UpdateVisualState()
        {
            if (isMystery)
            {
                // Jika isMystery == true:
                // 1. Tampilkan model penutup misteri
                if (mysteryWrapper != null && mysteryWrapper != gameObject)
                {
                    mysteryWrapper.SetActive(true);

                    // Pertahankan skala asli prefab secara stabil
                    if (_originalMysteryScale != Vector3.zero)
                    {
                        mysteryWrapper.transform.localScale = _originalMysteryScale;
                    }

                    // Pastikan seluruh mesh renderer mystery wrapper aktif dan terlihat
                    Renderer[] mysteryRenderers = mysteryWrapper.GetComponentsInChildren<Renderer>(true);
                    foreach (var mr in mysteryRenderers)
                    {
                        if (mr != null)
                        {
                            mr.gameObject.SetActive(true);
                            mr.enabled = true;
                        }
                    }
                }

                // 2. Sembunyikan model sushi aslinya
                if (normalModel != null && normalModel != gameObject && normalModel != mysteryWrapper)
                {
                    normalModel.SetActive(false);
                }

                // Nonaktifkan renderer normal sushi agar tidak overlap
                Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in allRenderers)
                {
                    if (r == null) continue;
                    if (mysteryWrapper != null && (r.gameObject == mysteryWrapper || r.transform.IsChildOf(mysteryWrapper.transform)))
                        continue;

                    r.enabled = false;
                }
            }
            else
            {
                // Jika isMystery == false:
                // 1. Sembunyikan mystery wrapper
                if (mysteryWrapper != null && mysteryWrapper != gameObject)
                {
                    mysteryWrapper.SetActive(false);
                }

                // 2. Tampilkan model asli
                if (normalModel != null && normalModel != gameObject)
                {
                    normalModel.SetActive(true);

                    Renderer[] normalRenderers = normalModel.GetComponentsInChildren<Renderer>(true);
                    foreach (var nr in normalRenderers)
                    {
                        if (nr != null)
                        {
                            nr.gameObject.SetActive(true);
                            nr.enabled = true;
                        }
                    }
                }

                // Pastikan seluruh renderer normal aktif dan menyala
                Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in allRenderers)
                {
                    if (r == null) continue;
                    if (mysteryWrapper != null && (r.gameObject == mysteryWrapper || r.transform.IsChildOf(mysteryWrapper.transform)))
                    {
                        r.enabled = false;
                        continue;
                    }

                    r.gameObject.SetActive(true);
                    r.enabled = true;
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

            // Stop any active peek coroutine and reset wrapper transform before official reveal
            SafeStopCoroutine(ref _peekCoroutine, ref _peekRunner);
            if (mysteryWrapper != null)
            {
                mysteryWrapper.transform.localPosition = Vector3.zero;
                mysteryWrapper.transform.localRotation = Quaternion.identity;
            }

            SafeStopCoroutine(ref _revealCoroutine, ref _revealRunner);
            _revealCoroutine = SafeStartCoroutine(AnimateRevealRoutine(onComplete), out _revealRunner);

            // Immediate fallback if coroutine could not be started
            if (_revealCoroutine == null)
            {
                if (revealParticle != null) revealParticle.Play();
                if (mysteryWrapper != null)
                {
                    mysteryWrapper.SetActive(false);
                    if (_originalMysteryScale != Vector3.zero)
                    {
                        mysteryWrapper.transform.localScale = _originalMysteryScale;
                    }
                }
                if (normalModel != null) normalModel.SetActive(true);
                isMystery = false;
                UpdateVisualState();
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Temporarily wiggles and opens/hides the mystery wrapper so the player can peek at the hidden sushi inside.
        /// Automatically restores the mystery wrapper after the specified duration if not officially revealed.
        /// </summary>
        public void TriggerPeek(float duration = 1.5f)
        {
            if (!isMystery || mysteryWrapper == null)
            {
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            SafeStopCoroutine(ref _peekCoroutine, ref _peekRunner);
            _peekCoroutine = SafeStartCoroutine(AnimatePeekRoutine(duration), out _peekRunner);
        }

        private IEnumerator AnimatePeekRoutine(float duration)
        {
            if (!isMystery || mysteryWrapper == null)
            {
                _peekCoroutine = null;
                _peekRunner = null;
                yield break;
            }

            Vector3 baseScale = _originalMysteryScale != Vector3.zero
                ? _originalMysteryScale
                : mysteryWrapper.transform.localScale;

            mysteryWrapper.SetActive(true);
            mysteryWrapper.transform.localPosition = Vector3.zero;
            mysteryWrapper.transform.localRotation = Quaternion.identity;
            mysteryWrapper.transform.localScale = baseScale;

            // 1. Wiggle / shake the mystery wrapper briefly before opening
            float wiggleDuration = 0.25f;
            float wiggleElapsed = 0f;
            while (wiggleElapsed < wiggleDuration)
            {
                if (!isMystery || mysteryWrapper == null)
                {
                    _peekCoroutine = null;
                    _peekRunner = null;
                    yield break;
                }

                wiggleElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(wiggleElapsed / wiggleDuration);
                float damp = 1f - (t * 0.3f);
                float angleZ = Mathf.Sin(wiggleElapsed * 65f) * 14f * damp;
                float angleY = Mathf.Cos(wiggleElapsed * 50f) * 10f * damp;
                mysteryWrapper.transform.localRotation = Quaternion.Euler(0f, angleY, angleZ);
                mysteryWrapper.transform.localPosition = new Vector3(Mathf.Sin(wiggleElapsed * 70f) * 0.04f, 0f, 0f);
                yield return null;
            }

            if (!isMystery || mysteryWrapper == null)
            {
                _peekCoroutine = null;
                _peekRunner = null;
                yield break;
            }

            // 2. Enable underlying sushi model & renderers so the hidden piece is visible during peek
            SetNormalSushiVisibleForPeek(true);

            // Lift and shrink/open wrapper upward so the sushi inside can be clearly seen
            float openDuration = 0.16f;
            float openElapsed = 0f;
            Vector3 peekLiftOffset = new Vector3(0f, 0.38f, 0f);
            Vector3 peekScale = new Vector3(baseScale.x * 0.85f, baseScale.y * 0.35f, baseScale.z * 0.85f);

            while (openElapsed < openDuration)
            {
                if (!isMystery || mysteryWrapper == null)
                {
                    _peekCoroutine = null;
                    _peekRunner = null;
                    yield break;
                }

                openElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(openElapsed / openDuration);
                float smoothT = t * t * (3f - 2f * t);
                mysteryWrapper.transform.localPosition = Vector3.Lerp(Vector3.zero, peekLiftOffset, smoothT);
                mysteryWrapper.transform.localScale = Vector3.Lerp(baseScale, peekScale, smoothT);
                mysteryWrapper.transform.localRotation = Quaternion.identity;
                yield return null;
            }

            // Hide wrapper while peeking so the sushi piece underneath is fully visible
            if (isMystery && mysteryWrapper != null)
            {
                mysteryWrapper.SetActive(false);
            }

            // 3. Hold peek for duration seconds (abort early if officially revealed during gameplay)
            float waitElapsed = 0f;
            while (waitElapsed < duration)
            {
                if (!isMystery)
                {
                    _peekCoroutine = null;
                    _peekRunner = null;
                    yield break;
                }

                waitElapsed += Time.deltaTime;
                yield return null;
            }

            // 4. Close wrapper back if still in mystery state
            if (isMystery && mysteryWrapper != null)
            {
                mysteryWrapper.SetActive(true);

                float closeDuration = 0.16f;
                float closeElapsed = 0f;
                while (closeElapsed < closeDuration)
                {
                    if (!isMystery || mysteryWrapper == null)
                    {
                        _peekCoroutine = null;
                        _peekRunner = null;
                        yield break;
                    }

                    closeElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(closeElapsed / closeDuration);
                    float smoothT = t * t * (3f - 2f * t);
                    mysteryWrapper.transform.localPosition = Vector3.Lerp(peekLiftOffset, Vector3.zero, smoothT);
                    mysteryWrapper.transform.localScale = Vector3.Lerp(peekScale, baseScale, smoothT);
                    yield return null;
                }

                if (isMystery && mysteryWrapper != null)
                {
                    mysteryWrapper.transform.localPosition = Vector3.zero;
                    mysteryWrapper.transform.localRotation = Quaternion.identity;
                    mysteryWrapper.transform.localScale = baseScale;
                    UpdateVisualState();
                }
            }

            _peekCoroutine = null;
            _peekRunner = null;
        }

        private void SetNormalSushiVisibleForPeek(bool visible)
        {
            if (normalModel != null && normalModel != gameObject && normalModel != mysteryWrapper)
            {
                normalModel.SetActive(visible);
            }

            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null) continue;
                if (mysteryWrapper != null && (r.gameObject == mysteryWrapper || r.transform.IsChildOf(mysteryWrapper.transform)))
                    continue;

                if (visible)
                {
                    r.gameObject.SetActive(true);
                }
                r.enabled = visible;
            }
        }

        private IEnumerator AnimateRevealRoutine(Action onComplete)
        {
            // 1. Mainkan efek partikel jika ada
            if (revealParticle != null)
            {
                revealParticle.Play();
            }

            // 2. Sembunyikan mysteryWrapper dengan animasi shrink/dissolve
            if (mysteryWrapper != null)
            {
                Vector3 originalScale = _originalMysteryScale != Vector3.zero ? _originalMysteryScale : mysteryWrapper.transform.localScale;
                Quaternion originalRot = mysteryWrapper.transform.localRotation;
                float elapsed = 0f;
                float duration = 0.22f;

                // Punch scale up slightly, then shrink to 0
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    // Easing scale out
                    mysteryWrapper.transform.localScale = Vector3.Lerp(originalScale * 1.25f, Vector3.zero, t);
                    mysteryWrapper.transform.Rotate(Vector3.up, 360f * Time.deltaTime * 2f, Space.Self);

                    yield return null;
                }

                mysteryWrapper.SetActive(false);
                mysteryWrapper.transform.localScale = originalScale;
                mysteryWrapper.transform.localRotation = originalRot;
            }

            // 3. Tampilkan normalModel dan update state
            if (normalModel != null)
            {
                normalModel.SetActive(true);
            }

            isMystery = false;
            UpdateVisualState();

            // 4. Jalankan animasi punch scale pada normalModel agar transisi juicy
            Transform popTarget = normalModel != null ? normalModel.transform : transform;
            Vector3 origScale = popTarget.localScale;
            float popElapsed = 0f;
            float popDuration = 0.20f;

            while (popElapsed < popDuration)
            {
                popElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(popElapsed / popDuration);
                float bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                popTarget.localScale = origScale * bump;
                yield return null;
            }

            popTarget.localScale = origScale;

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
            SafeStopCoroutine(ref _peekCoroutine, ref _peekRunner);
        }
    }
}
