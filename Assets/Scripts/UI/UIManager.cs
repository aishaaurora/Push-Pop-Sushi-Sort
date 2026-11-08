using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using SushiSort.Core;
using SushiSort.Utilities;

namespace SushiSort.UI
{
    /// <summary>
    /// Master UI Manager for Push Pop Sushi Sort.
    /// Manages Header HUD, Win Popup, Lose/Deadlock Popup, and Screenshot Flash/Toast.
    /// Fully decoupled via static GamePlayManager event subscriptions.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>(true);
                    if (_instance != null && !_instance.gameObject.activeInHierarchy)
                    {
                        _instance.gameObject.SetActive(true);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Header HUD")]
        [Tooltip("Header text displaying the current level number and difficulty badge.")]
        [SerializeField] private TextMeshProUGUI txtLevelTitle;
        [SerializeField] private TextMeshProUGUI txtDifficulty;

        [Tooltip("Button in header to restart current level immediately.")]
        [SerializeField] private Button btnRestartHeader;

        [Tooltip("Button in header to capture a screenshot.")]
        [SerializeField] private Button btnScreenshotHeader;

        [Header("Navigation & Panels")]
        [Tooltip("Button to return to Main Menu from gameplay or popups.")]
        [SerializeField] private Button btnHome;

        [Tooltip("Root panel for Main Menu (used if single-scene UI panel toggle).")]
        [SerializeField] private GameObject mainMenuPanel;

        [Tooltip("Root panel for Gameplay HUD (used if single-scene UI panel toggle).")]
        [SerializeField] private GameObject gameplayPanel;

        [Tooltip("Main Menu scene name when using multi-scene navigation.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Coin Economy UI")]
        [Tooltip("Coin counter text displayed in the Main Menu.")]
        [SerializeField] private TextMeshProUGUI txtCoinMainMenu;

        [Tooltip("Coin counter text displayed in the Gameplay HUD.")]
        [SerializeField] private TextMeshProUGUI txtCoinGameplay;

        [Header("Booster (+1 Tube) UI")]
        [Tooltip("Confirmation popup panel for buying +1 Empty Tube booster.")]
        [SerializeField] private GameObject popupBooster;

        [Tooltip("Green plus button in gameplay HUD to open the booster popup.")]
        [SerializeField] private Button btnHudBooster;

        [Tooltip("Red X button inside booster popup to close it.")]
        [SerializeField] private Button btnClosePopupBooster;

        [Tooltip("Confirm purchase button (30 coins) inside booster popup.")]
        [SerializeField] private Button btnConfirmBuyBooster;

        [Header("Setting Panel")]
        [Tooltip("Root GameObject or RectTransform for the Setting popup.")]
        [SerializeField] private GameObject settingPanel;

        [Tooltip("Button to open the setting panel.")]
        [SerializeField] private Button btnOpenSetting;

        [Tooltip("Button to close the setting panel.")]
        [SerializeField] private Button btnCloseSetting;

        [Tooltip("Button inside setting panel to take a screenshot.")]
        [SerializeField] private Button btnTakeScreenshot;

        [Tooltip("Optional text label for toast feedback notification.")]
        [SerializeField] private TextMeshProUGUI txtToastFeedback;

        [Header("Win Panel")]
        [Tooltip("Root GameObject or RectTransform for the Victory popup.")]
        [SerializeField] private GameObject winPanelRoot;

        [Tooltip("Title text on win panel (e.g. 'Order Completed! 🍣').")]
        [SerializeField] private TextMeshProUGUI txtWinTitle;

        [Tooltip("Button on win panel to proceed to the next level.")]
        [SerializeField] private Button btnNextLevel;

        [Header("Lose / Deadlock Panel")]
        [Tooltip("Root GameObject or RectTransform for the Deadlock/Lose popup.")]
        [SerializeField] private GameObject losePanelRoot;

        [Tooltip("Reason text on lose panel (e.g. 'No More Moves! 🥢').")]
        [SerializeField] private TextMeshProUGUI txtLoseReason;

        [Tooltip("Button on lose panel to retry current level.")]
        [SerializeField] private Button btnRetryLose;

        [Header("Screenshot Notification")]
        [Tooltip("White screen overlay with CanvasGroup for flash effect.")]
        [SerializeField] private CanvasGroup screenshotFlashOverlay;

        [Tooltip("Toast notification popup GameObject indicating screenshot was saved.")]
        [SerializeField] private GameObject toastNotification;

        [Tooltip("Text label inside toast notification.")]
        [SerializeField] private TextMeshProUGUI txtToastMessage;

        // Coroutine trackers
        private Coroutine _winAnimCoroutine;
        private Coroutine _loseAnimCoroutine;
        private Coroutine _settingAnimCoroutine;
        private Coroutine _boosterAnimCoroutine;
        private Coroutine _flashCoroutine;
        private Coroutine _toastCoroutine;
        private Coroutine _screenshotCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Bind button listeners with null-safety
            if (btnRestartHeader != null)
                btnRestartHeader.onClick.AddListener(OnRestartClicked);

            if (btnScreenshotHeader != null)
                btnScreenshotHeader.onClick.AddListener(OnScreenshotClicked);

            if (btnOpenSetting != null)
                btnOpenSetting.onClick.AddListener(OnOpenSettingClicked);

            if (btnCloseSetting != null)
                btnCloseSetting.onClick.AddListener(OnCloseSettingClicked);

            if (btnTakeScreenshot != null)
                btnTakeScreenshot.onClick.AddListener(OnTakeScreenshotClicked);

            if (btnHudBooster != null)
                btnHudBooster.onClick.AddListener(OnOpenBoosterClicked);

            if (btnClosePopupBooster != null)
                btnClosePopupBooster.onClick.AddListener(OnCloseBoosterClicked);

            if (btnConfirmBuyBooster != null)
                btnConfirmBuyBooster.onClick.AddListener(OnConfirmBuyBoosterClicked);

            if (btnNextLevel != null)
                btnNextLevel.onClick.AddListener(OnNextLevelClicked);

            if (btnRetryLose != null)
                btnRetryLose.onClick.AddListener(OnRetryClicked);

            if (btnHome != null)
            {
                btnHome.onClick.RemoveAllListeners();
                btnHome.onClick.AddListener(GoToMainMenu);
            }

            // Auto-bind any Home buttons inside popups or canvases
            BindHomeButtonsInRoot(winPanelRoot);
            BindHomeButtonsInRoot(losePanelRoot);
            BindHomeButtonsInRoot(settingPanel);
            BindAllSceneHomeButtons();

            // Hide popups initially
            HideImmediate(winPanelRoot);
            HideImmediate(losePanelRoot);
            HideImmediate(settingPanel);
            HideImmediate(popupBooster);
            HideImmediate(toastNotification);

            if (screenshotFlashOverlay != null)
            {
                screenshotFlashOverlay.alpha = 0f;
                screenshotFlashOverlay.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            GamePlayManager.OnLevelStarted += HandleLevelStarted;
            GamePlayManager.OnLevelCompleted += HandleLevelCompleted;
            GamePlayManager.OnGameOver += HandleGameOver;
            GamePlayManager.OnDeadlockDetected += HandleGameOver;
            CurrencyManager.OnCoinsChanged += HandleCoinsChanged;
        }

        private void OnDisable()
        {
            GamePlayManager.OnLevelStarted -= HandleLevelStarted;
            GamePlayManager.OnLevelCompleted -= HandleLevelCompleted;
            GamePlayManager.OnGameOver -= HandleGameOver;
            GamePlayManager.OnDeadlockDetected -= HandleGameOver;
            CurrencyManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        private void Start()
        {
            UpdateHeaderTitle();
            UpdateCoinTexts(CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCoins() : 0);
        }

        #region Event Handlers

        private void HandleLevelStarted()
        {
            UpdateHeaderTitle();
            UpdateCoinTexts(CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCoins() : 0);
            HideImmediate(winPanelRoot);
            HideImmediate(losePanelRoot);
            HideImmediate(settingPanel);
            HideImmediate(popupBooster);

            if (btnHudBooster != null)
            {
                bool boosterUsed = GamePlayManager.Instance != null && GamePlayManager.Instance.IsBoosterUsedThisLevel;
                btnHudBooster.interactable = !boosterUsed;
            }
        }

        private void HandleLevelCompleted()
        {
            ShowWinPanel();
        }

        private void HandleGameOver()
        {
            ShowLosePanel();
        }

        private void HandleCoinsChanged(int newCoinAmount)
        {
            UpdateCoinTexts(newCoinAmount);
        }

        #endregion

        #region Coin Economy UI

        /// <summary>
        /// Synchronizes coin display texts with the latest balance in CurrencyManager.
        /// </summary>
        public void UpdateCoinDisplay()
        {
            int coins = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCoins() : 0;
            UpdateCoinTexts(coins);
        }

        /// <summary>
        /// Refreshes both Main Menu and Gameplay HUD coin text labels in real-time.
        /// </summary>
        public void UpdateCoinTexts(int coinAmount)
        {
            string formatted = coinAmount.ToString();

            if (txtCoinMainMenu != null)
            {
                txtCoinMainMenu.text = formatted;
            }

            if (txtCoinGameplay != null)
            {
                txtCoinGameplay.text = formatted;
            }
        }

        /// <summary>
        /// Refreshes coin and level display texts when transitioning to the Main Menu.
        /// </summary>
        public void UpdateMainMenuDisplay()
        {
            UpdateCoinDisplay();
            UpdateHeaderTitle();
        }

        #endregion

        #region Booster (+1 Tube) Popup

        /// <summary>
        /// Opens the +1 Tube booster confirmation popup.
        /// </summary>
        public void OpenBoosterPopup()
        {
            if (popupBooster == null) return;

            popupBooster.SetActive(true);
            if (_boosterAnimCoroutine != null) StopCoroutine(_boosterAnimCoroutine);
            _boosterAnimCoroutine = StartCoroutine(AnimatePopupOpen(popupBooster.transform));
        }

        /// <summary>
        /// Closes the +1 Tube booster confirmation popup.
        /// </summary>
        public void CloseBoosterPopup()
        {
            if (popupBooster == null) return;

            if (_boosterAnimCoroutine != null) StopCoroutine(_boosterAnimCoroutine);
            _boosterAnimCoroutine = StartCoroutine(AnimatePopupClose(popupBooster.transform, () =>
            {
                popupBooster.SetActive(false);
            }));
        }

        private void OnOpenBoosterClicked()
        {
            OpenBoosterPopup();
        }

        private void OnCloseBoosterClicked()
        {
            CloseBoosterPopup();
        }

        private void OnConfirmBuyBoosterClicked()
        {
            if (GamePlayManager.Instance == null) return;

            bool success = GamePlayManager.Instance.TryPurchaseExtraTubeBooster();
            if (success)
            {
                if (_boosterAnimCoroutine != null) StopCoroutine(_boosterAnimCoroutine);
                if (popupBooster != null)
                {
                    popupBooster.transform.localScale = Vector3.zero;
                    popupBooster.SetActive(false);
                }

                if (btnHudBooster != null)
                {
                    btnHudBooster.interactable = false;
                }

                // Also hide lose/deadlock panel if player used booster to recover
                HideImmediate(losePanelRoot);
            }
            else
            {
                ShowFeedbackToast("Not enough coins!");
            }
        }

        #endregion

        #region Header Title

        private void UpdateHeaderTitle()
        {
            if (txtLevelTitle == null) return;

            int levelNum = GamePlayManager.Instance != null 
                ? GamePlayManager.Instance.CurrentLevelNumber 
                : 1;

            bool isHard = GamePlayManager.Instance != null && GamePlayManager.Instance.IsHardLevel;

            txtLevelTitle.text = $"Level {levelNum}";
            if (txtDifficulty != null)
                {
                    txtDifficulty.text = isHard ? "<color=#820400>[HARD]</color>" : "";
                }
        }

        #endregion

        #region Setting Popup

        /// <summary>
        /// Opens the setting panel with a smooth bouncy animation.
        /// </summary>
        public void OpenSettingPanel()
        {
            if (settingPanel == null) return;

            settingPanel.SetActive(true);
            if (_settingAnimCoroutine != null) StopCoroutine(_settingAnimCoroutine);
            _settingAnimCoroutine = StartCoroutine(AnimatePopupOpen(settingPanel.transform));
        }

        /// <summary>
        /// Closes the setting panel with a snappy shrink animation.
        /// </summary>
        public void CloseSettingPanel()
        {
            if (settingPanel == null) return;

            if (_settingAnimCoroutine != null) StopCoroutine(_settingAnimCoroutine);
            _settingAnimCoroutine = StartCoroutine(AnimatePopupClose(settingPanel.transform, () =>
            {
                settingPanel.SetActive(false);
            }));
        }

        private void OnOpenSettingClicked()
        {
            OpenSettingPanel();
        }

        private void OnCloseSettingClicked()
        {
            CloseSettingPanel();
        }

        #endregion

        #region Win & Lose Popups

        public void ShowWinPanel()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (winPanelRoot == null)
            {
                winPanelRoot = FindScenePanel("Win_Panel") ?? FindScenePanel("WinPanel");
            }

            if (winPanelRoot == null)
            {
                Debug.LogWarning("[UIManager] winPanelRoot reference is missing!");
                return;
            }

            // Ensure parent hierarchy (e.g. Canvas / Popups) is active
            EnsureParentHierarchyActive(winPanelRoot.transform);

            // Sync latest coin display
            UpdateCoinDisplay();

            if (txtWinTitle != null)
            {
                txtWinTitle.text = "Order Completed!";
            }

            // Ensure Next Level and Home button listeners are bound
            if (btnNextLevel == null)
            {
                btnNextLevel = winPanelRoot.GetComponentInChildren<Button>(true);
            }
            if (btnNextLevel != null)
            {
                btnNextLevel.onClick.RemoveListener(OnNextLevelClicked);
                btnNextLevel.onClick.AddListener(OnNextLevelClicked);
            }
            BindHomeButtonsInRoot(winPanelRoot);

            // If already open and active, just ensure scale is 1 and return
            if (winPanelRoot.activeSelf && _winAnimCoroutine != null)
            {
                return;
            }

            // Immediately activate panel and set valid scale so it is always visible
            winPanelRoot.SetActive(true);
            winPanelRoot.transform.localScale = Vector3.one;

            if (gameObject.activeInHierarchy)
            {
                if (_winAnimCoroutine != null) StopCoroutine(_winAnimCoroutine);
                _winAnimCoroutine = StartCoroutine(AnimatePopupOpen(winPanelRoot.transform));
            }
        }

        public void ShowLosePanel()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (losePanelRoot == null)
            {
                losePanelRoot = FindScenePanel("Lose_Panel") ?? FindScenePanel("LosePanel");
            }

            if (losePanelRoot == null) return;

            EnsureParentHierarchyActive(losePanelRoot.transform);
            UpdateCoinDisplay();

            if (txtLoseReason != null)
            {
                txtLoseReason.text = "No More Moves!";
            }

            if (btnRetryLose == null)
            {
                btnRetryLose = losePanelRoot.GetComponentInChildren<Button>(true);
            }
            if (btnRetryLose != null)
            {
                btnRetryLose.onClick.RemoveListener(OnRetryClicked);
                btnRetryLose.onClick.AddListener(OnRetryClicked);
            }
            BindHomeButtonsInRoot(losePanelRoot);

            if (losePanelRoot.activeSelf && _loseAnimCoroutine != null)
            {
                return;
            }

            losePanelRoot.SetActive(true);
            losePanelRoot.transform.localScale = Vector3.one;

            if (gameObject.activeInHierarchy)
            {
                if (_loseAnimCoroutine != null) StopCoroutine(_loseAnimCoroutine);
                _loseAnimCoroutine = StartCoroutine(AnimatePopupOpen(losePanelRoot.transform));
            }
        }

        private void EnsureParentHierarchyActive(Transform target)
        {
            Transform current = target != null ? target.parent : null;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
                current = current.parent;
            }
        }

        private GameObject FindScenePanel(string panelName)
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                Transform found = FindDeepChild(canvas.transform, panelName);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private void BindHomeButtonsInRoot(GameObject root)
        {
            if (root == null) return;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null) continue;

                string objName = b.gameObject.name;
                if (b == btnHome ||
                    string.Equals(objName, "Home", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objName, "Btn_Home", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objName, "BtnHome", StringComparison.OrdinalIgnoreCase))
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(GoToMainMenu);
                }
            }
        }

        private void BindAllSceneHomeButtons()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    BindHomeButtonsInRoot(canvases[i].gameObject);
                }
            }
        }

        private void StopAllUICoroutines()
        {
            if (_winAnimCoroutine != null) { StopCoroutine(_winAnimCoroutine); _winAnimCoroutine = null; }
            if (_loseAnimCoroutine != null) { StopCoroutine(_loseAnimCoroutine); _loseAnimCoroutine = null; }
            if (_settingAnimCoroutine != null) { StopCoroutine(_settingAnimCoroutine); _settingAnimCoroutine = null; }
            if (_boosterAnimCoroutine != null) { StopCoroutine(_boosterAnimCoroutine); _boosterAnimCoroutine = null; }
            if (_flashCoroutine != null) { StopCoroutine(_flashCoroutine); _flashCoroutine = null; }
            if (_toastCoroutine != null) { StopCoroutine(_toastCoroutine); _toastCoroutine = null; }
            if (_screenshotCoroutine != null) { StopCoroutine(_screenshotCoroutine); _screenshotCoroutine = null; }

            if (screenshotFlashOverlay != null)
            {
                screenshotFlashOverlay.alpha = 0f;
                screenshotFlashOverlay.blocksRaycasts = false;
            }
        }

        private void HideImmediate(GameObject panel)
        {
            if (panel == null) return;
            if (panel == winPanelRoot && _winAnimCoroutine != null)
            {
                StopCoroutine(_winAnimCoroutine);
                _winAnimCoroutine = null;
            }
            else if (panel == losePanelRoot && _loseAnimCoroutine != null)
            {
                StopCoroutine(_loseAnimCoroutine);
                _loseAnimCoroutine = null;
            }
            else if (panel == settingPanel && _settingAnimCoroutine != null)
            {
                StopCoroutine(_settingAnimCoroutine);
                _settingAnimCoroutine = null;
            }
            else if (panel == popupBooster && _boosterAnimCoroutine != null)
            {
                StopCoroutine(_boosterAnimCoroutine);
                _boosterAnimCoroutine = null;
            }
            else if (panel == toastNotification && _toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }
            panel.transform.localScale = Vector3.zero;
            panel.SetActive(false);
        }

        #endregion

        #region Button Callbacks

        /// <summary>
        /// Cleans up active gameplay/UI state and loads the "MainMenu" scene.
        /// </summary>
        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            StopAllUICoroutines();

            if (GamePlayManager.Instance != null)
            {
                GamePlayManager.Instance.CleanupCurrentLevel();
            }

            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Handles Home button click from gameplay HUD or pause/win/lose/setting popups.
        /// Safely stops active UI/gameplay coroutines, cleans up the active level, and transitions to Main Menu.
        /// </summary>
        public void OnHomeButtonClicked()
        {
            if (mainMenuPanel != null && gameplayPanel != null)
            {
                Time.timeScale = 1f;
                StopAllUICoroutines();
                HideImmediate(winPanelRoot);
                HideImmediate(losePanelRoot);
                HideImmediate(settingPanel);
                HideImmediate(popupBooster);
                HideImmediate(toastNotification);

                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.CleanupCurrentLevel();
                }

                gameplayPanel.SetActive(false);
                mainMenuPanel.SetActive(true);
                UpdateMainMenuDisplay();
            }
            else
            {
                GoToMainMenu();
            }
        }

        private void OnRestartClicked()
        {
            if (GamePlayManager.Instance != null)
            {
                GamePlayManager.Instance.RestartCurrentLevel();
            }
        }

        private void OnNextLevelClicked()
        {
            if (_winAnimCoroutine != null)
            {
                StopCoroutine(_winAnimCoroutine);
                _winAnimCoroutine = null;
            }

            if (winPanelRoot != null && gameObject.activeInHierarchy)
            {
                _winAnimCoroutine = StartCoroutine(AnimatePopupClose(winPanelRoot.transform, () =>
                {
                    _winAnimCoroutine = null;
                    if (GamePlayManager.Instance != null)
                    {
                        GamePlayManager.Instance.LoadNextLevel();
                    }
                }));
            }
            else
            {
                HideImmediate(winPanelRoot);
                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.LoadNextLevel();
                }
            }
        }

        private void OnRetryClicked()
        {
            if (_loseAnimCoroutine != null)
            {
                StopCoroutine(_loseAnimCoroutine);
                _loseAnimCoroutine = null;
            }

            if (losePanelRoot != null && gameObject.activeInHierarchy)
            {
                _loseAnimCoroutine = StartCoroutine(AnimatePopupClose(losePanelRoot.transform, () =>
                {
                    _loseAnimCoroutine = null;
                    if (GamePlayManager.Instance != null)
                    {
                        GamePlayManager.Instance.RestartCurrentLevel();
                    }
                }));
            }
            else
            {
                HideImmediate(losePanelRoot);
                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.RestartCurrentLevel();
                }
            }
        }

        private void OnTakeScreenshotClicked()
        {
            if (_screenshotCoroutine != null) StopCoroutine(_screenshotCoroutine);
            _screenshotCoroutine = StartCoroutine(CaptureScreenshotCleanRoutine());
        }

        private void OnScreenshotClicked()
        {
            OnTakeScreenshotClicked();
        }

        /// <summary>
        /// Captures a clean screenshot without setting panel UI interference.
        /// </summary>
        private IEnumerator CaptureScreenshotCleanRoutine()
        {
            TriggerScreenshotFlash();

            // 1. Sembunyikan panel setting sementara jika aktif
            bool wasSettingOpen = settingPanel != null && settingPanel.activeSelf;
            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }

            yield return new WaitForEndOfFrame();

            // 2. Capture screenshot ke persistentDataPath / format tanggal
            string fileName = $"SushiSort_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName);
            Debug.Log($"Screenshot saved: {fileName}");

            // 3. Tunggu 1 frame lalu buka kembali setting panel
            yield return null;

            if (settingPanel != null && wasSettingOpen)
            {
                settingPanel.SetActive(true);
                settingPanel.transform.localScale = Vector3.one;
            }

            // 4. Tampilkan pesan feedback (tanpa emoji Unicode agar bebas warning)
            ShowFeedbackToast("Screenshot Saved!");
        }

        /// <summary>
        /// Displays clean toast feedback without missing unicode glyph warnings.
        /// </summary>
        public void ShowFeedbackToast(string message = "Screenshot Saved!")
        {
            ShowToast(message);
        }

        #endregion

        #region Animation Coroutines (Procedural Easing)

        /// <summary>
        /// Bouncy pop-up scale animation from 0 to 1 with an overshoot bounce (EaseOutBack).
        /// </summary>
        private IEnumerator AnimatePopupOpen(Transform popupTransform)
        {
            if (popupTransform == null) yield break;

            popupTransform.gameObject.SetActive(true);
            popupTransform.localScale = Vector3.zero;

            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutBack overshoot curve formula
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float bounce = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                popupTransform.localScale = Vector3.one * Mathf.Max(0f, bounce);
                yield return null;
            }

            popupTransform.localScale = Vector3.one;

            if (winPanelRoot != null && popupTransform == winPanelRoot.transform)
                _winAnimCoroutine = null;
            else if (losePanelRoot != null && popupTransform == losePanelRoot.transform)
                _loseAnimCoroutine = null;
        }

        /// <summary>
        /// Snappy pop-up shrink animation from 1 to 0.
        /// </summary>
        private IEnumerator AnimatePopupClose(Transform popupTransform, Action onComplete = null)
        {
            if (popupTransform == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            Vector3 origScale = popupTransform.localScale;
            float elapsed = 0f;
            float duration = 0.16f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                popupTransform.localScale = Vector3.Lerp(origScale, Vector3.zero, t * t);
                yield return null;
            }

            popupTransform.localScale = Vector3.zero;
            popupTransform.gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        private void TriggerScreenshotFlash()
        {
            if (screenshotFlashOverlay == null) return;

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(AnimateFlashRoutine());
        }

        private IEnumerator AnimateFlashRoutine()
        {
            screenshotFlashOverlay.alpha = 1f;
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                screenshotFlashOverlay.alpha = 1f - t;
                yield return null;
            }

            screenshotFlashOverlay.alpha = 0f;
            _flashCoroutine = null;
        }

        private void ShowToast(string message)
        {
            if (txtToastFeedback != null)
            {
                txtToastFeedback.text = message;
            }

            if (txtToastMessage != null)
            {
                txtToastMessage.text = message;
            }

            if (toastNotification == null) return;

            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(AnimateToastRoutine());
        }

        private IEnumerator AnimateToastRoutine()
        {
            toastNotification.SetActive(true);
            toastNotification.transform.localScale = Vector3.zero;

            // Pop in
            float elapsed = 0f;
            float inDuration = 0.2f;
            while (elapsed < inDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / inDuration);
                float bounce = 1f + (2.7f * Mathf.Pow(t - 1f, 3f)) + (1.7f * Mathf.Pow(t - 1f, 2f));
                toastNotification.transform.localScale = Vector3.one * Mathf.Max(0f, bounce);
                yield return null;
            }
            toastNotification.transform.localScale = Vector3.one;

            // Display time
            yield return new WaitForSecondsRealtime(1.8f);

            // Pop out
            elapsed = 0f;
            float outDuration = 0.15f;
            while (elapsed < outDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / outDuration);
                toastNotification.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t * t);
                yield return null;
            }

            toastNotification.SetActive(false);
            _toastCoroutine = null;
        }

        #endregion
    }
}

