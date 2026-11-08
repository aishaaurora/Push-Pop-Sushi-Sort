using System;
using UnityEngine;

namespace SushiSort.Gameplay
{
    /// <summary>
    /// Detects non-standard shake input to trigger "Peek Mystery Sushi":
    /// - Mobile: Device shake via Accelerometer delta magnitude.
    /// - Editor / WebGL / PC fallback: Rapid mouse shake back-and-forth or Spacebar shortcut.
    /// Includes a cooldown to prevent continuous spam.
    /// </summary>
    public class ShakeInputDetector : MonoBehaviour
    {
        /// <summary>
        /// Fired when a valid shake gesture or fallback shortcut is detected.
        /// </summary>
        public static event Action OnShakeTriggered;

        [Header("Mobile Accelerometer Settings")]
        [Tooltip("Minimum acceleration delta magnitude required to trigger a device shake.")]
        [SerializeField] private float shakeThreshold = 2.5f;

        [Header("Editor / WebGL / PC Fallback Settings")]
        [Tooltip("Allow rapid horizontal mouse shaking to trigger the shake event.")]
        [SerializeField] private bool enableMouseShakeFallback = true;

        [Tooltip("Normalized screen-width velocity threshold required for mouse shake reversals.")]
        [SerializeField] private float mouseShakeSpeedThreshold = 3.2f;

        [Tooltip("Number of rapid direction reversals required within the time window.")]
        [SerializeField] private int requiredMouseReversals = 3;

        [Tooltip("Time window (seconds) before mouse reversal counter resets.")]
        [SerializeField] private float mouseShakeWindow = 0.45f;

        [Tooltip("Allow Spacebar key press as a quick testing shortcut in Editor/PC.")]
        [SerializeField] private bool enableKeyboardShortcut = true;

        [SerializeField] private KeyCode shortcutKey = KeyCode.Space;

        [Header("Cooldown")]
        [Tooltip("Minimum cooldown time in seconds between consecutive shake triggers.")]
        [SerializeField] private float cooldownDuration = 3.5f;

        private Vector3 _lastAcceleration;
        private Vector3 _lastMousePosition;
        private float _lastTriggerTime = -999f;

        // Mouse shake tracking
        private int _mouseDirectionSign = 0;
        private int _mouseReversalCount = 0;
        private float _lastReversalTime = 0f;

        private void Start()
        {
            _lastAcceleration = Input.acceleration;
            _lastMousePosition = Input.mousePosition;
        }

        private void Update()
        {
            float currentTime = Time.unscaledTime;

            // Always keep tracking baselines fresh
            Vector3 currentAcceleration = Input.acceleration;
            Vector3 accelerationDelta = currentAcceleration - _lastAcceleration;
            _lastAcceleration = currentAcceleration;

            Vector3 currentMousePos = Input.mousePosition;
            Vector3 mouseDelta = currentMousePos - _lastMousePosition;
            _lastMousePosition = currentMousePos;

            // Enforce cooldown
            if (currentTime - _lastTriggerTime < cooldownDuration)
            {
                return;
            }

            // 1. Mobile Accelerometer Shake Detection
            if (accelerationDelta.sqrMagnitude >= shakeThreshold * shakeThreshold)
            {
                TriggerShake("Accelerometer Shake");
                return;
            }

            // 2. Keyboard Shortcut Fallback (Spacebar)
            if (enableKeyboardShortcut && Input.GetKeyDown(shortcutKey))
            {
                TriggerShake($"Keyboard Shortcut ({shortcutKey})");
                return;
            }

            // 3. Rapid Mouse Shake Fallback (Editor / WebGL / PC)
            if (enableMouseShakeFallback && DetectRapidMouseShake(mouseDelta, currentTime))
            {
                TriggerShake("Rapid Mouse Shake");
            }
        }

        private bool DetectRapidMouseShake(Vector3 mouseDelta, float currentTime)
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float screenWidth = Mathf.Max(1f, Screen.width);
            float normalizedVelocityX = (mouseDelta.x / screenWidth) / dt;

            if (Mathf.Abs(normalizedVelocityX) < mouseShakeSpeedThreshold)
            {
                if (currentTime - _lastReversalTime > mouseShakeWindow)
                {
                    _mouseReversalCount = 0;
                    _mouseDirectionSign = 0;
                }
                return false;
            }

            int currentSign = normalizedVelocityX > 0f ? 1 : -1;
            if (_mouseDirectionSign == 0)
            {
                _mouseDirectionSign = currentSign;
                _mouseReversalCount = 1;
                _lastReversalTime = currentTime;
            }
            else if (currentSign != _mouseDirectionSign)
            {
                if (currentTime - _lastReversalTime <= mouseShakeWindow)
                {
                    _mouseDirectionSign = currentSign;
                    _mouseReversalCount++;
                    _lastReversalTime = currentTime;

                    if (_mouseReversalCount >= requiredMouseReversals)
                    {
                        _mouseReversalCount = 0;
                        _mouseDirectionSign = 0;
                        return true;
                    }
                }
                else
                {
                    _mouseDirectionSign = currentSign;
                    _mouseReversalCount = 1;
                    _lastReversalTime = currentTime;
                }
            }

            return false;
        }

        private void TriggerShake(string source)
        {
            _lastTriggerTime = Time.unscaledTime;
            _mouseReversalCount = 0;
            _mouseDirectionSign = 0;

            Debug.Log($"[ShakeInputDetector] Shake triggered via {source}!");
            OnShakeTriggered?.Invoke();
        }
    }
}

