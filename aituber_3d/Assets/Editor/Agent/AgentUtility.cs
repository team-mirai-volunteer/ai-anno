using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace AgentTools
{
    /// <summary>
    /// Agent用のUnity Editor操作ユーティリティ
    /// MCPやAgent経由でUnityエディタを操作するためのメソッド群
    /// </summary>
    public static class AgentUtility
    {
        /// <summary>
        /// Unity Consoleをクリアする
        /// </summary>
        [MenuItem("Agent Tools/Clear Console")]
        public static void ClearConsole()
        {
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
            
            Debug.Log("Console cleared by Agent");
        }
        
        
        
        /// <summary>
        /// 強制コンパイルを実行する
        /// </summary>
        [MenuItem("Agent Tools/Force Recompile")]
        public static void ForceRecompile()
        {
            AssetDatabase.Refresh();
            EditorUtility.RequestScriptReload();
            Debug.Log("Force recompile requested by Agent");
        }
    }
}