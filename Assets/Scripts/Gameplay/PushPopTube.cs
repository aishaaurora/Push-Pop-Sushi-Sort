using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SushiSort.Core;
using SushiSort.ScriptableObjects;

namespace SushiSort.Gameplay
{
    /// <summary>
    /// Manages an individual push-pop tube in the sorting puzzle.
    /// Handles stack operations, capacity restrictions, locked state, and slot positioning.
    /// Strictly positions sushi pieces at serialized Slot_0..Slot_3 transform positions and rotations.
    /// </summary>
    [SelectionBase]
    public class PushPopTube : MonoBehaviour
    {
        [Header("Capacity & Configuration")]
        [SerializeField] private int maxCapacity = 4;

        [Header("Transform Slots")]
        [Tooltip("Slot transforms from bottom (index 0) to top (index 3).")]
        [SerializeField] private Transform[] slotPositions = new Transform[4];

        [Tooltip("Position above the tube mouth where a selected sushi hovers.")]
        [SerializeField] private Transform hoverPoint;

        [Tooltip("The push-pop plunger stick mechanism at the bottom of the tube.")]
        [SerializeField] private Transform plungerStick;

        [Header("Visual Indicators")]
        [Tooltip("GameObject representing the lock / padlock graphic.")]
        [SerializeField] private GameObject lockVisual;

        [Tooltip("Renderer on child mesh AtlasSushi used to display the unlock requirement icon.")]
        [SerializeField] private Renderer lockRequirementRenderer;

        [Tooltip("Text or icon showing unlock requirement if locked.")]
        [SerializeField] private GameObject unlockRequirementIndicator;

        [Header("Celebration VFX & Bounce")]
        [Tooltip("Particle system triggered when completing this tube.")]
        [SerializeField] private ParticleSystem celebrationParticle;

        [Tooltip("Spawn point for completion particles. Fallbacks to hoverPoint if null.")]
        [SerializeField] private Transform effectSpawnPoint;

        [Tooltip("Enables juicy squish and stretch bounce animation upon completion.")]
        [SerializeField] private bool enablePunchScale = true;

        [SerializeField] private Vector3 punchScaleVector = new Vector3(0.12f, 0.2f, 0.12f);
        [SerializeField] private float punchDuration = 0.35f;

        [Tooltip("Legacy fallback reference for completion particle.")]
        [SerializeField] private ParticleSystem completeParticle;

        // Internal State
        private readonly Stack<SushiPiece> _sushiStack = new Stack<SushiPiece>();
        private Coroutine _shakeCoroutine;
        private Coroutine _plungerCoroutine;
        private Coroutine _celebrationCoroutine;
        private Vector3 _originalLocalPos;
        private Vector3 _plungerOriginalLocalPos;
        private LevelDatabaseSO _cachedDatabase;

        // Material Property Block for Atlas UV Offset
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        private MaterialPropertyBlock propBlock;

        // State Properties (Strict Encapsulation)
        public bool IsLocked { get; private set; }
        public SushiType UnlockRequirement { get; private set; }
        public int MaxCapacity => maxCapacity;
        public int CurrentCount => _sushiStack.Count;
        public int AvailableCapacity => Mathf.Max(0, maxCapacity - _sushiStack.Count);
        public bool IsEmpty => _sushiStack.Count == 0;
        public bool IsFull => _sushiStack.Count >= maxCapacity;
        public Transform HoverPoint => hoverPoint;

        /// <summary>
        /// True if the tube is completely filled to max capacity with identical, revealed sushi pieces.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                if (_sushiStack.Count != maxCapacity) return false;

                SushiType targetType = SushiType.None;
                foreach (var piece in _sushiStack)
                {
                    if (piece == null || piece.IsMystery) return false;

                    if (targetType == SushiType.None)
                    {
                        targetType = piece.SushiType;
                    }
                    else if (piece.SushiType != targetType)
                    {
                        return false;
                    }
                }

                return targetType != SushiType.None;
            }
        }

        private void Awake()
        {
            _originalLocalPos = transform.localPosition;
            if (plungerStick != null)
            {
                _plungerOriginalLocalPos = plungerStick.localPosition;
            }
            ValidateOrAutoBindSlots();

            if (effectSpawnPoint == null)
            {
                effectSpawnPoint = hoverPoint != null ? hoverPoint : transform;
            }

            if (celebrationParticle != null)
            {
                celebrationParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else if (completeParticle != null)
            {
                completeParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateOrAutoBindSlots();
        }
#endif

        /// <summary>
        /// Auto-binds slotPositions array and hoverPoint from child objects if unassigned.
        /// </summary>
        private void ValidateOrAutoBindSlots()
        {
            bool needsBinding = slotPositions == null || slotPositions.Length < maxCapacity;
            if (!needsBinding)
            {
                for (int i = 0; i < maxCapacity; i++)
                {
                    if (slotPositions[i] == null)
                    {
                        needsBinding = true;
                        break;
                    }
                }
            }

            if (needsBinding)
            {
                if (slotPositions == null || slotPositions.Length != maxCapacity)
                {
                    slotPositions = new Transform[maxCapacity];
                }

                Transform slotsContainer = transform.Find("Slots");
                for (int i = 0; i < maxCapacity; i++)
                {
                    if (slotPositions[i] == null)
                    {
                        string slotName = $"Slot_{i}";
                        Transform foundSlot = slotsContainer != null 
                            ? slotsContainer.Find(slotName) 
                            : transform.Find(slotName);

                        if (foundSlot == null)
                        {
                            foundSlot = FindDeepChild(transform, slotName);
                        }

                        if (foundSlot != null)
                        {
                            slotPositions[i] = foundSlot;
                        }
                    }
                }
            }

            if (hoverPoint == null)
            {
                hoverPoint = transform.Find("Hover Point") ?? FindDeepChild(transform, "Hover Point");
            }

            if (lockVisual == null)
            {
                lockVisual = (transform.Find("Locked") ?? transform.Find("LockVisual") ?? FindDeepChild(transform, "Locked") ?? FindDeepChild(transform, "LockVisual"))?.gameObject;
            }

            if (lockRequirementRenderer == null)
            {
                Transform atlasTransform = transform.Find("AtlasSushi") ?? FindDeepChild(transform, "AtlasSushi");
                if (atlasTransform != null)
                {
                    lockRequirementRenderer = atlasTransform.GetComponent<Renderer>();
                }
            }

            if (effectSpawnPoint == null)
            {
                effectSpawnPoint = hoverPoint != null ? hoverPoint : transform;
            }

            if (celebrationParticle == null)
            {
                celebrationParticle = GetComponentInChildren<ParticleSystem>(true);
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
        /// Initializes the tube with level data and spawns the starting sushi stack.
        /// Strictly positions sushi at slotPositions[i] and parents them with worldPositionStays: true.
        /// </summary>
        public void Initialize(TubeInitialData data, LevelDatabaseSO database, Transform sushiParent = null)
        {
            if (data == null)
            {
                Debug.LogError("[PushPopTube] TubeInitialData is null during initialization.");
                return;
            }

            _cachedDatabase = database;
            ValidateOrAutoBindSlots();

            // Clean up existing pieces if any
            ClearAllPieces();

            // Set lock parameters and update atlas UV visual
            SetLockState(data.IsLocked, data.UnlockRequirement, database);

            // Populate initial stack
            if (data.SushiStack != null && database != null)
            {
                for (int i = 0; i < data.SushiStack.Count; i++)
                {
                    if (i >= maxCapacity)
                    {
                        Debug.LogWarning($"[PushPopTube] Initial sushi count ({data.SushiStack.Count}) exceeds tube capacity ({maxCapacity}). Truncating.");
                        break;
                    }

                    SushiType type = data.SushiStack[i];
                    bool isMystery = i < data.MysteryCount;
                    SushiDataSO sushiData = database.GetSushiData(type);

                    if (sushiData == null || sushiData.SushiPrefab == null)
                    {
                        Debug.LogWarning($"[PushPopTube] Missing SushiDataSO or prefab for type '{type}'.");
                        continue;
                    }

                    Transform targetSlot = (slotPositions != null && i < slotPositions.Length && slotPositions[i] != null) 
                        ? slotPositions[i] 
                        : GetSlotTransform(i);

                    Vector3 spawnPos = targetSlot != null ? targetSlot.position : GetSlotPosition(i);
                    Quaternion spawnRot = targetSlot != null ? targetSlot.rotation : GetSlotRotation(i);
                    Transform targetParent = targetSlot != null ? targetSlot : (sushiParent != null ? sushiParent : transform);

                    if (targetSlot != null && !targetSlot.gameObject.activeSelf)
                    {
                        targetSlot.gameObject.SetActive(true);
                    }

                    GameObject sushiObj = Instantiate(sushiData.SushiPrefab, spawnPos, spawnRot);
                    sushiObj.name = $"Sushi_{type}_{i}";
                    sushiObj.SetActive(true);

                    // Parent with worldPositionStays: true to avoid any parent scale distortion
                    sushiObj.transform.SetParent(targetParent, true);

                    if (!sushiObj.TryGetComponent<SushiPiece>(out var sushiPiece))
                    {
                        sushiPiece = sushiObj.AddComponent<SushiPiece>();
                    }

                    // Pastikan posisi dan rotasi di-set langsung ke koordinat slot
                    if (slotPositions != null && i < slotPositions.Length && slotPositions[i] != null)
                    {
                        sushiPiece.transform.position = slotPositions[i].position;
                        sushiPiece.transform.rotation = slotPositions[i].rotation;
                    }
                    else
                    {
                        sushiPiece.transform.position = spawnPos;
                        sushiPiece.transform.rotation = spawnRot;
                    }

                    sushiPiece.gameObject.SetActive(true);
                    sushiPiece.Initialize(type, isMystery, sushiData);
                    _sushiStack.Push(sushiPiece);
                }
            }
        }

        /// <summary>
        /// Inspects the top sushi without removing it.
        /// </summary>
        public SushiPiece PeekTop()
        {
            return _sushiStack.Count > 0 ? _sushiStack.Peek() : null;
        }

        /// <summary>
        /// Returns all consecutive sushi pieces at the top of this tube that share
        /// the exact same revealed SushiType, from top-most (index 0) downward.
        /// </summary>
        public List<SushiPiece> GetTopMatchingGroup()
        {
            List<SushiPiece> group = new List<SushiPiece>();
            if (_sushiStack.Count == 0) return group;

            SushiPiece[] pieces = _sushiStack.ToArray();
            SushiPiece top = pieces[0];
            if (top == null || top.IsMystery) return group;

            SushiType matchType = top.SushiType;

            for (int i = 0; i < pieces.Length; i++)
            {
                SushiPiece piece = pieces[i];
                if (piece == null || piece.IsMystery || piece.SushiType != matchType)
                {
                    break;
                }
                group.Add(piece);
            }

            return group;
        }

        /// <summary>
        /// Measures the vertical distance between adjacent slot transforms in world coordinates.
        /// </summary>
        public float GetSlotSpacing()
        {
            if (slotPositions != null && slotPositions.Length >= 2 && slotPositions[0] != null && slotPositions[1] != null)
            {
                return Vector3.Distance(slotPositions[0].position, slotPositions[1].position);
            }
            return 0.35f;
        }

        /// <summary>
        /// Elevates a chain of matching sushi pieces above the tube.
        /// The top piece floats at hoverPoint, with subsequent pieces suspended neatly underneath.
        /// </summary>
        public void ElevateMatchingGroup(List<SushiPiece> group, float duration = 0.18f)
        {
            if (group == null || group.Count == 0) return;

            Vector3 hoverBase = hoverPoint != null ? hoverPoint.position : transform.position + Vector3.up * 2.2f;
            float spacing = GetSlotSpacing();

            for (int i = 0; i < group.Count; i++)
            {
                SushiPiece piece = group[i];
                if (piece == null) continue;

                if (!piece.gameObject.activeInHierarchy)
                {
                    piece.gameObject.SetActive(true);
                }

                Vector3 targetPos = hoverBase - Vector3.up * (i * spacing);
                piece.MoveTo(targetPos, duration);
            }

            AnimatePlungerElevate();
        }

        /// <summary>
        /// Drops elevated matching sushi pieces back to their original slots if selection is cancelled.
        /// </summary>
        public void DropMatchingGroup(List<SushiPiece> group, float duration = 0.18f)
        {
            if (group == null || group.Count == 0) return;

            int totalCount = _sushiStack.Count;

            for (int i = 0; i < group.Count; i++)
            {
                SushiPiece piece = group[i];
                if (piece == null) continue;

                if (!piece.gameObject.activeInHierarchy)
                {
                    piece.gameObject.SetActive(true);
                }

                int slotIndex = totalCount - 1 - i;
                if (slotIndex >= 0)
                {
                    Vector3 restPos = GetSlotPosition(slotIndex);
                    Quaternion restRot = GetSlotRotation(slotIndex);

                    piece.MoveTo(restPos, duration);
                    piece.transform.rotation = restRot;
                }
            }

            ResetPlunger();
        }

        /// <summary>
        /// Removes and returns the top sushi piece from the stack.
        /// </summary>
        public SushiPiece PopSushi()
        {
            if (_sushiStack.Count == 0) return null;

            SushiPiece popped = _sushiStack.Pop();
            AnimatePlungerPop();
            return popped;
        }

        /// <summary>
        /// Removes and returns count pieces from the top of the stack in LIFO order (top-most first).
        /// </summary>
        public List<SushiPiece> PopSushiGroup(int count)
        {
            List<SushiPiece> popped = new List<SushiPiece>(count);
            int toPop = Mathf.Min(count, _sushiStack.Count);

            for (int i = 0; i < toPop; i++)
            {
                popped.Add(PopSushi());
            }

            return popped;
        }

        /// <summary>
        /// Pushes a sushi piece onto the stack, parenting it with worldPositionStays: true
        /// and snapping its position and rotation to the exact designated slot transform.
        /// </summary>
        public void PushSushi(SushiPiece sushi)
        {
            if (sushi == null) return;

            if (!sushi.gameObject.activeSelf)
            {
                sushi.gameObject.SetActive(true);
            }

            if (_sushiStack.Count >= maxCapacity)
            {
                Debug.LogError("[PushPopTube] Attempted to push sushi into a full tube!");
                return;
            }

            int slotIndex = _sushiStack.Count;
            Transform targetSlot = GetSlotTransform(slotIndex);
            Transform targetParent = targetSlot != null ? targetSlot : transform;

            // Strict parenting with worldPositionStays: true
            sushi.transform.SetParent(targetParent, true);
            sushi.transform.position = GetSlotPosition(slotIndex);
            sushi.transform.rotation = GetSlotRotation(slotIndex);

            _sushiStack.Push(sushi);
            AnimatePlungerPush();
        }

        /// <summary>
        /// Elevates the top sushi or matching group to the hover point when selected as source.
        /// </summary>
        public void ElevateTopSushi(float duration = 0.18f)
        {
            var group = GetTopMatchingGroup();
            if (group.Count > 0)
            {
                ElevateMatchingGroup(group, duration);
            }
        }

        /// <summary>
        /// Drops the elevated sushi or matching group back to its rest slot.
        /// </summary>
        public void DropTopSushi(float duration = 0.18f)
        {
            var group = GetTopMatchingGroup();
            if (group.Count > 0)
            {
                DropMatchingGroup(group, duration);
            }
        }

        /// <summary>
        /// Updates the texture atlas UV tiling and offset on the child mesh renderer (AtlasSushi)
        /// via MaterialPropertyBlock to display the required sushi icon.
        /// </summary>
        public void UpdateLockRequirementVisual(SushiType requirement, LevelDatabaseSO database)
        {
            if (lockRequirementRenderer == null)
            {
                Transform atlasTransform = transform.Find("AtlasSushi") ?? FindDeepChild(transform, "AtlasSushi");
                if (atlasTransform != null)
                {
                    lockRequirementRenderer = atlasTransform.GetComponent<Renderer>();
                }
            }

            if (lockRequirementRenderer == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            var sushiData = database != null ? database.GetSushiData(requirement) : null;

            // Default UV scale is (1, 1) because AtlasSushi.fbx mesh is already pre-unwrapped to a 0.25x0.25 cell (Tile 0).
            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;

            // Auto-calculate exact offset based on the 4x4 atlas layout:
            // Row 0 (top): Salmon(1)=(0, 0), Tuna(2)=(+0.25, 0), Tamago(3)=(+0.50, 0), Kappa(4)=(+0.75, 0)
            // Row 1:       Ebi(5)=(0, -0.25), California(6)=(+0.25, -0.25)
            switch (requirement)
            {
                case SushiType.Salmon:
                    offset = new Vector2(0f, 0f);
                    break;
                case SushiType.Tuna:
                    offset = new Vector2(0.25f, 0f);
                    break;
                case SushiType.Tamago:
                    offset = new Vector2(0.50f, 0f);
                    break;
                case SushiType.Kappa:
                    offset = new Vector2(0.75f, 0f);
                    break;
                case SushiType.Ebi:
                    offset = new Vector2(0f, -0.25f);
                    break;
                case SushiType.California:
                    offset = new Vector2(0.25f, -0.25f);
                    break;
                default:
                    offset = Vector2.zero;
                    break;
            }

            // Allow custom override from SushiDataSO if explicitly specified (ignoring invalid 0.25 scale)
            if (sushiData != null)
            {
                if (sushiData.UvScale != Vector2.zero && sushiData.UvScale != new Vector2(0.25f, 0.25f))
                {
                    scale = sushiData.UvScale;
                }
                if (sushiData.UvOffset != Vector2.zero)
                {
                    offset = sushiData.UvOffset;
                }
            }

            lockRequirementRenderer.gameObject.SetActive(true);
            lockRequirementRenderer.enabled = true;

            lockRequirementRenderer.GetPropertyBlock(propBlock);
            Vector4 st = new Vector4(scale.x, scale.y, offset.x, offset.y);
            propBlock.SetVector(BaseMapSt, st);
            propBlock.SetVector(MainTexSt, st);
            lockRequirementRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Configures lock state, updates visual lock graphics, and applies requirement UV offset.
        /// </summary>
        public void SetLockState(bool locked, SushiType requirement, LevelDatabaseSO database = null)
        {
            if (database != null)
            {
                _cachedDatabase = database;
            }

            IsLocked = locked;
            UnlockRequirement = requirement;

            if (lockVisual != null)
            {
                lockVisual.SetActive(locked);
            }

            if (unlockRequirementIndicator != null)
            {
                unlockRequirementIndicator.SetActive(locked && requirement != SushiType.None);
            }

            if (locked && requirement != SushiType.None)
            {
                var db = database ?? _cachedDatabase ?? GamePlayManager.Instance?.LevelDatabase;
                if (db != null)
                {
                    UpdateLockRequirementVisual(requirement, db);
                }
            }
        }

        /// <summary>
        /// Unlocks the tube and triggers unlock visual feedback.
        /// </summary>
        public void Unlock()
        {
            if (!IsLocked) return;

            IsLocked = false;
            UnlockRequirement = SushiType.None;

            if (unlockRequirementIndicator != null)
            {
                unlockRequirementIndicator.SetActive(false);
            }

            if (lockVisual != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(AnimateUnlockRoutine());
            }
            else if (lockVisual != null)
            {
                lockVisual.SetActive(false);
            }
        }

        private IEnumerator AnimateUnlockRoutine()
        {
            if (lockVisual == null) yield break;

            lockVisual.SetActive(true);
            Vector3 origScale = lockVisual.transform.localScale;
            float elapsed = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                lockVisual.transform.localScale = Vector3.Lerp(origScale * 1.3f, Vector3.zero, t);
                yield return null;
            }

            lockVisual.SetActive(false);
            lockVisual.transform.localScale = origScale;
        }

        /// <summary>
        /// Updates the tube's world position and refreshes its cached local base position.
        /// </summary>
        public void UpdateBasePosition(Vector3 worldPos)
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }
            transform.position = worldPos;
            _originalLocalPos = transform.localPosition;
        }

        /// <summary>
        /// Triggers invalid move shake animation on this tube.
        /// </summary>
        public void ShakeInvalid()
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                transform.localPosition = _originalLocalPos;
            }
            else
            {
                _originalLocalPos = transform.localPosition;
            }

            _shakeCoroutine = StartCoroutine(AnimateShakeRoutine());
        }

        private IEnumerator AnimateShakeRoutine()
        {
            float elapsed = 0f;
            float duration = 0.28f;
            float intensity = 0.12f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - Mathf.Clamp01(elapsed / duration);
                float xOffset = Mathf.Sin(elapsed * 45f) * intensity * damp;
                transform.localPosition = _originalLocalPos + new Vector3(xOffset, 0f, 0f);
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            _shakeCoroutine = null;
        }

        /// <summary>
        /// Plays particle system VFX celebration and triggers bouncy squish & stretch on the tube.
        /// </summary>
        public void PlayCompleteCelebration()
        {
            var particle = celebrationParticle != null ? celebrationParticle : completeParticle;
            if (particle != null)
            {
                if (effectSpawnPoint != null)
                {
                    particle.transform.position = effectSpawnPoint.position;
                }
                particle.Play();
            }

            if (enablePunchScale && gameObject.activeInHierarchy)
            {
                if (_celebrationCoroutine != null) StopCoroutine(_celebrationCoroutine);
                _celebrationCoroutine = StartCoroutine(AnimateBounceRoutine(punchDuration));
            }
        }

        /// <summary>
        /// Backward-compatible alias for PlayCompleteCelebration.
        /// </summary>
        public void TriggerCompleteCelebration() => PlayCompleteCelebration();

        private IEnumerator AnimateBounceRoutine(float duration = 0.35f)
        {
            Vector3 originalScale = transform.localScale;
            float elapsed = 0f;
            if (duration <= 0.001f) yield break;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float damp = 1f - t;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * damp;
                float scaleY = 1f + wave * punchScaleVector.y;
                float scaleXZ = 1f - wave * punchScaleVector.x;
                transform.localScale = new Vector3(originalScale.x * scaleXZ, originalScale.y * scaleY, originalScale.z * scaleXZ);
                yield return null;
            }

            transform.localScale = originalScale;
            _celebrationCoroutine = null;
        }

        #region Slot Position Helpers

        /// <summary>
        /// Strictly returns the world position of the designated slot transform.
        /// </summary>
        public Vector3 GetSlotPosition(int index)
        {
            if (slotPositions != null && index >= 0 && index < slotPositions.Length && slotPositions[index] != null)
            {
                return slotPositions[index].position;
            }

            Debug.LogError($"[PushPopTube] Missing slot position at index {index} on '{gameObject.name}'. Check Inspector slotPositions assignment!", this);
            return transform.position;
        }

        /// <summary>
        /// Strictly returns the world rotation of the designated slot transform.
        /// </summary>
        public Quaternion GetSlotRotation(int index)
        {
            if (slotPositions != null && index >= 0 && index < slotPositions.Length && slotPositions[index] != null)
            {
                return slotPositions[index].rotation;
            }

            return transform.rotation;
        }

        public Transform GetSlotTransform(int index)
        {
            if (slotPositions != null && index >= 0 && index < slotPositions.Length)
            {
                return slotPositions[index];
            }
            return null;
        }

        #endregion

        #region Plunger Stick Animations

        private void AnimatePlungerElevate()
        {
            if (plungerStick == null) return;
            MovePlunger(_plungerOriginalLocalPos + Vector3.up * 0.25f, 0.15f);
        }

        private void AnimatePlungerPop()
        {
            if (plungerStick == null) return;
            MovePlunger(_plungerOriginalLocalPos, 0.15f);
        }

        private void AnimatePlungerPush()
        {
            if (plungerStick == null) return;
            MovePlunger(_plungerOriginalLocalPos - Vector3.up * 0.15f, 0.12f, ResetPlunger);
        }

        private void ResetPlunger()
        {
            if (plungerStick == null) return;
            MovePlunger(_plungerOriginalLocalPos, 0.15f);
        }

        private void MovePlunger(Vector3 targetLocalPos, float duration, Action onComplete = null)
        {
            if (_plungerCoroutine != null) StopCoroutine(_plungerCoroutine);
            _plungerCoroutine = StartCoroutine(AnimatePlungerRoutine(targetLocalPos, duration, onComplete));
        }

        private IEnumerator AnimatePlungerRoutine(Vector3 targetPos, float duration, Action onComplete)
        {
            Vector3 startPos = plungerStick.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = t * t * (3f - 2f * t);
                plungerStick.localPosition = Vector3.Lerp(startPos, targetPos, smoothT);
                yield return null;
            }

            plungerStick.localPosition = targetPos;
            _plungerCoroutine = null;
            onComplete?.Invoke();
        }

        #endregion

        /// <summary>
        /// Triggers the peek animation on all unrevealed mystery sushi pieces inside this tube.
        /// Returns the number of mystery pieces triggered.
        /// </summary>
        public int TriggerMysteryPeek(float duration = 1.5f)
        {
            int peekedCount = 0;
            foreach (var piece in _sushiStack)
            {
                if (piece != null && piece.IsMystery)
                {
                    piece.TriggerPeek(duration);
                    peekedCount++;
                }
            }
            return peekedCount;
        }

        /// <summary>
        /// Stops any active animations on this tube and destroys all sushi pieces in its stack and slots.
        /// </summary>
        public void ClearAllPieces()
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }
            if (_plungerCoroutine != null)
            {
                StopCoroutine(_plungerCoroutine);
                _plungerCoroutine = null;
            }
            if (_celebrationCoroutine != null)
            {
                StopCoroutine(_celebrationCoroutine);
                _celebrationCoroutine = null;
            }

            while (_sushiStack.Count > 0)
            {
                var piece = _sushiStack.Pop();
                if (piece != null)
                {
                    Destroy(piece.gameObject);
                }
            }

            // Also clean up any lingering child pieces under slot transforms
            if (slotPositions != null)
            {
                for (int i = 0; i < slotPositions.Length; i++)
                {
                    if (slotPositions[i] == null) continue;
                    for (int c = slotPositions[i].childCount - 1; c >= 0; c--)
                    {
                        var child = slotPositions[i].GetChild(c);
                        if (child.GetComponent<SushiPiece>() != null)
                        {
                            Destroy(child.gameObject);
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            if (_plungerCoroutine != null) StopCoroutine(_plungerCoroutine);
            if (_celebrationCoroutine != null) StopCoroutine(_celebrationCoroutine);
            transform.localPosition = _originalLocalPos;
            transform.localScale = Vector3.one;
        }
    }
}
