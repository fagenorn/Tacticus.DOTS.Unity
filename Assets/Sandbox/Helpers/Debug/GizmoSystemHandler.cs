using System;

using UnityEditor;

using UnityEngine;

namespace Sandbox.Helpers.Debug
{
#if (UNITY_EDITOR)
    public class GizmoSystemHandler : MonoBehaviour
    {
        public Action DrawGizmos = () => { };

        public Action DrawGizmosSelected = () => { };

        private void OnDrawGizmos()
        {
            if ( EditorApplication.isPlaying )
            {
                DrawGizmos?.Invoke();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if ( EditorApplication.isPlaying )
            {
                DrawGizmosSelected?.Invoke();
            }
        }
    }
#endif
}