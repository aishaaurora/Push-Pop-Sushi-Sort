using System.Collections.Generic;
using UnityEngine;
using SushiSort.Core;

namespace SushiSort.ScriptableObjects
{
    /// <summary>
    /// Master database holding references to all curated levels and sushi definition assets.
    /// Acts as the Single Source of Truth (SSOT) for level loading and sushi metadata resolution.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "SushiSort/Level Database", order = 3)]
    public class LevelDatabaseSO : ScriptableObject
    {
        [Header("Curated Levels")]
        [Tooltip("List of handcrafted levels (e.g., Level 1 to 10).")]
        [SerializeField] private List<LevelDataSO> curatedLevels = new List<LevelDataSO>();

        [Header("Sushi Definitions")]
        [Tooltip("List of all configured sushi types and their visual/prefab definitions.")]
        [SerializeField] private List<SushiDataSO> allSushiTypes = new List<SushiDataSO>();

        // Public Getters (Strict Encapsulation)
        public IReadOnlyList<LevelDataSO> CuratedLevels => curatedLevels;
        public IReadOnlyList<SushiDataSO> AllSushiTypes => allSushiTypes;
        public int CuratedLevelCount => curatedLevels != null ? curatedLevels.Count : 0;

        // Runtime dictionary cache for O(1) sushi lookup
        [System.NonSerialized]
        private Dictionary<SushiType, SushiDataSO> _sushiLookupCache;

        private void OnEnable()
        {
            RebuildLookupCache();
        }

        /// <summary>
        /// Retrieves a curated level by 0-based index.
        /// </summary>
        /// <param name="index">0-based index of the level in curatedLevels.</param>
        /// <returns>LevelDataSO if found; null if index is out of bounds.</returns>
        public LevelDataSO GetCuratedLevel(int index)
        {
            if (curatedLevels == null || curatedLevels.Count == 0)
            {
                Debug.LogWarning("[LevelDatabaseSO] No curated levels are assigned in the database.");
                return null;
            }

            if (index < 0 || index >= curatedLevels.Count)
            {
                Debug.LogWarning($"[LevelDatabaseSO] Level index {index} is out of bounds (Total: {curatedLevels.Count}).");
                return null;
            }

            return curatedLevels[index];
        }

        /// <summary>
        /// Retrieves a curated level by its 1-based level number (LevelNumber property).
        /// </summary>
        /// <param name="levelNumber">1-based level number.</param>
        /// <returns>LevelDataSO if matching level is found; null otherwise.</returns>
        public LevelDataSO GetLevelByNumber(int levelNumber)
        {
            if (curatedLevels == null || curatedLevels.Count == 0)
            {
                Debug.LogWarning("[LevelDatabaseSO] Curated levels list is empty.");
                return null;
            }

            for (int i = 0; i < curatedLevels.Count; i++)
            {
                var level = curatedLevels[i];
                if (level != null && level.LevelNumber == levelNumber)
                {
                    return level;
                }
            }

            // Fallback to 0-based index if levelNumber matches sequential index
            int index = levelNumber - 1;
            if (index >= 0 && index < curatedLevels.Count)
            {
                return curatedLevels[index];
            }

            Debug.LogWarning($"[LevelDatabaseSO] Level with number {levelNumber} not found.");
            return null;
        }

        /// <summary>
        /// Retrieves the SushiDataSO asset corresponding to the specified SushiType.
        /// Uses cached dictionary for O(1) performance.
        /// </summary>
        /// <param name="type">The sushi type to query.</param>
        /// <returns>SushiDataSO definition if registered; null otherwise.</returns>
        public SushiDataSO GetSushiData(SushiType type)
        {
            if (type == SushiType.None)
            {
                return null;
            }

            if (_sushiLookupCache == null || _sushiLookupCache.Count != (allSushiTypes != null ? allSushiTypes.Count : 0))
            {
                RebuildLookupCache();
            }

            if (_sushiLookupCache.TryGetValue(type, out var sushiData))
            {
                return sushiData;
            }

            Debug.LogWarning($"[LevelDatabaseSO] SushiDataSO for type '{type}' is not configured in allSushiTypes.");
            return null;
        }

        /// <summary>
        /// Rebuilds the internal dictionary lookup cache from the serialized allSushiTypes list.
        /// </summary>
        public void RebuildLookupCache()
        {
            if (_sushiLookupCache == null)
            {
                _sushiLookupCache = new Dictionary<SushiType, SushiDataSO>();
            }
            else
            {
                _sushiLookupCache.Clear();
            }

            if (allSushiTypes == null) return;

            for (int i = 0; i < allSushiTypes.Count; i++)
            {
                var data = allSushiTypes[i];
                if (data == null) continue;

                if (!_sushiLookupCache.ContainsKey(data.SushiType))
                {
                    _sushiLookupCache.Add(data.SushiType, data);
                }
                else
                {
                    Debug.LogWarning($"[LevelDatabaseSO] Duplicate SushiDataSO entry found for type '{data.SushiType}'. Ignoring subsequent duplicates.");
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookupCache();
        }
#endif
    }
}

