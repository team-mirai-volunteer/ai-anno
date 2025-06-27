using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using System;

namespace Assets.Editor.Agent.TestRunner
{
    /// <summary>
    /// Agent専用テスト実行ツール
    /// MCP経由でのタイムアウト問題回避用
    /// Unity Test Runner完全バイパスでリフレクション直接実行
    /// </summary>
    public static class AgentTestRunner
    {


        [MenuItem("Agent Tools/Run All Tests", false, 102)]
        public static void RunAllTests()
        {
            Debug.Log("[AgentTestRunner] Running ALL tests (MCP Timeout Bypass)...");
            
            try
            {
                var testAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Tests");
                
                if (testAssembly == null)
                {
                    Debug.LogError("[AgentTestRunner] Tests assembly not found");
                    return;
                }

                var testTypes = testAssembly.GetTypes()
                    .Where(t => t.Name.EndsWith("Tests") && 
                               !t.Name.Contains("Integration"))
                    .ToArray();

                foreach (var testType in testTypes)
                {
                    Debug.Log($"[AgentTestRunner] Running test class: {testType.Name}");
                    RunTestClass(testType, testType.Name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AgentTestRunner] Error running all tests: {ex.Message}");
            }
        }

        private static void RunTestClass(Type testType, string className)
        {
            Debug.Log($"[AgentTestRunner] Running {className}...");
            
            int passed = 0;
            int failed = 0;
            int total = 0;

            var testMethods = testType.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                .ToArray();

            Debug.Log($"[AgentTestRunner] Found {testMethods.Length} test methods in {className}");

            foreach (var method in testMethods)
            {
                total++;
                try
                {
                    var instance = Activator.CreateInstance(testType);
                    
                    // SetUpメソッドがあれば実行
                    var setupMethod = testType.GetMethods()
                        .FirstOrDefault(m => m.GetCustomAttributes(typeof(SetUpAttribute), false).Length > 0);
                    setupMethod?.Invoke(instance, null);

                    // テストメソッド実行
                    Debug.Log($"[AgentTestRunner] Running: {method.Name}");
                    method.Invoke(instance, null);
                    
                    Debug.Log($"[AgentTestRunner] ✅ PASSED: {method.Name}");
                    passed++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AgentTestRunner] ❌ FAILED: {method.Name}");
                    Debug.LogError($"  Error: {ex.InnerException?.Message ?? ex.Message}");
                    failed++;
                }
            }

            Debug.Log($"[AgentTestRunner] {className} Results: {passed} passed, {failed} failed, {total} total");
        }

        private static void TestSSEParserDirectly()
        {
            Debug.Log("[AgentTestRunner] Testing SSEParser.ParseSingleLine directly...");

            // 直接SSEParserをテスト
            var testData = "data: {\"event\":\"message\",\"answer\":\"Hello World\"}";
            
            try
            {
                var result = AiTuber.Services.Legacy.Dify.Infrastructure.SSEParser.ParseSingleLine(testData);
                
                if (result != null && result.IsValid && result.Event != null)
                {
                    Debug.Log($"[AgentTestRunner] ✅ Direct test PASSED!");
                    Debug.Log($"  Event type: {result.Event.@event}");
                    Debug.Log($"  Answer: {result.Event.answer}");
                }
                else
                {
                    Debug.LogError($"[AgentTestRunner] ❌ Direct test FAILED!");
                    Debug.LogError($"  Result valid: {result?.IsValid}");
                    Debug.LogError($"  Event null: {result?.Event == null}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AgentTestRunner] ❌ Direct test ERROR: {ex.Message}");
                Debug.LogError($"  Stack trace: {ex.StackTrace}");
            }
        }



        private static void RunTestByClassName(string className)
        {
            try
            {
                var testAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Tests");
                
                if (testAssembly == null)
                {
                    Debug.LogError($"[AgentTestRunner] Tests assembly not found for {className}");
                    return;
                }

                var testType = testAssembly.GetType(className);
                if (testType == null)
                {
                    Debug.LogError($"[AgentTestRunner] Test class not found: {className}");
                    return;
                }

                RunTestClass(testType, testType.Name);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AgentTestRunner] Error running {className}: {ex.Message}");
            }
        }
    }
}