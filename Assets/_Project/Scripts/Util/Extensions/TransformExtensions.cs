using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for Transform.
    /// Hierarchy manipulation used across UI screens,
    /// pool containers, and scene transitions.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Destroy all child GameObjects.
        /// 
        ///   // Clear the reward options container before generating new ones
        ///   rewardContainer.DestroyAllChildren();
        /// 
        ///   // Clear shop inventory UI
        ///   shopGrid.DestroyAllChildren();
        /// </summary>
        public static void DestroyAllChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Immediately destroy all child GameObjects.
        /// Use in editor scripts or when you need children gone this frame.
        /// </summary>
        public static void DestroyAllChildrenImmediate(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Reset local position, rotation, and scale to defaults.
        /// 
        ///   // After reparenting a pooled object
        ///   pooledObject.transform.ResetLocal();
        /// </summary>
        public static void ResetLocal(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Set only the local X position.
        /// 
        ///   // Position a note in its lane
        ///   noteTransform.SetLocalX(lanePositions[lane]);
        /// </summary>
        public static void SetLocalX(this Transform transform, float x)
        {
            Vector3 pos = transform.localPosition;
            pos.x = x;
            transform.localPosition = pos;
        }

        /// <summary>
        /// Set only the local Y position.
        /// 
        ///   // Scroll a note on the highway
        ///   noteTransform.SetLocalY(screenY);
        /// </summary>
        public static void SetLocalY(this Transform transform, float y)
        {
            Vector3 pos = transform.localPosition;
            pos.y = y;
            transform.localPosition = pos;
        }

        /// <summary>
        /// Set only the local Z position.
        /// </summary>
        public static void SetLocalZ(this Transform transform, float z)
        {
            Vector3 pos = transform.localPosition;
            pos.z = z;
            transform.localPosition = pos;
        }
    }
}
