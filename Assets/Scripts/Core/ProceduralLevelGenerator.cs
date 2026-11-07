using System;
using System.Collections.Generic;
using UnityEngine;
using SushiSort.ScriptableObjects;

namespace SushiSort.Core
{
    /// <summary>
    /// Pure static procedural level generator for Push Pop Sushi Sort.
    /// Utilizes a Solvable Reverse Simulation Shuffle starting from a completed puzzle state,
    /// guaranteeing 100% mathematical solvability for procedural levels (Level 11+).
    /// </summary>
    public static class ProceduralLevelGenerator
    {
        public const int TubeCapacity = 4;
        public const int DefaultBufferTubeCount = 2;

        /// <summary>
        /// Generates a complete, mathematically solvable LevelDataSO asset at runtime.
        /// </summary>
        /// <param name="levelNumber">1-based level index.</param>
        /// <param name="database">Master database containing sushi definitions.</param>
        /// <returns>A configured runtime LevelDataSO instance.</returns>
        public static LevelDataSO GenerateLevel(int levelNumber, LevelDatabaseSO database)
        {
            if (levelNumber < 1) levelNumber = 1;

            // 1. Determine Difficulty Cycle (5-level repeating cycle)
            // Cycle indices 0, 1, 2 = Normal | Cycle indices 3, 4 = Hard
            int cycleIndex = (levelNumber - 1) % 5;
            bool isHard = (cycleIndex == 3 || cycleIndex == 4);

            // 2. Determine Active Color Count (3 to 6 colors)
            int colorCount = CalculateActiveColorCount(levelNumber, isHard);

            // 3. Determine Shuffle Steps
            int shuffleSteps = isHard 
                ? UnityEngine.Random.Range(60, 81) 
                : UnityEngine.Random.Range(35, 51);

            // 4. Determine Mystery and Locked counts
            int mysteryCount = isHard ? UnityEngine.Random.Range(1, 3) : 0;
            int lockedCount = (isHard && (cycleIndex == 4 || UnityEngine.Random.value < 0.5f)) ? 1 : 0;

            // 5. Generate Shuffled Tube Data
            List<TubeInitialData> tubeDataList = GenerateShuffledTubeData(
                colorCount,
                DefaultBufferTubeCount,
                shuffleSteps,
                mysteryCount,
                lockedCount,
                database
            );

            // 6. Package into LevelDataSO ScriptableObject
            LevelDataSO runtimeLevel = ScriptableObject.CreateInstance<LevelDataSO>();
            runtimeLevel.name = $"Procedural_Level_{levelNumber}";
            runtimeLevel.InitializeRuntime(levelNumber, TubeCapacity, tubeDataList);

            return runtimeLevel;
        }

        /// <summary>
        /// Generates a custom list of TubeInitialData using the reverse simulation shuffle algorithm.
        /// Shared between procedural runtime generation and editor LevelDataSO generation.
        /// </summary>
        public static List<TubeInitialData> GenerateShuffledTubeData(
            int colorCount,
            int emptyBufferTubes,
            int shuffleSteps,
            int mysteryTubeCount,
            int lockedTubeCount,
            LevelDatabaseSO database)
        {
            colorCount = Mathf.Clamp(colorCount, 2, 6);
            emptyBufferTubes = Mathf.Clamp(emptyBufferTubes, 1, 3);
            shuffleSteps = Mathf.Clamp(shuffleSteps, 10, 200);

            // 1. Select Active Sushi Types
            List<SushiType> activeTypes = SelectActiveSushiTypes(colorCount, database);
            int filledTubeCount = activeTypes.Count;
            int totalTubeCount = filledTubeCount + emptyBufferTubes;

            List<List<SushiType>> simulatedTubes = new List<List<SushiType>>(totalTubeCount);

            // 2. Initialize Solved State (filled tubes + empty buffer tubes)
            for (int i = 0; i < filledTubeCount; i++)
            {
                var tubeStack = new List<SushiType>(TubeCapacity);
                SushiType type = activeTypes[i];
                for (int piece = 0; piece < TubeCapacity; piece++)
                {
                    tubeStack.Add(type);
                }
                simulatedTubes.Add(tubeStack);
            }

            for (int i = 0; i < emptyBufferTubes; i++)
            {
                simulatedTubes.Add(new List<SushiType>(TubeCapacity));
            }

            // 3. Execute Solvable Backward Step Simulation
            ExecuteReverseShuffle(simulatedTubes, filledTubeCount, shuffleSteps);

            // 4. Consolidate Buffer Tubes (Guarantee buffer tubes are 100% empty)
            ConsolidateBufferTubes(simulatedTubes, filledTubeCount);

            // 5. Special Features: Locked Tubes (Deadlock Free)
            List<int> lockedTubeIndices = new List<int>();
            Dictionary<int, SushiType> unlockRequirements = new Dictionary<int, SushiType>();

            if (lockedTubeCount > 0 && filledTubeCount > 0)
            {
                int targetLocks = Mathf.Min(lockedTubeCount, filledTubeCount - 1);
                for (int attempt = 0; attempt < 50 && lockedTubeIndices.Count < targetLocks; attempt++)
                {
                    int candidate = UnityEngine.Random.Range(0, filledTubeCount);
                    if (!lockedTubeIndices.Contains(candidate) && simulatedTubes[candidate].Count > 0)
                    {
                        // Find a type from activeTypes that is NOT contained inside candidate tube
                        SushiType validReq = SushiType.None;
                        foreach (var t in activeTypes)
                        {
                            if (!simulatedTubes[candidate].Contains(t))
                            {
                                validReq = t;
                                break;
                            }
                        }

                        if (validReq != SushiType.None)
                        {
                            lockedTubeIndices.Add(candidate);
                            unlockRequirements[candidate] = validReq;
                        }
                    }
                }
            }

            // 6. Special Features: Mystery Rolls
            HashSet<int> mysteryIndices = new HashSet<int>();
            if (mysteryTubeCount > 0 && filledTubeCount > 0)
            {
                int targetMystery = Mathf.Min(mysteryTubeCount, filledTubeCount);
                for (int attempt = 0; attempt < 50 && mysteryIndices.Count < targetMystery; attempt++)
                {
                    int candidate = UnityEngine.Random.Range(0, filledTubeCount);
                    // Avoid placing mystery in locked tube if possible
                    if (!mysteryIndices.Contains(candidate) && (!lockedTubeIndices.Contains(candidate) || targetMystery >= filledTubeCount))
                    {
                        mysteryIndices.Add(candidate);
                    }
                }
            }

            // 7. Build serialized TubeInitialData list
            List<TubeInitialData> result = new List<TubeInitialData>(totalTubeCount);
            for (int i = 0; i < simulatedTubes.Count; i++)
            {
                bool isLocked = lockedTubeIndices.Contains(i);
                SushiType req = isLocked && unlockRequirements.ContainsKey(i) ? unlockRequirements[i] : SushiType.None;

                int mysteryCount = 0;
                if (mysteryIndices.Contains(i) && simulatedTubes[i].Count >= 2)
                {
                    // 1 or 2 bottom sushi pieces (never top)
                    mysteryCount = Mathf.Min(UnityEngine.Random.Range(1, 3), simulatedTubes[i].Count - 1);
                }

                List<SushiType> stackCopy = new List<SushiType>(simulatedTubes[i]);
                result.Add(new TubeInitialData(stackCopy, isLocked, req, mysteryCount));
            }

            return result;
        }

        #region Difficulty & Color Selection

        public static int CalculateActiveColorCount(int levelNumber, bool isHard)
        {
            // Tutorial levels (Level 1 & Level 2) have exactly 2 sushi color variants
            if (levelNumber <= 2)
            {
                return 2;
            }

            // Level 3..15 -> 3 to 4 colors
            // Level 16..25 -> 4 to 5 colors
            // Level 26+    -> 5 to 6 colors
            int baseColors = 3 + (levelNumber - 10) / 10;
            if (isHard)
            {
                baseColors += 1;
            }

            return Mathf.Clamp(baseColors, 2, 6);
        }

        public static List<SushiType> SelectActiveSushiTypes(int count, LevelDatabaseSO database)
        {
            List<SushiType> candidates = new List<SushiType>();

            // Retrieve from database if available
            if (database != null && database.AllSushiTypes != null && database.AllSushiTypes.Count > 0)
            {
                foreach (var sushiSO in database.AllSushiTypes)
                {
                    if (sushiSO != null && sushiSO.SushiType != SushiType.None && !candidates.Contains(sushiSO.SushiType))
                    {
                        candidates.Add(sushiSO.SushiType);
                    }
                }
            }

            // Fallback to enum values if database was empty
            if (candidates.Count == 0)
            {
                var enumValues = (SushiType[])Enum.GetValues(typeof(SushiType));
                foreach (var val in enumValues)
                {
                    if (val != SushiType.None)
                    {
                        candidates.Add(val);
                    }
                }
            }

            // Fisher-Yates shuffle
            for (int i = 0; i < candidates.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, candidates.Count);
                SushiType temp = candidates[i];
                candidates[i] = candidates[rnd];
                candidates[rnd] = temp;
            }

            int finalCount = Mathf.Min(count, candidates.Count);
            return candidates.GetRange(0, finalCount);
        }

        #endregion

        #region Reverse Shuffle Simulation

        /// <summary>
        /// Simulates legal backward steps from solved state to scrambled initial puzzle state.
        /// </summary>
        public static void ExecuteReverseShuffle(List<List<SushiType>> tubes, int filledCount, int stepCount)
        {
            int totalTubes = tubes.Count;
            int lastFrom = -1;
            int lastTo = -1;

            int safetyIterations = stepCount * 4;
            int stepsCompleted = 0;

            for (int iter = 0; iter < safetyIterations && stepsCompleted < stepCount; iter++)
            {
                // Gather valid source tubes: must have pieces
                List<int> validSources = new List<int>();
                for (int i = 0; i < totalTubes; i++)
                {
                    int c = tubes[i].Count;
                    if (c > 0)
                    {
                        if (c == 1 || tubes[i][c - 1] == tubes[i][c - 2])
                        {
                            validSources.Add(i);
                        }
                    }
                }

                // If strictly reversible sources are scarce, fall back to any non-empty tube
                if (validSources.Count == 0)
                {
                    for (int i = 0; i < totalTubes; i++)
                    {
                        if (tubes[i].Count > 0) validSources.Add(i);
                    }
                }

                if (validSources.Count == 0) break;

                // Select source, avoiding immediate reverse undo if alternatives exist
                int fromIndex = -1;
                if (validSources.Count > 1 && lastTo >= 0 && validSources.Contains(lastTo))
                {
                    List<int> nonUndoSources = validSources.FindAll(s => s != lastTo);
                    fromIndex = nonUndoSources.Count > 0 
                        ? nonUndoSources[UnityEngine.Random.Range(0, nonUndoSources.Count)]
                        : validSources[UnityEngine.Random.Range(0, validSources.Count)];
                }
                else
                {
                    fromIndex = validSources[UnityEngine.Random.Range(0, validSources.Count)];
                }

                // Gather valid destinations: cannot be same tube, cannot be full (< 4)
                List<int> validDests = new List<int>();
                for (int j = 0; j < totalTubes; j++)
                {
                    if (j != fromIndex && tubes[j].Count < TubeCapacity)
                    {
                        // Avoid immediate undo (lastTo -> lastFrom)
                        if (!(fromIndex == lastTo && j == lastFrom))
                        {
                            validDests.Add(j);
                        }
                    }
                }

                if (validDests.Count == 0)
                {
                    for (int j = 0; j < totalTubes; j++)
                    {
                        if (j != fromIndex && tubes[j].Count < TubeCapacity)
                        {
                            validDests.Add(j);
                        }
                    }
                }

                if (validDests.Count == 0) continue;

                int toIndex = validDests[UnityEngine.Random.Range(0, validDests.Count)];

                // Move 1 piece backwards: pop from source, push into destination
                int topIdx = tubes[fromIndex].Count - 1;
                SushiType movedPiece = tubes[fromIndex][topIdx];
                tubes[fromIndex].RemoveAt(topIdx);
                tubes[toIndex].Add(movedPiece);

                lastFrom = fromIndex;
                lastTo = toIndex;
                stepsCompleted++;
            }
        }

        /// <summary>
        /// Moves any pieces that ended up in the buffer tubes back into tubes 0..filledCount-1,
        /// ensuring the player always starts with exactly emptyBufferTubes empty buffer tubes and filled tubes of capacity 4.
        /// </summary>
        public static void ConsolidateBufferTubes(List<List<SushiType>> tubes, int filledCount)
        {
            int totalTubes = tubes.Count;

            for (int bufIdx = filledCount; bufIdx < totalTubes; bufIdx++)
            {
                while (tubes[bufIdx].Count > 0)
                {
                    int topIdx = tubes[bufIdx].Count - 1;
                    SushiType piece = tubes[bufIdx][topIdx];
                    tubes[bufIdx].RemoveAt(topIdx);

                    bool placed = false;
                    for (int mainIdx = 0; mainIdx < filledCount; mainIdx++)
                    {
                        if (tubes[mainIdx].Count < TubeCapacity)
                        {
                            tubes[mainIdx].Add(piece);
                            placed = true;
                            break;
                        }
                    }

                    if (!placed)
                    {
                        tubes[bufIdx].Add(piece);
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
