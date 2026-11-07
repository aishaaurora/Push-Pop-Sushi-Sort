using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
        public static UIManager Instance { get; private set; }

        [Header("Header HUD")]
        [Tooltip("Header text displaying the current level number and difficulty badge.")]
        [SerializeField] private TextMeshProUGUI txtLevelTitle;

        [Tooltip("Button in header to restart current level immediately.")]
        [SerializeField] private Button btnRestartHeader;

        [Tooltip("Button in header to capture a screenshot.")]
        [SerializeField] private Button btnScreenshotHeader;

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
        private Coroutine _flashCoroutine;
        private Coroutine _toastCoroutine;

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

            if (btnNextLevel != null)
                btnNextLevel.onClick.AddListener(OnNextLevelClicked);

            if (btnRetryLose != null)
                btnRetryLose.onClick.AddListener(OnRetryClicked);

            // Hide popups initially
            HideImmediate(winPanelRoot);
            HideImmediate(losePanelRoot);
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
        }

        private void OnDisable()
        {
            GamePlayManager.OnLevelStarted -= HandleLevelStarted;
            GamePlayManager.OnLevelCompleted -= HandleLevelCompleted;
            GamePlayManager.OnGameOver -= HandleGameOver;
            GamePlayManager.OnDeadlockDetected -= HandleGameOver;
        }

        private void Start()
        {
            UpdateHeaderTitle();
        }

        #region Event Handlers

        private void HandleLevelStarted()
        {
            UpdateHeaderTitle();
            HideImmediate(winPanelRoot);
            HideImmediate(losePanelRoot);
        }

        private void HandleLevelCompleted()
        {
            ShowWinPanel();
        }

        private void HandleGameOver()
        {
            ShowLosePanel();
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

            txtLevelTitle.text = isHard 
                ? $"Level {levelNum} <color=#FF5252>[HARD]</color>" 
                : $"Level {levelNum}";
        }

        #endregion

        #region Win & Lose Popups

        private void ShowWinPanel()
        {
            if (winPanelRoot == null) return;

            if (txtWinTitle != null)
            {
                txtWinTitle.text = "Order Completed! 🍣";
            }

            if (_winAnimCoroutine != null) StopCoroutine(_winAnimCoroutine);
            _winAnimCoroutine = StartCoroutine(AnimatePopupOpen(winPanelRoot.transform));
        }

        private void ShowLosePanel()
        {
            if (losePanelRoot == null) return;

            if (txtLoseReason != null)
            {
                txtLoseReason.text = "No More Moves! 🥢";
            }

            if (_loseAnimCoroutine != null) StopCoroutine(_loseAnimCoroutine);
            _loseAnimCoroutine = StartCoroutine(AnimatePopupOpen(losePanelRoot.transform));
        }

        private void HideImmediate(GameObject panel)
        {
            if (panel == null) return;
            panel.transform.localScale = Vector3.zero;
            panel.SetActive(false);
        }

        #endregion

        #region Button Callbacks

        private void OnRestartClicked()
        {
            if (GamePlayManager.Instance != null)
            {
                GamePlayManager.Instance.RestartCurrentLevel();
            }
        }

        private void OnNextLevelClicked()
        {
            if (_winAnimCoroutine != null) StopCoroutine(_winAnimCoroutine);
            _winAnimCoroutine = StartCoroutine(AnimatePopupClose(winPanelRoot.transform, () =>
            {
                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.LoadNextLevel();
                }
            }));
        }

        private void OnRetryClicked()
        {
            if (_loseAnimCoroutine != null) StopCoroutine(_loseAnimCoroutine);
            _loseAnimCoroutine = StartCoroutine(AnimatePopupClose(losePanelRoot.transform, () =>
            {
                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.RestartCurrentLevel();
                }
            }));
        }

        private void OnScreenshotClicked()
        {
            TriggerScreenshotFlash();

            if (ScreenshotService.Instance != null)
            {
                ScreenshotService.Instance.TakeScreenshot(OnScreenshotSaved);
            }
            else
            {
                // Fallback direct capture if service is not attached
                string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = System.IO.Path.Combine(Application.persistentDataPath, $"PushPopSushi_{time}.png");
                ScreenCapture.CaptureScreenshot(path);
                OnScreenshotSaved(path);
            }
        }

        private void OnScreenshotSaved(string path)
        {
            ShowToast("Screenshot saved! 📷");
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
            if (toastNotification == null) return;

            if (txtToastMessage != null)
            {
                txtToastMessage.text = message;
            }

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

