using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(FeedbackButton), true), CanEditMultipleObjects]
    public class FeedbackButtonEditor : UnityEditor.UI.ButtonEditor
    {
        private SerializedProperty _punchEnabled, _punchTarget, _punchStrength, _punchDuration;
        private SerializedProperty _punchVibrato, _punchElasticity, _soundEnabled, _clickSound;

        protected override void OnEnable()
        {
            base.OnEnable();
            _punchEnabled = serializedObject.FindProperty("punchEnabled");
            _punchTarget = serializedObject.FindProperty("punchTarget");
            _punchStrength = serializedObject.FindProperty("punchStrength");
            _punchDuration = serializedObject.FindProperty("punchDuration");
            _punchVibrato = serializedObject.FindProperty("punchVibrato");
            _punchElasticity = serializedObject.FindProperty("punchElasticity");
            _soundEnabled = serializedObject.FindProperty("soundEnabled");
            _clickSound = serializedObject.FindProperty("clickSound");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Button Feedback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_punchEnabled);
            using (new EditorGUI.DisabledScope(!_punchEnabled.hasMultipleDifferentValues && !_punchEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(_punchTarget);
                EditorGUILayout.PropertyField(_punchStrength);
                EditorGUILayout.PropertyField(_punchDuration);
                EditorGUILayout.PropertyField(_punchVibrato);
                EditorGUILayout.PropertyField(_punchElasticity);
            }
            EditorGUILayout.PropertyField(_soundEnabled);
            using (new EditorGUI.DisabledScope(!_soundEnabled.hasMultipleDifferentValues && !_soundEnabled.boolValue))
                EditorGUILayout.PropertyField(_clickSound);
            DrawAdditionalProperties();
            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawAdditionalProperties() { }
    }

    [CustomEditor(typeof(HoldButton)), CanEditMultipleObjects]
    public sealed class HoldButtonEditor : FeedbackButtonEditor
    {
        private SerializedProperty _requireHold, _holdSeconds, _rewindSeconds, _progressFill, _progressMask, _onHoldCompleted;

        protected override void OnEnable()
        {
            base.OnEnable();
            _requireHold = serializedObject.FindProperty("requireHold");
            _holdSeconds = serializedObject.FindProperty("holdSeconds");
            _rewindSeconds = serializedObject.FindProperty("rewindSeconds");
            _progressFill = serializedObject.FindProperty("progressFill");
            _progressMask = serializedObject.FindProperty("progressMask");
            _onHoldCompleted = serializedObject.FindProperty("onHoldCompleted");
        }

        protected override void DrawAdditionalProperties()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hold Confirmation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_requireHold);
            EditorGUILayout.PropertyField(_holdSeconds);
            EditorGUILayout.PropertyField(_rewindSeconds);
            EditorGUILayout.PropertyField(_progressFill);
            EditorGUILayout.PropertyField(_progressMask);
            EditorGUILayout.PropertyField(_onHoldCompleted);
        }
    }
}
