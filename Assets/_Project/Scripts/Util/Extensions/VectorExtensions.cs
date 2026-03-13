using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for Vector2 and Vector3.
    /// The With() pattern avoids the verbose "copy, modify, assign" dance
    /// that Unity vectors require because they're value types.
    /// </summary>
    public static class VectorExtensions
    {
        /// <summary>
        /// Return a copy with specific axes replaced.
        /// Pass only the axes you want to change.
        /// 
        ///   // Set X to lane position, keep Y and Z
        ///   transform.position = transform.position.With(x: lanePositions[lane]);
        /// 
        ///   // Set Y to scroll position
        ///   note.position = note.position.With(y: screenY);
        /// 
        ///   // Set multiple axes
        ///   transform.position = transform.position.With(x: 0f, y: 5f);
        /// </summary>
        public static Vector3 With(this Vector3 v, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(
                x ?? v.x,
                y ?? v.y,
                z ?? v.z
            );
        }

        /// <summary>
        /// Return a copy with specific axes replaced.
        /// 
        ///   uiElement.anchoredPosition = uiElement.anchoredPosition.With(x: targetX);
        /// </summary>
        public static Vector2 With(this Vector2 v, float? x = null, float? y = null)
        {
            return new Vector2(
                x ?? v.x,
                y ?? v.y
            );
        }

        /// <summary>
        /// Add to specific axes only.
        /// 
        ///   // Move note down the highway
        ///   note.position = note.position.Add(y: -scrollSpeed * deltaTime);
        /// </summary>
        public static Vector3 Add(this Vector3 v, float x = 0f, float y = 0f, float z = 0f)
        {
            return new Vector3(v.x + x, v.y + y, v.z + z);
        }

        /// <summary>
        /// Add to specific axes only.
        /// </summary>
        public static Vector2 Add(this Vector2 v, float x = 0f, float y = 0f)
        {
            return new Vector2(v.x + x, v.y + y);
        }

        /// <summary>
        /// Flatten a Vector3 to the XZ plane (y = 0).
        /// Useful for 2D distance checks in 3D space.
        /// </summary>
        public static Vector3 Flat(this Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
