using System.Collections.Generic;
using UnityEngine;

namespace CustomizableUI.Utilities
{
    internal static class TransformExtensions
    {
        /// <summary>
        /// Breadth-first search for a descendant by exact GameObject name. The legacy mod assumed
        /// a fixed hierarchy depth; Redux's flight HUD layout isn't guaranteed to match, so this
        /// searches the whole subtree instead of a hardcoded path.
        /// </summary>
        internal static Transform FindDescendant(this Transform root, string name)
        {
            if (root == null)
                return null;

            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name == name)
                        return child;
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        /// <summary>
        /// Redux commonly wraps a flight-HUD group's real, visually-sized widget in an invisible
        /// full-canvas stretch container (anchorMin (0,0), anchorMax (1,1), zero sizeDelta -- i.e.
        /// no inset from the stretch). Confirmed from a live diagnostic dump: 17 of 21 discovered
        /// groups resolved to exactly that signature, with a world position sitting dead-center of
        /// the screen regardless of where the group actually renders. So instead of taking the
        /// first RectTransform found (which is always the outer wrapper), this searches the whole
        /// subtree breadth-first and prefers the shallowest RectTransform that ISN'T a full-stretch
        /// wrapper -- that's the actual positioned widget. Falls back to the first RectTransform
        /// found (old behavior) if every candidate in the subtree turns out to be a wrapper.
        /// </summary>
        internal static Transform ResolvePositionable(this Transform root)
        {
            const int maxNodesToVisit = 500;

            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            Transform firstAny = null;
            var visited = 0;

            while (queue.Count > 0 && visited < maxNodesToVisit)
            {
                var current = queue.Dequeue();
                visited++;

                if (current.TryGetComponent<RectTransform>(out var rt))
                {
                    firstAny ??= current;
                    if (!IsFullStretchWrapper(rt))
                        return current;
                }

                for (var i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return firstAny != null ? firstAny : root;
        }

        private static bool IsFullStretchWrapper(RectTransform rt) =>
            rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && rt.sizeDelta == Vector2.zero;
    }
}
