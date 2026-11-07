using UnityEditor;
using UnityEngine;
using SushiSort.Core;
using SushiSort.ScriptableObjects;

namespace SushiSort.Editor
{
    /// <summary>
    /// Custom Editor for LevelDataSO providing an automated instant generation panel
    /// and visual read-only tube preview alongside the raw serialized properties.
    /// </summary>
    [CustomEditor(typeof(LevelDataSO))]
    public class LevelDataSOEditor : UnityEditor.Editor
    {
        private LevelDatabaseSO _databaseReference;
        private SerializedProperty _levelNumberProp;
        private SerializedProperty _maxTubeCapacityProp;
        private SerializedProperty _tubesProp;

        private SerializedProperty _colorCountProp;
        private SerializedProperty _emptyBufferTubesProp;
        private SerializedProperty _mysteryTubeCountProp;
        private SerializedProperty _lockedTubeCountProp;
        private SerializedProperty _shuffleStepsProp;

        private bool _showTubesPreview = true;
        private bool _showRawProperties = false;

        private void OnEnable()
        {
            _levelNumberProp = serializedObject.FindProperty("levelNumber");
            _maxTubeCapacityProp = serializedObject.FindProperty("maxTubeCapacity");
            _tubesProp = serializedObject.FindProperty("tubes");

            _colorCountProp = serializedObject.FindProperty("colorCount");
            _emptyBufferTubesProp = serializedObject.FindProperty("emptyBufferTubes");
            _mysteryTubeCountProp = serializedObject.FindProperty("mysteryTubeCount");
            _lockedTubeCountProp = serializedObject.FindProperty("lockedTubeCount");
            _shuffleStepsProp = serializedObject.FindProperty("shuffleSteps");

            // Auto-locate master LevelDatabaseSO if available in the project
            if (_databaseReference == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:LevelDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _databaseReference = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(path);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LevelDataSO levelData = (LevelDataSO)target;

            // --- 1. Level Meta Settings ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Level Identification", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_levelNumberProp, new GUIContent("Level Number"));
            EditorGUILayout.PropertyField(_maxTubeCapacityProp, new GUIContent("Max Tube Capacity"));

            EditorGUILayout.Space(8);

            // --- 2. Auto Generator & Reverse Shuffle Panel ---
            GUI.backgroundColor = new Color(0.85f, 0.92f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField("⚡ Auto Generator & Reverse Shuffle", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure the parameters below and click 'Generate & Shuffle Level' to automatically simulate solvable backward steps and populate this asset.", 
                MessageType.Info
            );

            EditorGUILayout.Space(4);
            _databaseReference = (LevelDatabaseSO)EditorGUILayout.ObjectField(
                new GUIContent("Sushi Database", "Reference to LevelDatabaseSO for active sushi definitions."),
                _databaseReference, 
                typeof(LevelDatabaseSO), 
                false
            );

            EditorGUILayout.IntSlider(_colorCountProp, 2, 6, new GUIContent("Active Colors", "Total sushi variants (2 to 6 colors). 1 color = 4 sushi pieces."));
            EditorGUILayout.PropertyField(_emptyBufferTubesProp, new GUIContent("Empty Buffer Tubes", "Number of empty tubes for player movement."));
            EditorGUILayout.PropertyField(_mysteryTubeCountProp, new GUIContent("Mystery Tubes", "Number of tubes with hidden bottom rolls."));
            EditorGUILayout.PropertyField(_lockedTubeCountProp, new GUIContent("Locked Tubes", "Number of tubes locked at start (guaranteed anti-deadlock)."));
            EditorGUILayout.PropertyField(_shuffleStepsProp, new GUIContent("Shuffle Steps", "Number of backward simulation steps."));

            EditorGUILayout.Space(8);

            // Large Action Button
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button("⚡ Generate & Shuffle Level", GUILayout.Height(35)))
            {
                serializedObject.ApplyModifiedProperties();

                Undo.RecordObject(levelData, "Generate & Shuffle Level");
                levelData.GenerateFromSettings(_databaseReference);

                EditorUtility.SetDirty(levelData);
                AssetDatabase.SaveAssets();

                serializedObject.Update();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // --- 3. Generated Tubes Configuration (Rich Visual Read-Only Preview) ---
            GUI.backgroundColor = new Color(0.98f, 0.98f, 0.98f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            _showTubesPreview = EditorGUILayout.Foldout(
                _showTubesPreview, 
                $"Tubes Layout Overview ({levelData.TotalTubes} Tubes Total)", 
                true, 
                EditorStyles.foldoutHeader
            );

            if (_showTubesPreview)
            {
                EditorGUI.indentLevel++;
                if (levelData.TotalTubes == 0)
                {
                    EditorGUILayout.HelpBox("No tubes configured yet. Click 'Generate & Shuffle Level' above.", MessageType.Warning);
                }
                else
                {
                    for (int i = 0; i < levelData.Tubes.Count; i++)
                    {
                        var tube = levelData.Tubes[i];
                        if (tube == null) continue;

                        string statusText = tube.IsEmpty 
                            ? "Empty (Buffer Tube)" 
                            : $"{tube.SushiCount} Pieces";

                        if (tube.IsLocked) statusText += $" | 🔒 Locked (Needs {tube.UnlockRequirement})";
                        if (tube.MysteryCount > 0) statusText += $" | ❓ Mystery: {tube.MysteryCount}";

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField($"Tube [{i:00}]: {statusText}", EditorStyles.boldLabel);

                        // Visual stack readout from Top (mouth) to Bottom (base)
                        if (!tube.IsEmpty)
                        {
                            EditorGUI.indentLevel++;
                            for (int s = tube.SushiStack.Count - 1; s >= 0; s--)
                            {
                                string layerLabel = (s == tube.SushiStack.Count - 1) 
                                    ? "▲ TOP   " 
                                    : (s == 0 ? "▼ BOTTOM" : $"  Slot {s} ");

                                bool isMysteryPiece = (s < tube.MysteryCount);
                                string pieceText = isMysteryPiece 
                                    ? $"{tube.SushiStack[s]} [MYSTERY ROLL]" 
                                    : tube.SushiStack[s].ToString();

                                EditorGUILayout.LabelField($"{layerLabel} : {pieceText}", EditorStyles.miniLabel);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            // --- 4. Raw Serialized Tubes List (Optional manual inspection) ---
            EditorGUILayout.Space(6);
            _showRawProperties = EditorGUILayout.Foldout(_showRawProperties, "Advanced: Raw Serialized Properties", true);
            if (_showRawProperties)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_tubesProp, new GUIContent("Raw Tubes List"), true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

