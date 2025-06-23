using UnityEngine;
using UnityEditor;

namespace AiTuber.Editor.Agent.MCPFix
{
    /// <summary>
    /// Unity起動時に自動実行されるMCP ConsoleWindowUtility修正ツール
    /// コンパイルエラーが発生する前に問題を修正
    /// </summary>
    [InitializeOnLoad]
    public static class MCPConsoleFixAuto
    {
        private const string PREF_KEY = "MCPAutoFix_Applied";
        
        static MCPConsoleFixAuto()
        {
            // Unity起動時に一度だけ実行
            EditorApplication.delayCall += () =>
            {
                CheckAndFixMCPConsoleError();
            };
        }
        
        /// <summary>
        /// MCP ConsoleWindowUtilityエラーを自動検出・修正
        /// </summary>
        private static void CheckAndFixMCPConsoleError()
        {
            try
            {
                var targetFile = MCPConsoleFixCore.FindMCPConsoleFile();
                if (string.IsNullOrEmpty(targetFile))
                {
                    Debug.Log("[MCPConsoleFixAuto] MCP Console file not found. Skipping fix.");
                    return;
                }
                
                if (MCPConsoleFixCore.IsFileAlreadyFixed(targetFile))
                {
                    Debug.Log("[MCPConsoleFixAuto] MCP Console file already fixed. Skipping.");
                    return;
                }
                
                if (MCPConsoleFixCore.HasProblematicCode(targetFile))
                {
                    Debug.LogWarning("[MCPConsoleFixAuto] Detected MCP ConsoleWindowUtility error. Applying automatic fix...");
                    
                    if (MCPConsoleFixCore.ApplyConsoleFix(targetFile))
                    {
                        Debug.Log("[MCPConsoleFixAuto] ✅ MCP Console fix applied successfully. Unity will recompile automatically.");
                        EditorPrefs.SetString(PREF_KEY, System.DateTime.Now.ToString());
                        
                        // 自動リコンパイルをトリガー
                        MCPConsoleFixCore.TriggerRecompile();
                    }
                    else
                    {
                        Debug.LogError("[MCPConsoleFixAuto] ❌ Failed to apply MCP Console fix.");
                    }
                }
                else
                {
                    Debug.Log("[MCPConsoleFixAuto] No MCP Console errors detected.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MCPConsoleFixAuto] Error during automatic fix: {ex.Message}");
            }
        }
    }
}