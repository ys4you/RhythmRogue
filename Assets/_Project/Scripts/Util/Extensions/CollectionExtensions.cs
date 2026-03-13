using System.Collections.Generic;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for collections (IList, IReadOnlyList).
    /// Integrated with ISeededRandom for deterministic operations.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Shuffle a list in-place using a seeded random source.
        /// Deterministic: same seed → same shuffle.
        /// 
        ///   // Shuffle reward pool with the run seed
        ///   rewardPool.Shuffle(seed.GetRandom(RandomDomain.Rewards));
        /// </summary>
        public static void Shuffle<T>(this IList<T> list, ISeededRandom rng)
        {
            rng.Shuffle(list);
        }

        /// <summary>
        /// Pick a random element using a seeded random source.
        /// Deterministic: same seed → same pick.
        /// 
        ///   string song = songList.Pick(rng);
        /// </summary>
        public static T Pick<T>(this IReadOnlyList<T> list, ISeededRandom rng)
        {
            return rng.Pick(list);
        }

        /// <summary>
        /// Check if a collection is null or empty.
        /// 
        ///   if (enemies.IsNullOrEmpty())
        ///       SpawnDefaultEnemy();
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>
        /// Get an element by index, or a default value if out of range.
        /// Avoids IndexOutOfRangeException for defensive coding.
        /// 
        ///   // Safe fallback for lane colors
        ///   Color color = laneColors.GetOrDefault(lane, Color.white);
        /// </summary>
        public static T GetOrDefault<T>(this IReadOnlyList<T> list, int index, T defaultValue = default)
        {
            if (list == null || index < 0 || index >= list.Count)
                return defaultValue;

            return list[index];
        }

        /// <summary>
        /// Return the first N elements of a list.
        /// 
        ///   // Show top 3 scores
        ///   var topScores = allScores.Take(3);
        /// </summary>
        public static List<T> Take<T>(this IReadOnlyList<T> list, int count)
        {
            int actual = count < list.Count ? count : list.Count;
            var result = new List<T>(actual);

            for (int i = 0; i < actual; i++)
                result.Add(list[i]);

            return result;
        }

        /// <summary>
        /// Return the last element.
        /// 
        ///   var lastNode = mapPath.Last();
        /// </summary>
        public static T Last<T>(this IReadOnlyList<T> list)
        {
            return list[list.Count - 1];
        }

        /// <summary>
        /// Return the first element.
        /// </summary>
        public static T First<T>(this IReadOnlyList<T> list)
        {
            return list[0];
        }
    }
}
