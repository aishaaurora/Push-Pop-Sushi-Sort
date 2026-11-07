using UnityEngine;
using SushiSort.Core;

namespace SushiSort.ScriptableObjects
{
    /// <summary>
    /// Configuration data asset for a specific sushi roll type.
    /// Defines visual representation, prefab references, display information,
    /// and texture atlas UV coordinates for locked tube ribbon requirements.
    /// </summary>
    [CreateAssetMenu(fileName = "SushiData_", menuName = "SushiSort/Sushi Data", order = 1)]
    public class SushiDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The logical sushi type identifier.")]
        [SerializeField] private SushiType sushiType;

        [Tooltip("Display name used in UI and notifications.")]
        [SerializeField] private string displayName;

        [Header("Visuals & FX")]
        [Tooltip("Theme color used for UI highlights, progress bars, and particle VFX.")]
        [SerializeField] private Color themeColor = Color.white;

        [Tooltip("3D Prefab model instance spawned inside tubes.")]
        [SerializeField] private GameObject sushiPrefab;

        [Tooltip("Optional material override if prefab meshes share geometry but swap materials.")]
        [SerializeField] private Material sushiMaterial;

        [Header("Atlas UV Settings (Lock Requirement Ribbon)")]
        [Tooltip("UV offset (X, Y) inside the 2048x2048 sushi icon texture atlas.")]
        [SerializeField] private Vector2 uvOffset = Vector2.zero;

        [Tooltip("UV scale/tiling (X, Y) inside the sushi icon texture atlas (e.g. 0.25, 0.25 for 4x4 grid).")]
        [SerializeField] private Vector2 uvScale = new Vector2(0.25f, 0.25f);

        // Public Getters (Strict Encapsulation)
        public SushiType SushiType => sushiType;
        public string DisplayName => displayName;
        public Color ThemeColor => themeColor;
        public GameObject SushiPrefab => sushiPrefab;
        public Material SushiMaterial => sushiMaterial;
        public Vector2 UvOffset => uvOffset;
        public Vector2 UvScale => uvScale;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-populate displayName from enum if empty
            if (string.IsNullOrWhiteSpace(displayName) && sushiType != SushiType.None)
            {
                displayName = sushiType.ToString();
            }

            // Ensure UV scale is not accidentally zeroed
            if (uvScale == Vector2.zero)
            {
                uvScale = new Vector2(0.25f, 0.25f);
            }
        }
#endif
    }
}
