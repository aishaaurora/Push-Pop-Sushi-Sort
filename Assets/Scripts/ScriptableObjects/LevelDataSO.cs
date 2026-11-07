using System;
using System.Collections.Generic;
using UnityEngine;
using SushiSort.Core;

namespace SushiSort.ScriptableObjects
{
    /// <summary>
    /// Serializable data describing the initial configuration of a single tube in a level.
    /// </summary>
    [Serializable]
    public class TubeInitialData
    {
        [Tooltip("Stack order from index 0 (bottom-most) to n-1 (top-most). Maximum capacity defined by LevelDataSO.")]
        [SerializeField] private List<SushiType> sushiStack = new List<SushiType>();

        [Tooltip("Indicates whether this tube is locked when the level begins.")]
        [SerializeField] private bool isLocked = false;

        [Tooltip("The sushi type that must be fully sorted to unlock this tube if isLocked is true.")]
        [SerializeField] private SushiType unlockRequirement = SushiType.None;

        [Tooltip("Number of sushi pieces at the bottom of this tube that are hidden / mystery rolls.")]
        [SerializeField] private int mysteryCount = 0;

        // Public Getters (Strict Encapsulation)
        public IReadOnlyList<SushiType> SushiStack => sushiStack;
        public bool IsLocked => isLocked;
        public SushiType UnlockRequirement => unlockRequirement;
        public int MysteryCount => mysteryCount;

        // Convenience Helper Properties
        public bool IsEmpty => sushiStack == null || sushiStack.Count == 0;
        public int SushiCount => sushiStack != null ? sushiStack.Count : 0;

        public TubeInitialData()
        {
            sushiStack = new List<SushiType>();
            isLocked = false;
            unlockRequirement = SushiType.None;
            mysteryCount = 0;
        }

        public TubeInitialData(List<SushiType> stack, bool locked = false, SushiType requirement = SushiType.None, int mystery = 0)
        {
            sushiStack = stack ?? new List<SushiType>();
            isLocked = locked;
            unlockRequirement = requirement;
            mysteryCount = Mathf.Clamp(mystery, 0, sushiStack.Count);
        }
    }

    /// <summary>
    /// Configuration data asset for a single level in Push Pop Sushi Sort.
    /// Contains all initial tube layouts, capacities, and progression parameters.
    /// Includes integrated procedural reverse-shuffle parameters for instant editor generation.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "SushiSort/Level Data", order = 2)]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Level Settings")]
        [Tooltip("The sequential level number (1-based index).")]
        [SerializeField] private int levelNumber = 1;

        [Tooltip("Maximum number of sushi pieces a tube can hold in this level.")]
        [Range(1, 8)]
        [SerializeField] private int maxTubeCapacity = 4;

        [Header("Auto Generator Settings")]
        [Tooltip("Total sushi color variants (1 color = 4 sushi pieces). Minimum 2 for tutorial levels.")]
        [Range(2, 6)]
        [SerializeField] public int colorCount = 2;

        [Tooltip("Number of empty buffer tubes for player movement.")]
        [Range(1, 3)]
        [SerializeField] public int emptyBufferTubes = 2;

        [Tooltip("How many tubes have hidden mystery sushi rolls at the bottom.")]
        [Range(0, 3)]
        [SerializeField] public int mysteryTubeCount = 0;

        [Tooltip("How many tubes are locked at level start.")]
        [Range(0, 2)]
        [SerializeField] public int lockedTubeCount = 0;

        [Tooltip("Number of backward simulation shuffle steps.")]
        [Range(20, 100)]
        [SerializeField] public int shuffleSteps = 40;

        [Header("Tubes Configuration")]
        [Tooltip("List of initial tube setups for this level, including empty and locked tubes.")]
        [SerializeField] private List<TubeInitialData> tubes = new List<TubeInitialData>();

        // Public Getters (Strict Encapsulation)
        public int LevelNumber => levelNumber;
        public int MaxTubeCapacity => maxTubeCapacity;
        public IReadOnlyList<TubeInitialData> Tubes => tubes;
        public int TotalTubes => tubes != null ? tubes.Count : 0;

        /// <summary>
        /// Runtime initialization method for procedurally generated level data instances.
        /// </summary>
        public void InitializeRuntime(int levelNum, int maxCapacity, List<TubeInitialData> initialTubes)
        {
            levelNumber = levelNum;
            maxTubeCapacity = maxCapacity;
            tubes = initialTubes ?? new List<TubeInitialData>();
        }

        /// <summary>
        /// Context menu entry enabling right-click generation directly on asset in Inspector.
        /// </summary>
        [ContextMenu("Generate & Shuffle This Level")]
        public void GenerateFromContextMenu()
        {
            GenerateFromSettings(null);
        }

        /// <summary>
        /// Generates and reverse-shuffles tube data based on Auto Generator Settings,
        /// writing the resulting layout into this asset and saving changes permanently.
        /// </summary>
        /// <param name="database">Master sushi definition database. Auto-found in editor if null.</param>
        public void GenerateFromSettings(LevelDatabaseSO database = null)
        {
#if UNITY_EDITOR
            if (database == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LevelDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    database = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(path);
                }
            }
#endif

            // Clamp parameters to valid ranges
            colorCount = Mathf.Clamp(colorCount, 2, 6);
            emptyBufferTubes = Mathf.Clamp(emptyBufferTubes, 1, 3);

            // Generate solvable puzzle configuration using ProceduralLevelGenerator reverse shuffle
            List<TubeInitialData> generatedTubes = ProceduralLevelGenerator.GenerateShuffledTubeData(
                colorCount,
                emptyBufferTubes,
                shuffleSteps,
                mysteryTubeCount,
                lockedTubeCount,
                database
            );

            // Commit results
            tubes = generatedTubes;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[LevelDataSO] Successfully generated and shuffled Level {levelNumber} ({tubes.Count} tubes).", this);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (levelNumber < 1)
            {
                levelNumber = 1;
            }

            if (maxTubeCapacity < 1)
            {
                maxTubeCapacity = 4;
            }

            colorCount = Mathf.Clamp(colorCount, 2, 6);
            emptyBufferTubes = Mathf.Clamp(emptyBufferTubes, 1, 3);
            lockedTubeCount = Mathf.Clamp(lockedTubeCount, 0, 2);
            mysteryTubeCount = Mathf.Clamp(mysteryTubeCount, 0, 3);
            shuffleSteps = Mathf.Clamp(shuffleSteps, 20, 100);

            if (tubes == null) return;

            for (int i = 0; i < tubes.Count; i++)
            {
                var tube = tubes[i];
                if (tube == null) continue;

                if (tube.SushiCount > maxTubeCapacity)
                {
                    Debug.LogWarning($"[LevelDataSO] Level {levelNumber} - Tube {i} exceeds max capacity ({tube.SushiCount}/{maxTubeCapacity}).", this);
                }

                if (tube.MysteryCount > tube.SushiCount)
                {
                    Debug.LogWarning($"[LevelDataSO] Level {levelNumber} - Tube {i} has mystery count ({tube.MysteryCount}) greater than total sushi ({tube.SushiCount}).", this);
                }

                if (tube.IsLocked && tube.UnlockRequirement == SushiType.None)
                {
                    Debug.LogWarning($"[LevelDataSO] Level {levelNumber} - Tube {i} is locked but has no unlock requirement assigned.", this);
                }
            }
        }
#endif
    }
}
