using UnityEditor;
using UnityEngine;

namespace MultiAgentNPC.Services.EditorTools
{
    /// <summary>
    /// Adds Test LLM / TTS / STT buttons to the <see cref="ExternalServiceTester"/>
    /// Inspector, in addition to the component context-menu entries.
    /// </summary>
    [CustomEditor(typeof(ExternalServiceTester))]
    public class ExternalServiceTesterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var tester = (ExternalServiceTester)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connectivity Tests", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode for reliable async results and TTS playback.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Test LLM"))
                {
                    tester.TestLLM();
                }

                if (GUILayout.Button("Test TTS"))
                {
                    tester.TestTTS();
                }

                if (GUILayout.Button("Test STT"))
                {
                    tester.TestSTT();
                }
            }
        }
    }
}
