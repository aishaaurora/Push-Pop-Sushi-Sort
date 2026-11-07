using System;
using System.Collections;
using UnityEngine;

namespace SushiSort.Core
{
    /// <summary>
    /// Master Audio Manager for Push Pop Sushi Sort.
    /// Manages sound effects (SFX) and background music (BGM).
    /// Decoupled via automatic subscriptions to GamePlayManager events.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("Audio source dedicated to one-shot sound effects (SFX).")]
        [SerializeField] private AudioSource sfxSource;

        [Tooltip("Audio source dedicated to background music (BGM).")]
        [SerializeField] private AudioSource bgmSource;

        [Header("Gameplay SFX Clips")]
        [Tooltip("SFX played when sushi pieces are elevated/popped from a tube.")]
        [SerializeField] private AudioClip clipPop;

        [Tooltip("SFX played when sushi piece settles/drops into a tube slot.")]
        [SerializeField] private AudioClip clipDrop;

        [Tooltip("SFX played on invalid selection, illegal move, or tube shake.")]
        [SerializeField] private AudioClip clipInvalid;

        [Tooltip("SFX played when a locked tube is unlocked or a tube is completed.")]
        [SerializeField] private AudioClip clipUnlock;

        [Header("Game State SFX Clips")]
        [Tooltip("SFX fanfare played when all tubes are sorted and level is won.")]
        [SerializeField] private AudioClip clipWin;

        [Tooltip("SFX played on puzzle deadlock / game over.")]
        [SerializeField] private AudioClip clipDeadlock;

        [Header("BGM Clips & Settings")]
        [Tooltip("Looping background music audio track.")]
        [SerializeField] private AudioClip bgmLoop;

        [Tooltip("Automatically start playing BGM on game start.")]
        [SerializeField] private bool playBgmOnStart = true;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.35f;

        [Header("Juiciness & Feel")]
        [Tooltip("Applies subtle pitch variation to repetitive SFX like pops and drops.")]
        [SerializeField] private bool usePitchVariation = true;
        [Range(0.8f, 1.2f)] [SerializeField] private float minPitch = 0.94f;
        [Range(0.8f, 1.2f)] [SerializeField] private float maxPitch = 1.06f;

        // Timestamps to prevent audio spam on simultaneous events
        private float _lastDropTime = -1f;
        private float _lastUnlockTime = -1f;
        private Coroutine _duckBgmCoroutine;

        // Public Volume Accessors
        public float SFXVolume => sfxVolume;
        public float BGMVolume => bgmVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AutoConfigureSources();
        }

        private void OnEnable()
        {
            GamePlayManager.OnLevelStarted += HandleLevelStarted;
            GamePlayManager.OnPiecePopped += HandlePiecePopped;
            GamePlayManager.OnPieceDropped += HandlePieceDropped;
            GamePlayManager.OnMoveSuccess += HandleMoveSuccess;
            GamePlayManager.OnInvalidMove += HandleInvalidMove;
            GamePlayManager.OnTubeCompleted += HandleTubeCompleted;
            GamePlayManager.OnTubeUnlocked += HandleTubeUnlocked;
            GamePlayManager.OnLevelCompleted += HandleLevelCompleted;
            GamePlayManager.OnGameOver += HandleGameOver;
            GamePlayManager.OnDeadlockDetected += HandleGameOver;
        }

        private void OnDisable()
        {
            GamePlayManager.OnLevelStarted -= HandleLevelStarted;
            GamePlayManager.OnPiecePopped -= HandlePiecePopped;
            GamePlayManager.OnPieceDropped -= HandlePieceDropped;
            GamePlayManager.OnMoveSuccess -= HandleMoveSuccess;
            GamePlayManager.OnInvalidMove -= HandleInvalidMove;
            GamePlayManager.OnTubeCompleted -= HandleTubeCompleted;
            GamePlayManager.OnTubeUnlocked -= HandleTubeUnlocked;
            GamePlayManager.OnLevelCompleted -= HandleLevelCompleted;
            GamePlayManager.OnGameOver -= HandleGameOver;
            GamePlayManager.OnDeadlockDetected -= HandleGameOver;
        }

        private void Start()
        {
            if (playBgmOnStart && bgmLoop != null)
            {
                StartBGM(bgmLoop);
            }
        }

        /// <summary>
        /// Auto-binds or creates AudioSource components if not manually assigned in the Inspector.
        /// </summary>
        private void AutoConfigureSources()
        {
            if (sfxSource == null)
            {
                // Look for a child named "SFX" or create one
                Transform sfxChild = transform.Find("SFX_Source");
                if (sfxChild != null)
                {
                    sfxSource = sfxChild.GetComponent<AudioSource>() ?? sfxChild.gameObject.AddComponent<AudioSource>();
                }
                else
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
            }

            if (bgmSource == null)
            {
                // Look for a child named "BGM" or create one
                Transform bgmChild = transform.Find("BGM_Source");
                if (bgmChild != null)
                {
                    bgmSource = bgmChild.GetComponent<AudioSource>() ?? bgmChild.gameObject.AddComponent<AudioSource>();
                }
                else
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                }
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume;
            }
        }

        #region Event Subscriptions Handlers

        private void HandleLevelStarted()
        {
            // Ensure BGM is actively playing and at normal level
            if (bgmSource != null && bgmLoop != null && !bgmSource.isPlaying)
            {
                StartBGM(bgmLoop);
            }
            else if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume;
            }
        }

        private void HandlePiecePopped()
        {
            PlayPop();
        }

        private void HandlePieceDropped()
        {
            PlayDrop();
            _lastDropTime = Time.time;
        }

        private void HandleMoveSuccess()
        {
            // If individual piece drop sound wasn't played in the last 0.15s, play drop as fallback
            if (Time.time - _lastDropTime > 0.15f)
            {
                PlayDrop();
            }
        }

        private void HandleInvalidMove()
        {
            PlayInvalid();
        }

        private void HandleTubeCompleted(SushiType completedType)
        {
            PlayUnlock();
        }

        private void HandleTubeUnlocked()
        {
            PlayUnlock();
        }

        private void HandleLevelCompleted()
        {
            PlayWin();

            // Duck BGM temporarily so the victory fanfare sounds punchy
            if (_duckBgmCoroutine != null) StopCoroutine(_duckBgmCoroutine);
            _duckBgmCoroutine = StartCoroutine(DuckBGMRoutine(0.12f, 2.5f));
        }

        private void HandleGameOver()
        {
            PlayDeadlock();
        }

        #endregion

        #region Public Play SFX Methods

        /// <summary>
        /// Plays a sound effect with null-safety and scaled volume.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale * sfxVolume));
        }

        /// <summary>
        /// Plays a sound effect with optional pitch variation for casual tactile juiciness.
        /// </summary>
        public void PlaySFXWithPitch(AudioClip clip, float minP = 0.94f, float maxP = 1.06f, float volumeScale = 1.0f)
        {
            if (clip == null || sfxSource == null) return;

            if (usePitchVariation)
            {
                sfxSource.pitch = UnityEngine.Random.Range(minP, maxP);
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale * sfxVolume));
                sfxSource.pitch = 1.0f;
            }
            else
            {
                PlaySFX(clip, volumeScale);
            }
        }

        /// <summary>
        /// Plays sushi pop/elevation sound.
        /// </summary>
        public void PlayPop()
        {
            PlaySFXWithPitch(clipPop, minPitch, maxPitch);
        }

        /// <summary>
        /// Plays sushi slot drop/placement sound.
        /// </summary>
        public void PlayDrop()
        {
            PlaySFXWithPitch(clipDrop, minPitch, maxPitch);
        }

        /// <summary>
        /// Plays invalid move / illegal action buzzer or shake sound.
        /// </summary>
        public void PlayInvalid()
        {
            PlaySFX(clipInvalid);
        }

        /// <summary>
        /// Plays tube unlocked or tube completed chime. Debounced to avoid duplicate sounds in single frame.
        /// </summary>
        public void PlayUnlock()
        {
            if (Time.time - _lastUnlockTime < 0.15f) return;
            _lastUnlockTime = Time.time;

            PlaySFX(clipUnlock);
        }

        /// <summary>
        /// Plays level completion victory fanfare.
        /// </summary>
        public void PlayWin()
        {
            PlaySFX(clipWin);
        }

        /// <summary>
        /// Plays deadlock / game over sound.
        /// </summary>
        public void PlayDeadlock()
        {
            PlaySFX(clipDeadlock);
        }

        #endregion

        #region Public BGM Methods

        /// <summary>
        /// Starts looping BGM with the specified audio clip.
        /// </summary>
        public void StartBGM(AudioClip clip, bool loop = true)
        {
            if (bgmSource == null || clip == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        /// <summary>
        /// Stops background music immediately.
        /// </summary>
        public void StopBGM()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
        }

        /// <summary>
        /// Pauses background music.
        /// </summary>
        public void PauseBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Pause();
            }
        }

        /// <summary>
        /// Resumes background music.
        /// </summary>
        public void ResumeBGM()
        {
            if (bgmSource != null && !bgmSource.isPlaying)
            {
                bgmSource.UnPause();
            }
        }

        /// <summary>
        /// Sets master SFX volume.
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Sets master BGM volume and updates active BGM source.
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume;
            }
        }

        private IEnumerator DuckBGMRoutine(float duckedVolume, float restoreDelay)
        {
            if (bgmSource == null) yield break;

            float originalVol = bgmVolume;
            bgmSource.volume = duckedVolume;

            yield return new WaitForSeconds(restoreDelay);

            // Smoothly restore volume
            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (bgmSource != null)
                {
                    bgmSource.volume = Mathf.Lerp(duckedVolume, originalVol, elapsed / duration);
                }
                yield return null;
            }

            if (bgmSource != null)
            {
                bgmSource.volume = originalVol;
            }
            _duckBgmCoroutine = null;
        }

        #endregion
    }
}

