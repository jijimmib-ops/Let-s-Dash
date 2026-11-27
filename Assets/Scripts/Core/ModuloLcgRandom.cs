using UnityEngine;

namespace LetsDash.Core
{
    /// <summary>
    /// Pure Mathematical Linear Congruential Generator (LCG) + Modulo (%) selector.
    /// Zero dependency on UnityEngine.Random, System.Random, or system clock/time functions.
    /// Formula: Seed_new = (Seed_old * A + C + Input) % M
    /// Result (0..11): Seed_new % 12
    /// </summary>
    public static class ModuloLcgRandom
    {
        public const ulong MultiplierA = 1103515245UL;
        public const ulong IncrementC = 12345UL;
        public const ulong ModulusM = 2147483647UL;
        public const ulong DefaultSeed = 12345UL;

        private const string CachedSeedKey = "LetsDash_LCG_Seed";

        private static ulong currentSeed = DefaultSeed;
        private static bool initialized;

        public static int CurrentSeed
        {
            get
            {
                EnsureInitialized();
                return (int)currentSeed;
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            int saved = PlayerPrefs.GetInt(CachedSeedKey, (int)DefaultSeed);
            currentSeed = saved > 0 ? (ulong)saved % ModulusM : DefaultSeed;
            if (currentSeed == 0UL) currentSeed = DefaultSeed;
            initialized = true;
        }

        public static void SetSeed(int seed)
        {
            EnsureInitialized();
            currentSeed = seed > 0 ? (ulong)seed % ModulusM : DefaultSeed;
            if (currentSeed == 0UL) currentSeed = DefaultSeed;
            SaveSeed();
        }

        /// <summary>
        /// Updates in-memory LCG seed using player input (e.g. W=1, A=2, S=3, D=4).
        /// </summary>
        public static int FeedInput(int inputValue)
        {
            EnsureInitialized();
            ulong safeInput = inputValue >= 0 ? (ulong)inputValue : 0UL;
            currentSeed = (currentSeed * MultiplierA + IncrementC + safeInput) % ModulusM;
            return (int)currentSeed;
        }

        /// <summary>
        /// Steps the LCG formula and returns Seed_new % modulo (e.g., modulo 12 -> 0..11).
        /// </summary>
        public static int NextMod(int modulo, int inputValue = 0)
        {
            if (modulo <= 0) return 0;
            FeedInput(inputValue);
            return (int)(currentSeed % (ulong)modulo);
        }

        /// <summary>
        /// Pure mathematical LCG [min, max] range without UnityEngine.Random.
        /// </summary>
        public static float NextRange(float min, float max, int inputValue = 0)
        {
            float unit = NextMod(10000, inputValue) / 10000f;
            return min + (max - min) * unit;
        }

        public static void SaveSeed()
        {
            if (!initialized) return;
            PlayerPrefs.SetInt(CachedSeedKey, (int)currentSeed);
            PlayerPrefs.Save();
        }
    }
}
