using System;
using UnityEngine;

namespace SushiSort.Core
{
    /// <summary>
    /// Manages the game's coin economy using PlayerPrefs (WebGL safe).
    /// Supports both MonoBehaviour Singleton in scene and automatic lazy instantiation.
    /// Broadcasts OnCoinsChanged whenever the coin balance updates.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        private const string COINS_KEY = "SushiSort_Coins";

        private static CurrencyManager _instance;
        public static CurrencyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CurrencyManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CurrencyManager");
                        _instance = go.AddComponent<CurrencyManager>();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Fired whenever the player's coin total changes. Passes the new coin balance.
        /// </summary>
        public static event Action<int> OnCoinsChanged;

        [Header("Debug / Runtime State")]
        [SerializeField] private int currentCoins = 0;

        private bool _isInitialized = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            InitializeCoins();
        }

        private void InitializeCoins()
        {
            if (_isInitialized) return;
            currentCoins = PlayerPrefs.GetInt(COINS_KEY, 0);
            _isInitialized = true;
        }

        /// <summary>
        /// Returns the current number of coins stored.
        /// </summary>
        public int GetCoins()
        {
            InitializeCoins();
            return currentCoins;
        }

        /// <summary>
        /// Adds the specified amount of coins and saves immediately to PlayerPrefs.
        /// </summary>
        /// <param name="amount">Positive amount of coins to add.</param>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;

            InitializeCoins();
            currentCoins += amount;
            SaveCoins();

            Debug.Log($"[CurrencyManager] Added {amount} coins. Total: {currentCoins}");
            OnCoinsChanged?.Invoke(currentCoins);
        }

        /// <summary>
        /// Attempts to spend the specified amount of coins.
        /// Returns true and deducts coins if balance is sufficient; otherwise returns false.
        /// </summary>
        /// <param name="amount">Amount of coins to spend.</param>
        public bool SpendCoins(int amount)
        {
            if (amount < 0) return false;

            InitializeCoins();
            if (currentCoins < amount)
            {
                Debug.Log($"[CurrencyManager] Not enough coins! Needed: {amount}, Current: {currentCoins}");
                return false;
            }

            currentCoins -= amount;
            SaveCoins();

            Debug.Log($"[CurrencyManager] Spent {amount} coins. Remaining: {currentCoins}");
            OnCoinsChanged?.Invoke(currentCoins);
            return true;
        }

        private void SaveCoins()
        {
            PlayerPrefs.SetInt(COINS_KEY, currentCoins);
            PlayerPrefs.Save(); // Explicit save for WebGL compatibility
        }

#if UNITY_EDITOR
        [ContextMenu("Add 100 Test Coins")]
        private void DebugAdd100Coins()
        {
            AddCoins(100);
        }

        [ContextMenu("Reset Coins to 0")]
        private void DebugResetCoins()
        {
            currentCoins = 0;
            SaveCoins();
            OnCoinsChanged?.Invoke(currentCoins);
            Debug.Log("[CurrencyManager] Coins reset to 0.");
        }
#endif
    }
}

