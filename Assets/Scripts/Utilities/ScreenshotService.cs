using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SushiSort.Utilities
{
    /// <summary>
    /// Hybrid cross-platform service for capturing and saving in-game screenshots:
    /// - WebGL: Downloads PNG directly in the user's browser via WebGLFileSaver.jslib.
    /// - Android / iOS: Saves to the device's public photo gallery via NativeGallery.
    /// - Editor / PC Standalone: Writes PNG to Application.persistentDataPath.
    /// </summary>
    public class ScreenshotService : MonoBehaviour
    {
        public static ScreenshotService Instance { get; private set; }

        /// <summary>
        /// Event fired when a screenshot is successfully captured, passing the saved file path or filename.
        /// </summary>
        public static event Action<string> OnScreenshotCaptured;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void DownloadFileFromMemory(byte[] array, int byteLength, string fileName);
#endif

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
        /// Captures a screenshot and saves it using the appropriate platform method.
        /// </summary>
        public void TakeScreenshot(Action<string> onComplete = null)
        {
            StartCoroutine(TakeScreenshotAndSaveRoutine(onComplete));
        }

        private IEnumerator TakeScreenshotAndSaveRoutine(Action<string> onComplete)
        {
            yield return new WaitForEndOfFrame();

            Texture2D screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenTexture.Apply();

            string filename = "SushiSort_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";

#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] bytes = screenTexture.EncodeToPNG();
            DownloadFileFromMemory(bytes, bytes.Length, filename);
            OnScreenshotCaptured?.Invoke(filename);
            onComplete?.Invoke(filename);
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            NativeGallery.SaveImageToGallery(
                screenTexture,
                "Push Pop Sushi Sort",
                filename,
                (success, path) =>
                {
                    Debug.Log($"Save result: {success}, Path: {path}");
                    if (success)
                    {
                        OnScreenshotCaptured?.Invoke(path);
                        onComplete?.Invoke(path);
                    }
                }
            );
#else
            byte[] bytes = screenTexture.EncodeToPNG();
            string fullPath = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllBytes(fullPath, bytes);
            Debug.Log($"[ScreenshotService] Screenshot saved to: {fullPath}");
            OnScreenshotCaptured?.Invoke(fullPath);
            onComplete?.Invoke(fullPath);
#endif

            Destroy(screenTexture);
        }
    }
}
