using UnityEditor;
using UnityEngine;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.EditorTools
{
    /// <summary>
    /// CombatReceiver가 보유한 DOD 체력 값을 Inspector에서 읽기 전용으로 표시합니다.
    /// </summary>
    [CustomEditor(typeof(CombatReceiver))]
    public class CombatReceiverEditor : Editor
    {
        private void OnEnable()
        {
            // DOD 체력은 직렬화된 필드가 아니므로 Play Mode에서 최신 값을 보이도록 Inspector를 갱신합니다.
            EditorApplication.update += RepaintDuringPlayMode;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintDuringPlayMode;
        }

        private void RepaintDuringPlayMode()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CombatReceiver receiver = (CombatReceiver)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Current Health", receiver.CurrentHealth);
                EditorGUILayout.IntField("Max Health", receiver.MaxHealth);
            }
        }
    }
}
