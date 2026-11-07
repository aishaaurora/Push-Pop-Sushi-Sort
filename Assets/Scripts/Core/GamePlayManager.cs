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

        // Runtime State
        private GameState _currentState = GameState.SelectingSource;
        private PushPopTube _sourceTube;
        private SushiPiece _selectedPiece;
        private List<SushiPiece> _activeMovingGroup;
        private Coroutine _movementSequenceCoroutine;

        // Public Getters
        public GameState CurrentState => _currentState;
        public IReadOnlyList<PushPopTube> ActiveTubes => activeTubes;
        public LevelDataSO CurrentLevelData => currentLevelData;
        public int CurrentLevelNumber => currentLevelNumber;
        public bool IsHardLevel => (currentLevelNumber - 1) % 5 >= 3;
        public LevelDatabaseSO LevelDatabase => levelDatabase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            InputReader.OnTubeClicked += HandleTubeClicked;
        }

        private void OnDisable()
        {
            InputReader.OnTubeClicked -= HandleTubeClicked;
        }

        private void Start()
        {
            if (currentLevelData != null)
            {
                LoadLevel(currentLevelData);
            }
            else
            {
                StartLevel(currentLevelNumber);
            }
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
            if (targetLevel == null)
            {
                targetLevel = ProceduralLevelGenerator.GenerateLevel(levelNumber, levelDatabase);
            }

            LoadLevel(targetLevel);
        }

        /// <summary>
        /// Advances to and starts the next level.
        /// </summary>
        public void LoadNextLevel()
        {
            StartLevel(currentLevelNumber + 1);
        }

        /// <summary>
        /// Restarts the current active level.
        /// </summary>
        public void RestartCurrentLevel()
        {
            if (currentLevelData != null)
            {
                LoadLevel(currentLevelData);
            }
            else
            {
                StartLevel(currentLevelNumber);
            }
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
                OnLevelCompleted?.Invoke();
                Debug.Log("[GamePlayManager] Level Complete! All tubes sorted.");
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
    }
}

