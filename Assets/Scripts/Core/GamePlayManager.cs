using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SushiSort.Gameplay;
using SushiSort.ScriptableObjects;

namespace SushiSort.Core
{
    /// <summary>
    /// Game state enum representing high-level puzzle interaction states.
    /// </summary>
    public enum GameState
    {
        SelectingSource,
        SelectingDestination,
        MovingPiece,
        GameOver,
        LevelCompleted
    }

    /// <summary>
    /// Core gameplay manager governing puzzle rules, selection states, tube interactions,
    /// unlock conditions, and victory evaluations.
    /// </summary>
    public class GamePlayManager : MonoBehaviour
    {
        public static GamePlayManager Instance { get; private set; }

        [Header("Databases & Data")]
        [SerializeField] private LevelDatabaseSO levelDatabase;
        [SerializeField] private LevelDataSO currentLevelData;
        [SerializeField] private int currentLevelNumber = 1;

        // PlayerPrefs Storage Key (WebGL Safe)
        private const string SAVE_LEVEL_KEY = "SushiSort_CurrentLevel";
        public const int BOOSTER_COST = 30;
        public const int WIN_COIN_REWARD = 10;

        [Header("Scene References")]
        [Tooltip("Optional dynamic spawner for instantiating and layouting tubes.")]
        [SerializeField] private LevelSpawner levelSpawner;

        [Tooltip("Active tubes in the puzzle scene (populated manually or via LevelSpawner).")]
        [SerializeField] private List<PushPopTube> activeTubes = new List<PushPopTube>();

        [Tooltip("Optional parent container for spawned sushi game objects.")]
        [SerializeField] private Transform sushiContainer;

        [Header("Animation Timings")]
        [SerializeField] private float travelDuration = 0.28f;
        [SerializeField] private float dropDuration = 0.18f;
        [SerializeField] private float arcJumpHeight = 1.0f;

        [Tooltip("Duration in seconds that mystery sushi stays open when device shake peek is triggered.")]
        [SerializeField] private float mysteryPeekDuration = 1.5f;

        // Static Events (Decoupled Architecture)
        public static event Action OnLevelStarted;
        public static event Action OnPiecePopped;
        public static event Action OnPieceDropped;
        public static event Action OnMoveSuccess;
        public static event Action OnInvalidMove;
        public static event Action OnLevelCompleted;
        public static event Action OnGameOver;
        public static event Action OnDeadlockDetected;
        public static event Action<SushiType> OnTubeCompleted;
        public static event Action OnTubeUnlocked;
        public static event Action OnBoosterUsed;

        // Runtime State
        private GameState _currentState = GameState.SelectingSource;
        private PushPopTube _sourceTube;
        private SushiPiece _selectedPiece;
        private List<SushiPiece> _activeMovingGroup;
        private Coroutine _movementSequenceCoroutine;
        private bool _isBoosterUsedThisLevel = false;

        // Public Getters
        public GameState CurrentState => _currentState;
        public IReadOnlyList<PushPopTube> ActiveTubes => activeTubes;
        public LevelDataSO CurrentLevelData => currentLevelData;
        public int CurrentLevelNumber => currentLevelNumber;
        public int CurrentLevelIndex => currentLevelNumber;
        public bool IsHardLevel => (currentLevelNumber - 1) % 5 >= 3;
        public LevelDatabaseSO LevelDatabase => levelDatabase;
        public bool IsBoosterUsedThisLevel => _isBoosterUsedThisLevel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (FindObjectOfType<ShakeInputDetector>() == null)
            {
                gameObject.AddComponent<ShakeInputDetector>();
            }

            LoadProgress();
        }

        /// <summary>
        /// Loads saved level progress from PlayerPrefs. Defaults to Level 1 if no save exists.
        /// </summary>
        public void LoadProgress()
        {
            currentLevelNumber = PlayerPrefs.GetInt(SAVE_LEVEL_KEY, 1);
            Debug.Log($"[GamePlayManager] LoadProgress: Loaded Level {currentLevelNumber} from PlayerPrefs.");
        }

        /// <summary>
        /// Saves progress to PlayerPrefs and immediately flushes via PlayerPrefs.Save() for WebGL safety.
        /// </summary>
        /// <param name="levelIndex">The 1-based level index to save.</param>
        public void SaveCurrentLevel(int levelIndex)
        {
            PlayerPrefs.SetInt(SAVE_LEVEL_KEY, levelIndex);
            PlayerPrefs.Save(); // Wajib dipanggil eksplisit agar aman di WebGL
            Debug.Log($"[GamePlayManager] SaveProgress: PlayerPrefs key '{SAVE_LEVEL_KEY}' saved with Level {levelIndex}.");
        }

#if UNITY_EDITOR
        [ContextMenu("Reset Save Progress (Level 1)")]
        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(SAVE_LEVEL_KEY);
            PlayerPrefs.Save();
            currentLevelNumber = 1;
            Debug.Log("[GamePlayManager] Save progress reset to Level 1.");
        }
#endif

        private void OnEnable()
        {
            InputReader.OnTubeClicked += HandleTubeClicked;
            ShakeInputDetector.OnShakeTriggered += HandleShakeTriggered;
        }

        private void OnDisable()
        {
            InputReader.OnTubeClicked -= HandleTubeClicked;
            ShakeInputDetector.OnShakeTriggered -= HandleShakeTriggered;
        }

        private void Start()
        {
            StartLevel(currentLevelNumber);
        }

        /// <summary>
        /// Starts a specific level. Loads from curated levels (1..10) if available,
        /// or generates a 100% solvable procedural level (11+).
        /// </summary>
        /// <param name="levelNumber">1-based level index.</param>
        public void StartLevel(int levelNumber)
        {
            currentLevelNumber = levelNumber;

            LevelDataSO targetLevel = null;

            // Check if level exists in curated levels
            if (levelDatabase != null && levelNumber <= levelDatabase.CuratedLevelCount)
            {
                targetLevel = levelDatabase.GetCuratedLevel(levelNumber - 1);
            }

            // Procedural generation fallback for Level 11+ or missing curated levels
            if (targetLevel == null && levelDatabase != null)
            {
                targetLevel = ProceduralLevelGenerator.GenerateLevel(levelNumber, levelDatabase);
            }

            if (targetLevel == null)
            {
                targetLevel = currentLevelData;
            }

            LoadLevel(targetLevel);
        }

        /// <summary>
        /// Advances to and starts the next level, ensuring progress is saved.
        /// </summary>
        public void LoadNextLevel()
        {
            int nextLevel = currentLevelNumber + 1;
            SaveCurrentLevel(nextLevel);
            StartLevel(nextLevel);
        }

        /// <summary>
        /// Restarts the current active level.
        /// </summary>
        public void RestartCurrentLevel()
        {
            StartLevel(currentLevelNumber);
        }

        /// <summary>
        /// Loads and initializes tubes according to the specified LevelDataSO.
        /// </summary>
        public void LoadLevel(LevelDataSO levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("[GamePlayManager] LevelDataSO is null.");
                return;
            }

            currentLevelData = levelData;
            _currentState = GameState.SelectingSource;
            _sourceTube = null;
            _activeMovingGroup = null;
            _selectedPiece = null;
            _isBoosterUsedThisLevel = false;

            if (_movementSequenceCoroutine != null)
            {
                StopCoroutine(_movementSequenceCoroutine);
                _movementSequenceCoroutine = null;
            }

            if (levelDatabase == null)
            {
                Debug.LogError("[GamePlayManager] LevelDatabaseSO reference is missing!");
                return;
            }

            // If LevelSpawner is attached, dynamically spawn and arrange tubes
            if (levelSpawner != null)
            {
                activeTubes = levelSpawner.SpawnLevel(levelData, levelDatabase, sushiContainer);
            }
            else
            {
                // Fallback: Initialize pre-placed tubes in scene
                for (int i = 0; i < activeTubes.Count; i++)
                {
                    var tube = activeTubes[i];
                    if (tube == null) continue;

                    if (i < levelData.Tubes.Count)
                    {
                        tube.gameObject.SetActive(true);
                        tube.Initialize(levelData.Tubes[i], levelDatabase, sushiContainer);
                    }
                    else
                    {
                        // Deactivate extra tubes not used in this level
                        tube.gameObject.SetActive(false);
                    }
                }
            }

            OnLevelStarted?.Invoke();
        }

        /// <summary>
        /// Attempts to purchase and spawn the "+1 Empty Tube" booster for 30 coins.
        /// Can only be used once per level.
        /// </summary>
        public bool TryPurchaseExtraTubeBooster()
        {
            if (_isBoosterUsedThisLevel)
            {
                Debug.Log("[GamePlayManager] Booster already used in this level.");
                return false;
            }

            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendCoins(BOOSTER_COST))
            {
                return false;
            }

            // Drop any currently selected / elevated pieces back before re-centering tubes
            if (_currentState == GameState.SelectingDestination)
            {
                CancelSelection();
            }

            if (levelSpawner != null)
            {
                levelSpawner.SpawnExtraEmptyTube(levelDatabase, sushiContainer, activeTubes);
            }
            else if (activeTubes != null && activeTubes.Count > 0 && activeTubes[0] != null)
            {
                // Fallback if LevelSpawner is not assigned: clone existing tube
                PushPopTube template = activeTubes[0];
                PushPopTube extraTube = Instantiate(template, template.transform.position, Quaternion.identity, template.transform.parent);
                extraTube.gameObject.SetActive(true);
                extraTube.name = $"Tube_{activeTubes.Count:00}_Booster";
                extraTube.Initialize(new TubeInitialData(new List<SushiType>(), false, SushiType.None, 0), levelDatabase, sushiContainer);
                activeTubes.Add(extraTube);
            }

            _isBoosterUsedThisLevel = true;

            if (_currentState == GameState.GameOver)
            {
                _currentState = GameState.SelectingSource;
            }

            OnBoosterUsed?.Invoke();
            Debug.Log("[GamePlayManager] +1 Empty Tube Booster activated and layout re-centered.");
            return true;
        }

        /// <summary>
        /// Triggered when ShakeInputDetector detects a device shake, rapid mouse shake, or keyboard shortcut.
        /// Temporarily wiggles and peeks all unrevealed mystery sushi pieces across active tubes.
        /// </summary>
        private void HandleShakeTriggered()
        {
            if (_currentState == GameState.GameOver || _currentState == GameState.LevelCompleted)
            {
                return;
            }

            if (activeTubes == null || activeTubes.Count == 0)
            {
                return;
            }

            int totalPeeked = 0;
            for (int i = 0; i < activeTubes.Count; i++)
            {
                PushPopTube tube = activeTubes[i];
                if (tube == null || !tube.gameObject.activeInHierarchy) continue;

                totalPeeked += tube.TriggerMysteryPeek(mysteryPeekDuration);
            }

            if (totalPeeked > 0)
            {
                Debug.Log($"[GamePlayManager] Mystery Peek activated on {totalPeeked} mystery sushi piece(s).");
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                if (Application.isMobilePlatform)
                {
                    Handheld.Vibrate();
                }
#endif
            }
        }

        /// <summary>
        /// Processes player interaction when a tube is clicked.
        /// </summary>
        public void HandleTubeClicked(PushPopTube clickedTube)
        {
            if (clickedTube == null) return;

            // Only accept input when in selection states
            if (_currentState != GameState.SelectingSource && _currentState != GameState.SelectingDestination)
            {
                return;
            }

            if (_currentState == GameState.SelectingSource)
            {
                ProcessSourceSelection(clickedTube);
            }
            else if (_currentState == GameState.SelectingDestination)
            {
                ProcessDestinationSelection(clickedTube);
            }
        }

        private void ProcessSourceSelection(PushPopTube tube)
        {
            // Validate source: cannot be locked, cannot be empty
            if (tube.IsLocked || tube.IsEmpty)
            {
                tube.ShakeInvalid();
                OnInvalidMove?.Invoke();
                return;
            }

            _sourceTube = tube;
            _activeMovingGroup = _sourceTube.GetTopMatchingGroup();

            if (_activeMovingGroup == null || _activeMovingGroup.Count == 0)
            {
                _sourceTube = null;
                _activeMovingGroup = null;
                return;
            }

            _selectedPiece = _activeMovingGroup[0];

            // Elevate the entire consecutive matching sushi group
            _sourceTube.ElevateMatchingGroup(_activeMovingGroup);
            _currentState = GameState.SelectingDestination;
            OnPiecePopped?.Invoke();
        }

        private void ProcessDestinationSelection(PushPopTube destinationTube)
        {
            // Case 1: Clicking the exact same tube cancels selection
            if (destinationTube == _sourceTube)
            {
                CancelSelection();
                return;
            }

            // Case 2: Validate destination rules
            if (IsValidMove(_sourceTube, destinationTube))
            {
                int availableSpace = destinationTube.AvailableCapacity;
                int groupCount = (_activeMovingGroup != null && _activeMovingGroup.Count > 0) ? _activeMovingGroup.Count : 1;
                int moveCount = Mathf.Min(groupCount, availableSpace);

                if (moveCount <= 0)
                {
                    destinationTube.ShakeInvalid();
                    CancelSelection();
                    OnInvalidMove?.Invoke();
                    return;
                }

                _currentState = GameState.MovingPiece;
                _movementSequenceCoroutine = StartCoroutine(ExecuteMultiMoveRoutine(_sourceTube, destinationTube, _activeMovingGroup, moveCount));
            }
            else
            {
                // Invalid move: shake target, drop source pieces back, notify listeners
                destinationTube.ShakeInvalid();
                CancelSelection();
                OnInvalidMove?.Invoke();
            }
        }

        /// <summary>
        /// Validates whether a move from source to destination is legally permissible.
        /// </summary>
        public bool IsValidMove(PushPopTube source, PushPopTube destination)
        {
            if (source == null || destination == null) return false;
            if (source.IsEmpty || source.IsLocked) return false;
            if (destination.IsLocked || destination.IsFull) return false;

            SushiPiece sourcePiece = source.PeekTop();
            if (sourcePiece == null) return false;

            // Destination can accept piece if it is empty OR top sushi type matches
            if (destination.IsEmpty)
            {
                return true;
            }

            SushiPiece destinationTop = destination.PeekTop();
            if (destinationTop == null) return false;

            // Types must match to stack
            return destinationTop.SushiType == sourcePiece.SushiType;
        }

        /// <summary>
        /// Executes procedural movement sequence for chain / combo moves.
        /// Pops moveCount pieces from source and moves them in a smooth staggered flow into destination slots.
        /// </summary>
        private IEnumerator ExecuteMultiMoveRoutine(
            PushPopTube source, 
            PushPopTube destination, 
            List<SushiPiece> movingGroup, 
            int moveCount)
        {
            if (source == null || destination == null || movingGroup == null || moveCount <= 0)
            {
                _currentState = GameState.SelectingSource;
                yield break;
            }

            // 1. If any elevated pieces cannot fit in destination, drop them back to source rest slots immediately
            if (moveCount < movingGroup.Count)
            {
                for (int k = moveCount; k < movingGroup.Count; k++)
                {
                    var leftoverPiece = movingGroup[k];
                    if (leftoverPiece == null) continue;

                    int slotIdx = source.CurrentCount - 1 - k;
                    if (slotIdx >= 0)
                    {
                        leftoverPiece.MoveTo(source.GetSlotPosition(slotIdx), dropDuration);
                        leftoverPiece.transform.rotation = source.GetSlotRotation(slotIdx);
                    }
                }
            }

            // 2. Pop moveCount pieces from source stack in LIFO order
            List<SushiPiece> piecesToMove = new List<SushiPiece>(moveCount);
            for (int i = 0; i < moveCount; i++)
            {
                var p = source.PopSushi();
                if (p != null)
                {
                    piecesToMove.Add(p);
                }
            }

            Vector3 destHover = destination.HoverPoint != null 
                ? destination.HoverPoint.position 
                : destination.transform.position + Vector3.up * 2.2f;

            // 3. Move pieces to destination sequentially with smooth staggered chain flow
            for (int i = 0; i < piecesToMove.Count; i++)
            {
                SushiPiece piece = piecesToMove[i];
                if (piece == null) continue;

                // Arc jump from source hover to destination hover
                bool arcFinished = false;
                piece.MoveAlongArc(destHover, arcJumpHeight, travelDuration, () => arcFinished = true);
                while (!arcFinished) yield return null;

                // Drop into target slot
                int targetSlotIndex = destination.CurrentCount;
                Vector3 targetSlotPos = destination.GetSlotPosition(targetSlotIndex);

                bool dropFinished = false;
                piece.MoveTo(targetSlotPos, dropDuration, () => dropFinished = true);
                while (!dropFinished) yield return null;

                // Register piece inside destination stack (parents with worldPositionStays: true)
                destination.PushSushi(piece);
                OnPieceDropped?.Invoke();

                if (i < piecesToMove.Count - 1)
                {
                    yield return new WaitForSeconds(0.04f);
                }
            }

            // 4. Reveal mystery sushi on source tube if newly uncovered at the top
            if (!source.IsEmpty)
            {
                SushiPiece newSourceTop = source.PeekTop();
                if (newSourceTop != null && newSourceTop.IsMystery)
                {
                    bool revealFinished = false;
                    newSourceTop.RevealMystery(() => revealFinished = true);
                    while (!revealFinished) yield return null;
                }
            }

            // 5. Check if destination tube is completed
            if (destination.IsCompleted)
            {
                destination.PlayCompleteCelebration();
                SushiType completedType = destination.PeekTop().SushiType;
                OnTubeCompleted?.Invoke(completedType);

                // Check and unlock any dependent locked tubes
                UnlockDependentTubes(completedType);
            }

            OnMoveSuccess?.Invoke();

            // 6. Evaluate Level Completion & Deadlock Guard
            if (CheckLevelCompleted())
            {
                _currentState = GameState.LevelCompleted;
                int nextLevel = currentLevelNumber + 1;
                SaveCurrentLevel(nextLevel);
                CurrencyManager.Instance.AddCoins(WIN_COIN_REWARD);
                OnLevelCompleted?.Invoke();
                Debug.Log($"[GamePlayManager] Level Complete! All tubes sorted. +{WIN_COIN_REWARD} Coins awarded. Progress saved for Next Level: {nextLevel}");
            }
            else if (!HasAnyValidMove())
            {
                _currentState = GameState.GameOver;
                OnGameOver?.Invoke();
                OnDeadlockDetected?.Invoke();
                Debug.Log("[GamePlayManager] Deadlock detected! No more legal moves available.");
            }
            else
            {
                // Reset to source selection
                _sourceTube = null;
                _activeMovingGroup = null;
                _selectedPiece = null;
                _currentState = GameState.SelectingSource;
            }

            _movementSequenceCoroutine = null;
        }

        /// <summary>
        /// Backward-compatible single-move routine redirecting to ExecuteMultiMoveRoutine.
        /// </summary>
        private IEnumerator ExecuteMoveRoutine(PushPopTube source, PushPopTube destination)
        {
            var group = source != null ? source.GetTopMatchingGroup() : null;
            int count = (group != null && group.Count > 0) ? Mathf.Min(group.Count, destination.AvailableCapacity) : 1;
            return ExecuteMultiMoveRoutine(source, destination, group, count);
        }

        /// <summary>
        /// Unlocks any active tube whose unlock requirement matches the newly completed sushi type.
        /// </summary>
        private void UnlockDependentTubes(SushiType completedType)
        {
            if (completedType == SushiType.None) return;

            for (int i = 0; i < activeTubes.Count; i++)
            {
                var tube = activeTubes[i];
                if (tube != null && tube.IsLocked && tube.UnlockRequirement == completedType)
                {
                    tube.Unlock();
                    OnTubeUnlocked?.Invoke();
                    Debug.Log($"[GamePlayManager] Tube {i} unlocked by completed sushi type '{completedType}'!");
                }
            }
        }

        /// <summary>
        /// Evaluates whether all active non-empty tubes are completed and no tubes remain locked.
        /// </summary>
        private bool CheckLevelCompleted()
        {
            if (activeTubes == null || activeTubes.Count == 0) return false;

            int completedTubesCount = 0;
            int totalActiveTubes = 0;

            for (int i = 0; i < activeTubes.Count; i++)
            {
                var tube = activeTubes[i];
                if (tube == null || !tube.gameObject.activeInHierarchy) continue;

                totalActiveTubes++;

                // If any tube remains locked, level is not yet won
                if (tube.IsLocked) return false;

                // Non-empty tube must be fully completed
                if (!tube.IsEmpty)
                {
                    if (!tube.IsCompleted)
                    {
                        return false;
                    }
                    completedTubesCount++;
                }
            }

            // Level is won if there is at least one completed tube and no incomplete tubes
            return completedTubesCount > 0;
        }

        /// <summary>
        /// Evaluates whether any legally valid move remains between all active unlocked tubes.
        /// Returns false if the puzzle is completely deadlocked.
        /// </summary>
        public bool HasAnyValidMove()
        {
            if (activeTubes == null || activeTubes.Count == 0) return false;

            for (int i = 0; i < activeTubes.Count; i++)
            {
                var source = activeTubes[i];
                if (source == null || !source.gameObject.activeInHierarchy || source.IsLocked || source.IsEmpty)
                    continue;

                // Completed tubes are already sorted; moving pieces from them is unnecessary
                if (source.IsCompleted)
                    continue;

                for (int j = 0; j < activeTubes.Count; j++)
                {
                    if (i == j) continue;
                    var dest = activeTubes[j];
                    if (dest == null || !dest.gameObject.activeInHierarchy || dest.IsLocked || dest.IsFull)
                        continue;

                    if (IsValidMove(source, dest))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Cancels currently selected source tube and drops elevated sushi back.
        /// </summary>
        public void CancelSelection()
        {
            if (_sourceTube != null)
            {
                if (_activeMovingGroup != null && _activeMovingGroup.Count > 0)
                {
                    _sourceTube.DropMatchingGroup(_activeMovingGroup);
                }
                else
                {
                    _sourceTube.DropTopSushi();
                }
                _sourceTube = null;
            }

            _activeMovingGroup = null;
            _selectedPiece = null;
            _currentState = GameState.SelectingSource;
        }

        /// <summary>
        /// Stops active movement coroutines, resets selection state, and destroys all active tubes and sushi pieces
        /// so that returning to Main Menu or re-entering gameplay starts from a completely clean state.
        /// </summary>
        public void CleanupCurrentLevel()
        {
            // 1. Stop any active movement coroutines
            if (_movementSequenceCoroutine != null)
            {
                StopCoroutine(_movementSequenceCoroutine);
                _movementSequenceCoroutine = null;
            }
            StopAllCoroutines();

            // 2. Clean up any in-flight moving sushi group
            if (_activeMovingGroup != null)
            {
                for (int i = _activeMovingGroup.Count - 1; i >= 0; i--)
                {
                    if (_activeMovingGroup[i] != null)
                    {
                        Destroy(_activeMovingGroup[i].gameObject);
                    }
                }
                _activeMovingGroup.Clear();
                _activeMovingGroup = null;
            }

            // 3. Reset selection & state variables
            _sourceTube = null;
            _selectedPiece = null;
            _isBoosterUsedThisLevel = false;
            _currentState = GameState.SelectingSource;

            // 4. Destroy spawned tubes & sushi via LevelSpawner and activeTubes
            if (levelSpawner != null)
            {
                levelSpawner.ClearTubes();
            }

            if (activeTubes != null)
            {
                for (int i = activeTubes.Count - 1; i >= 0; i--)
                {
                    PushPopTube tube = activeTubes[i];
                    if (tube != null)
                    {
                        tube.ClearAllPieces();
                        if (levelSpawner != null)
                        {
                            Destroy(tube.gameObject);
                        }
                    }
                }

                if (levelSpawner != null)
                {
                    activeTubes.Clear();
                }
            }

            // 5. Destroy any remaining sushi objects in sushiContainer
            if (sushiContainer != null)
            {
                for (int i = sushiContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = sushiContainer.GetChild(i);
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}

