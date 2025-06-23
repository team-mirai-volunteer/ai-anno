using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace AiTuber.Editor.Agent.MCPFix
{
    /// <summary>
    /// MCP ConsoleWindowUtility修正の共通ロジック
    /// 自動修正と手動修正の両方で使用される核となる機能
    /// </summary>
    public static class MCPConsoleFixCore
    {
        private const string TARGET_FILE_PATTERN = "Library/PackageCache/io.github.hatayama.umcp@*/Editor/Tools/ConsoleLogFetcher/CustomLogManager.cs";
        private const string PROBLEMATIC_CODE = "ConsoleWindowUtility.GetConsoleLogCounts";
        private const string FIXED_MARKER = "Unity 2023/2024対応: ConsoleWindowUtilityの代替実装";
        private const string REFLECTION_MARKER = "System.Type.GetType(\"UnityEditor.LogEntries,UnityEditor\")";

        /// <summary>
        /// MCP CustomLogManager.csファイルを検索
        /// </summary>
        /// <returns>見つかったファイルのフルパス、見つからない場合はnull</returns>
        public static string FindMCPConsoleFile()
        {
            var packageCacheDir = Path.Combine(Application.dataPath, "../Library/PackageCache");
            if (!Directory.Exists(packageCacheDir))
                return null;
                
            var mcpDirs = Directory.GetDirectories(packageCacheDir, "io.github.hatayama.umcp@*");
            
            foreach (var mcpDir in mcpDirs)
            {
                var targetFile = Path.Combine(mcpDir, "Editor/Tools/ConsoleLogFetcher/CustomLogManager.cs");
                if (File.Exists(targetFile))
                {
                    return targetFile;
                }
            }
            
            return null;
        }

        /// <summary>
        /// ファイルが既に修正済みかチェック
        /// </summary>
        /// <param name="filePath">チェック対象のファイルパス</param>
        /// <returns>修正済みの場合true</returns>
        public static bool IsFileAlreadyFixed(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
                
            var content = File.ReadAllText(filePath);
            
            // 修正済みの証拠：リフレクション実装または修正マーカーが存在するか
            return content.Contains(REFLECTION_MARKER) || 
                   content.Contains(FIXED_MARKER) ||
                   content.Contains("// FIXED BY PreCompileMCPFix");
        }

        /// <summary>
        /// 問題のあるコードが存在するかチェック
        /// </summary>
        /// <param name="filePath">チェック対象のファイルパス</param>
        /// <returns>問題コードが存在する場合true</returns>
        public static bool HasProblematicCode(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
                
            var content = File.ReadAllText(filePath);
            return content.Contains(PROBLEMATIC_CODE);
        }


        /// <summary>
        /// MCP Console修正を適用
        /// </summary>
        /// <param name="filePath">修正対象のファイルパス</param>
        /// <returns>修正成功の場合true</returns>
        public static bool ApplyConsoleFix(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var originalContent = content;
                
                // 修正適用
                var fixedContent = ApplyConsoleWindowUtilityFix(content);
                
                if (fixedContent != originalContent)
                {
                    // 修正マーカーを追加
                    fixedContent = "// FIXED BY MCPConsoleFixCore - " + DateTime.Now.ToString() + "\n" + fixedContent;
                    
                    File.WriteAllText(filePath, fixedContent);
                    Debug.Log("[MCPConsoleFixCore] File content updated successfully.");
                    return true;
                }
                else
                {
                    Debug.LogWarning("[MCPConsoleFixCore] No changes made to file content.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCPConsoleFixCore] Error applying fix: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unity再コンパイルをトリガー
        /// </summary>
        public static void TriggerRecompile()
        {
            AssetDatabase.Refresh();
            Debug.Log("[MCPConsoleFixCore] Unity recompile triggered.");
        }

        /// <summary>
        /// ConsoleWindowUtility修正の具体的な実装
        /// </summary>
        /// <param name="content">修正対象のファイル内容</param>
        /// <returns>修正後のファイル内容</returns>
        private static string ApplyConsoleWindowUtilityFix(string content)
        {
            // 既存のMCPConsoleFixToolの修正パターンを使用
            var oldMethod = @"        private void OnConsoleLogsChanged()
        {
            // If the Console is cleared, clear the custom logs as well.
            ConsoleWindowUtility.GetConsoleLogCounts(out int err, out int warn, out int log);
            if (err == 0 && warn == 0 && log == 0)
            {
                ClearLogs();
            }
        }";

            var newMethod = @"        private void OnConsoleLogsChanged()
        {
            // Unity 2023/2024対応: ConsoleWindowUtilityの代替実装
            try
            {
                // リフレクションを使用してConsole情報にアクセス
                var logEntriesType = System.Type.GetType(""UnityEditor.LogEntries,UnityEditor"");
                if (logEntriesType != null)
                {
                    var getCountMethod = logEntriesType.GetMethod(""GetCount"", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (getCountMethod != null)
                    {
                        int totalCount = (int)getCountMethod.Invoke(null, null);
                        if (totalCount == 0)
                        {
                            ClearLogs();
                        }
                        return;
                    }
                }
                
                // フォールバック: カスタムログカウントを使用
                lock (lockObject)
                {
                    if (logEntries.Count == 0)
                    {
                        // 既にクリア済み
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($""[CustomLogManager] Console sync warning: {ex.Message}"");
            }
        }";

            // メソッド全体の置換
            if (content.Contains(oldMethod))
            {
                content = content.Replace(oldMethod, newMethod);
            }
            // より広範囲の修正（PreCompileMCPFixパターン）
            else if (content.Contains("ConsoleWindowUtility.GetConsoleLogCounts"))
            {
                content = content.Replace(
                    "ConsoleWindowUtility.GetConsoleLogCounts(out int err, out int warn, out int log);",
                    @"// Safe implementation - replaced by MCPConsoleFixCore
                    int err = 0, warn = 0, log = 0;
                    try
                    {
                        var logEntriesType = System.Type.GetType(""UnityEditor.LogEntries,UnityEditor"");
                        if (logEntriesType != null)
                        {
                            var getCountMethod = logEntriesType.GetMethod(""GetCount"", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                            if (getCountMethod != null)
                            {
                                int totalCount = (int)getCountMethod.Invoke(null, null);
                                log = totalCount;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($""Console log count error: {ex.Message}"");
                    }"
                );
            }
            
            return content;
        }
    }
}