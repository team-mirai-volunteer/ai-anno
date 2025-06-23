using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace AiTuber.Editor.Agent.MCPFix
{
    /// <summary>
    /// MCP (Unity Model Control Protocol) パッケージの ConsoleWindowUtility エラーを手動修正するツール
    /// Unity 2023/2024 でのコンパイルエラーを手動で修正
    /// </summary>
    public static class MCPConsoleFixManual
    {
        private const string MENU_PATH = "Agent Tools/Fix ConsoleWindowUtility Error";

        [MenuItem(MENU_PATH)]
        public static void FixConsoleWindowUtilityError()
        {
            Debug.Log("[MCPConsoleFixManual] Starting MCP ConsoleWindowUtility fix...");

            try
            {
                // ファイル検索
                var targetFile = MCPConsoleFixCore.FindMCPConsoleFile();
                
                if (string.IsNullOrEmpty(targetFile))
                {
                    EditorUtility.DisplayDialog("MCP Fix Error", 
                        "Could not find CustomLogManager.cs file.\n\n" +
                        "Expected location:\nLibrary/PackageCache/io.github.hatayama.umcp@*/Editor/Tools/ConsoleLogFetcher/CustomLogManager.cs\n\n" +
                        "Please ensure the MCP package is installed.", 
                        "OK");
                    return;
                }

                // 既に修正済みかチェック
                if (MCPConsoleFixCore.IsFileAlreadyFixed(targetFile))
                {
                    EditorUtility.DisplayDialog("MCP Fix", 
                        "The file has already been fixed.", 
                        "OK");
                    Debug.Log("[MCPConsoleFixManual] File already fixed.");
                    return;
                }

                // ConsoleWindowUtility の使用をチェック
                if (!MCPConsoleFixCore.HasProblematicCode(targetFile))
                {
                    EditorUtility.DisplayDialog("MCP Fix", 
                        "ConsoleWindowUtility error not found in the file.\n" +
                        "The file may have been updated or modified.", 
                        "OK");
                    Debug.Log("[MCPConsoleFixManual] ConsoleWindowUtility not found in file.");
                    return;
                }

                // 修正実行
                if (MCPConsoleFixCore.ApplyConsoleFix(targetFile))
                {
                    // Unity エディタをリフレッシュ
                    MCPConsoleFixCore.TriggerRecompile();

                    EditorUtility.DisplayDialog("MCP Fix Success", 
                        "ConsoleWindowUtility error has been fixed successfully!\n\n" +
                        "The Unity Editor will now recompile.", 
                        "OK");

                    Debug.Log("[MCPConsoleFixManual] Fix applied successfully!");
                    Debug.Log($"[MCPConsoleFixManual] Modified file: {targetFile}");
                }
                else
                {
                    EditorUtility.DisplayDialog("MCP Fix Error", 
                        "Failed to apply the fix. Please check the Console for details.", 
                        "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("MCP Fix Error", 
                    $"An error occurred while fixing the file:\n\n{ex.Message}", 
                    "OK");
                Debug.LogError($"[MCPConsoleFixManual] Error: {ex}");
            }
        }
    }
}