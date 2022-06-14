using System;

using UnityEngine;

namespace Sandbox.Helpers.Debug
{
#if UNITY_EDITOR
    public static class GizmoManager
    {
        public static void OnDrawGizmos(Action action) { Handler.DrawGizmos += action; }

        public static void OnDrawGizmosSelected(Action action) { Handler.DrawGizmosSelected += action; }

        private static GizmoSystemHandler Handler => _handler != null ? _handler : (_handler = CreateHandler());

        private static GizmoSystemHandler _handler;

        private static GizmoSystemHandler CreateHandler()
        {
            var go = new GameObject("Gizmo Handler") { hideFlags = HideFlags.DontSave };

            return go.AddComponent<GizmoSystemHandler>();
        }
    }
#endif
}