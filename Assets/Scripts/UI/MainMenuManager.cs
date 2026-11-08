using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using SushiSort.Core;

namespace SushiSort.UI
{
    /// <summary>
    /// Controller for the MainMenu scene (Scene 0: "MainMenu").
    /// Displays saved level & coin progress from PlayerPrefs and handles transitioning to "PlayZone".
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        private const string SAVE_LEVEL_KEY = "SushiSort_CurrentLevel";
        private const string SAVE_COINS_KEY = "SushiSort_Coins";
        private const string PLAY_SCENE_NAME = "PlayZone";

        [Header("UI References")]
        [Tooltip("Play / Level button to start gameplay.")]
        [SerializeField] private Button btnPlay;

        [Tooltip("Text displaying the current saved level (e.g. 'Level 1').")]
        [SerializeField] private TextMeshProUGUI txtCurrentLevel;

        [Tooltip("Text displaying the player's total coins.")]
        [SerializeField] private TextMeshProUGUI txtTotalCoins;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExistsInMainMenu()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateForActiveScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateForActiveScene(scene);
        }

        private static void TryCreateForActiveScene(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase))
                return;

            if (FindObjectOfType<MainMenuManager>(true) == null)
            {
                GameObject go = new GameObject("MainMenuManager");
                go.AddComponent<MainMenuManager>();
            }
        }

        private void Awake()
        {
            AutoResolveReferencesIfNeeded();
        }

        private void OnEnable()
        {
            CurrencyManager.OnCoinsChanged += HandleCoinsChanged;
        }

        private void OnDisable()
        {
            CurrencyManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        private void Start()
        {
            AutoResolveReferencesIfNeeded();
            RefreshMainMenuUI();

            if (btnPlay != null)
            {
                btnPlay.onClick.RemoveAllListeners();
                btnPlay.onClick.AddListener(() => SceneManager.LoadScene(PLAY_SCENE_NAME));
            }
        }

        /// <summary>
        /// Loads saved level and coin values from PlayerPrefs and updates the Main Menu UI labels.
        /// </summary>
        public void RefreshMainMenuUI()
        {
            int currentLevel = PlayerPrefs.GetInt(SAVE_LEVEL_KEY, 1);
            int totalCoins = PlayerPrefs.GetInt(SAVE_COINS_KEY, 0);

            if (txtCurrentLevel != null)
            {
                txtCurrentLevel.text = $"Level {currentLevel}";
            }

            if (txtTotalCoins != null)
            {
                txtTotalCoins.text = totalCoins.ToString();
            }
        }

        /// <summary>
        /// Loads the "PlayZone" gameplay scene.
        /// </summary>
        public void StartGame()
        {
            SceneManager.LoadScene(PLAY_SCENE_NAME);
        }

        private void HandleCoinsChanged(int newCoinAmount)
        {
            if (txtTotalCoins != null)
            {
                txtTotalCoins.text = newCoinAmount.ToString();
            }
        }

        /// <summary>
        /// Automatically resolves UI references from the active Canvas if left unassigned in the Inspector.
        /// </summary>
        private void AutoResolveReferencesIfNeeded()
        {
            if (btnPlay != null && txtCurrentLevel != null && txtTotalCoins != null)
                return;

            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Transform canvasRoot = canvases[i].transform;

                if (btnPlay == null)
                {
                    Transform playTransform = FindDeepChild(canvasRoot, "Play") ?? FindDeepChild(canvasRoot, "Btn_Play") ?? FindDeepChild(canvasRoot, "BtnPlay");
                    if (playTransform != null)
                    {
                        btnPlay = playTransform.GetComponent<Button>();
                    }
                }

                if (txtCurrentLevel == null && btnPlay != null)
                {
                    txtCurrentLevel = btnPlay.GetComponentInChildren<TextMeshProUGUI>(true);
                }

                if (txtTotalCoins == null)
                {
                    Transform totalTransform = FindDeepChild(canvasRoot, "Total") ?? FindDeepChild(canvasRoot, "Txt_Coin") ?? FindDeepChild(canvasRoot, "CoinText");
                    if (totalTransform != null)
                    {
                        txtTotalCoins = totalTransform.GetComponent<TextMeshProUGUI>() ?? totalTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                    }
                }
            }
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindDeepChild(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

