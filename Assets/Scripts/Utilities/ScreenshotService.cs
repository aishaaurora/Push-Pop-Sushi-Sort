using System;
using System.IO;
using UnityEngine;

namespace SushiSort.Utilities
{
    /// <summary>
    /// Service for capturing and saving in-game screenshots to disk.
    /// Accessible globally via Singleton instance.
    /// </summary>
    public class ScreenshotService : MonoBehaviour
    {
        public static ScreenshotService Instance { get; private set; }

        /// <summary>
        /// Event fired when a screenshot is successfully captured, passing the file path.
        /// </summary>
        public static event Action<string> OnScreenshotCaptured;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Captures a screenshot and saves it as a PNG file in persistentDataPath/Screenshots.
        /// </summary>
        public void TakeScreenshot(Action<string> onComplete = null)
        {
            string folderPath = Path.Combine(Application.persistentDataPath, "Screenshots");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"PushPopSushi_{timestamp}.png";
            string fullPath = Path.Combine(folderPath, fileName);

            ScreenCapture.CaptureScreenshot(fullPath);

            Debug.Log($"[ScreenshotService] Screenshot saved to: {fullPath}");
            OnScreenshotCaptured?.Invoke(fullPath);
            onComplete?.Invoke(fullPath);
        }
    }
}

