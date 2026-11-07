using System.Collections.Generic;
using UnityEngine;
using SushiSort.Core;
using SushiSort.ScriptableObjects;

namespace SushiSort.Gameplay
{
    /// <summary>
    /// Spawns and dynamically arranges tubes in a centered grid (1 or 2 rows)
    /// based on the total tube count of the current level.
    /// Cleans up old tube instances and manages dynamic layout adjustments.
    /// </summary>
    public class LevelSpawner : MonoBehaviour
    {
        [Header("Prefab & Hierarchy")]
        [Tooltip("The tube prefab instantiated into the scene.")]
        [SerializeField] private PushPopTube tubePrefab;

        [Tooltip("Parent transform for instantiated tube instances.")]
        [SerializeField] private Transform tubesContainer;

        [Header("Layout Parameters")]
        [Tooltip("Horizontal spacing between adjacent tubes.")]
        [SerializeField] private float horizontalSpacing = 2.4f;

        [Tooltip("Vertical distance between top row and bottom row when > 5 tubes.")]
        [SerializeField] private float verticalRowSpacing = 2.8f;

        [Tooltip("Z-depth offset for the top row to prevent visual overlap at inclined camera angles.")]
        [SerializeField] private float rowDepthOffset = 0.5f;

        [Tooltip("World reference center point around which tubes are symmetrically arranged.")]
        [SerializeField] private Transform spawnAnchorPoint;

        // Internal tracking
        private readonly List<PushPopTube> _spawnedTubes = new List<PushPopTube>();

        // Public Getters
        public IReadOnlyList<PushPopTube> SpawnedTubes => _spawnedTubes;
        public int SpawnedTubeCount => _spawnedTubes.Count;

        /// <summary>
        /// Clears previously spawned tubes and generates a new layout matching levelData.
        /// </summary>
        /// <param name="levelData">The LevelDataSO containing tube configurations.</param>
        /// <param name="database">Master sushi definition database.</param>
        /// <param name="sushiContainer">Parent container for sushi GameObjects.</param>
        /// <returns>List of instantiated and initialized PushPopTube components.</returns>
        public List<PushPopTube> SpawnLevel(LevelDataSO levelData, LevelDatabaseSO database, Transform sushiContainer = null)
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelSpawner] LevelDataSO is null.");
                return _spawnedTubes;
            }

            if (tubePrefab == null)
            {
                Debug.LogError("[LevelSpawner] TubePrefab is not assigned in the inspector!");
                return _spawnedTubes;
            }

            // 1. Destroy and clear existing tubes
            ClearTubes();

            int totalTubes = levelData.TotalTubes;
            if (totalTubes == 0)
            {
                Debug.LogWarning("[LevelSpawner] LevelDataSO contains 0 tubes.");
                return _spawnedTubes;
            }

            Transform container = tubesContainer != null ? tubesContainer : transform;
            Vector3 anchorPos = spawnAnchorPoint != null ? spawnAnchorPoint.position : transform.position;

            // 2. Calculate positions based on layout rules
            List<Vector3> layoutPositions = CalculateLayoutPositions(totalTubes, anchorPos);

            // 3. Instantiate and initialize each tube
            for (int i = 0; i < totalTubes; i++)
            {
                Vector3 spawnPos = layoutPositions[i];
                Quaternion spawnRot = Quaternion.identity;

                PushPopTube tubeInstance = Instantiate(tubePrefab, spawnPos, spawnRot, container);
                tubeInstance.name = $"Tube_{i:00}";

                // Initialize tube internal stack & visual state
                tubeInstance.Initialize(levelData.Tubes[i], database, sushiContainer);

                _spawnedTubes.Add(tubeInstance);
            }

            return _spawnedTubes;
        }

        /// <summary>
        /// Calculates centered world coordinates for each tube based on 1-row or 2-row layout rules.
        /// </summary>
        private List<Vector3> CalculateLayoutPositions(int totalTubes, Vector3 anchor)
        {
            List<Vector3> positions = new List<Vector3>(totalTubes);

            if (totalTubes <= 5)
            {
                // Single centered horizontal row
                float rowWidth = (totalTubes - 1) * horizontalSpacing;
                float startX = anchor.x - (rowWidth * 0.5f);

                for (int i = 0; i < totalTubes; i++)
                {
                    float x = startX + (i * horizontalSpacing);
                    positions.Add(new Vector3(x, anchor.y, anchor.z));
                }
            }
            else
            {
                // Two horizontal rows: Top row and Bottom row
                int topRowCount = Mathf.CeilToInt(totalTubes / 2f);
                int bottomRowCount = totalTubes - topRowCount;

                // --- Top Row (Indices 0 .. topRowCount - 1) ---
                float topWidth = (topRowCount - 1) * horizontalSpacing;
                float topStartX = anchor.x - (topWidth * 0.5f);
                float topY = anchor.y + (verticalRowSpacing * 0.5f);
                float topZ = anchor.z + rowDepthOffset;

                for (int i = 0; i < topRowCount; i++)
                {
                    float x = topStartX + (i * horizontalSpacing);
                    positions.Add(new Vector3(x, topY, topZ));
                }

                // --- Bottom Row (Indices topRowCount .. totalTubes - 1) ---
                float bottomWidth = (bottomRowCount - 1) * horizontalSpacing;
                float bottomStartX = anchor.x - (bottomWidth * 0.5f);
                float bottomY = anchor.y - (verticalRowSpacing * 0.5f);
                float bottomZ = anchor.z;

                for (int i = 0; i < bottomRowCount; i++)
                {
                    float x = bottomStartX + (i * horizontalSpacing);
                    positions.Add(new Vector3(x, bottomY, bottomZ));
                }
            }

            return positions;
        }

        /// <summary>
        /// Destroys all currently spawned tube GameObjects.
        /// </summary>
        public void ClearTubes()
        {
            for (int i = _spawnedTubes.Count - 1; i >= 0; i--)
            {
                if (_spawnedTubes[i] != null)
                {
                    Destroy(_spawnedTubes[i].gameObject);
                }
            }

            _spawnedTubes.Clear();
        }

        private void OnDestroy()
        {
            ClearTubes();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 anchor = spawnAnchorPoint != null ? spawnAnchorPoint.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(anchor, 0.25f);

            // Preview 1-row layout (5 tubes)
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
            float width5 = 4 * horizontalSpacing;
            float start5 = anchor.x - (width5 * 0.5f);
            for (int i = 0; i < 5; i++)
            {
                Gizmos.DrawWireCube(new Vector3(start5 + i * horizontalSpacing, anchor.y, anchor.z), new Vector3(0.8f, 2f, 0.8f));
            }
        }
#endif
    }
}

