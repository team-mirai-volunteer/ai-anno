using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using AiTuber.Services.Legacy.Dify;
using AiTuber.Services.Legacy.Dify.Data;
using Cysharp.Threading.Tasks;

namespace AiTuber.Editor.Agent
{
    /// <summary>
    /// Difyサーバー接続テストランナー
    /// Unity Editor内で実際のDifyサーバーとの通信をテスト
    /// </summary>
    public static class DifyServerTestRunner
    {
        private static string DIFY_API_KEY => AiTuber.Editor.Dify.DifyEditorSettings.ApiKey;
        private static string DIFY_API_URL => AiTuber.Editor.Dify.DifyEditorSettings.ApiUrl;
        
        [MenuItem("Agent Tools/Test Dify Server Connection", false, 200)]
        public static async void TestDifyServerConnection()
        {
            Debug.Log("=== Dify Server Connection Test ===");
            
            try
            {
                // 0. 設定確認
                Debug.Log($"[Dify Test] API Key: '{DIFY_API_KEY}'");
                Debug.Log($"[Dify Test] API URL: '{DIFY_API_URL}'");
                
                if (string.IsNullOrEmpty(DIFY_API_KEY))
                {
                    Debug.LogError("[Dify Test] ❌ API Key is not set. Please configure via Window > AiTuber > Dify Editor Tool.");
                    return;
                }
                
                // 1. 設定作成
                var config = new DifyServiceConfig
                {
                    ApiKey = DIFY_API_KEY,
                    ApiUrl = DIFY_API_URL,
                    EnableAudioProcessing = true
                };
                
                var apiClient = new DifyApiClient();
                var difyService = new DifyService(apiClient, config);
                
                // 2. 接続テスト
                Debug.Log("[Dify Test] Testing connection...");
                var isConnected = await difyService.TestConnectionAsync();
                
                if (!isConnected)
                {
                    Debug.LogError("[Dify Test] ❌ Connection failed! Make sure Dify server is running.");
                    Debug.LogError("Run: cd ../dify && docker compose up -d");
                    return;
                }
                
                Debug.Log("[Dify Test] ✅ Connection successful!");
                
                // 3. シンプルなクエリテスト
                Debug.Log("[Dify Test] Sending test query...");
                var result = await difyService.ProcessUserQueryAsync(
                    "こんにちは、今日はいい天気ですね。",
                    "unity-test-user-" + DateTime.Now.Ticks,
                    conversationId: null,
                    cancellationToken: default
                );
                
                if (result.IsSuccess)
                {
                    Debug.Log($"[Dify Test] ✅ Query successful!");
                    Debug.Log($"[Dify Test] Response: {result.TextResponse}");
                    Debug.Log($"[Dify Test] Conversation ID: {result.ConversationId}");
                    Debug.Log($"[Dify Test] Processing time: {result.ProcessingTimeMs}ms");
                    Debug.Log($"[Dify Test] Events received: {result.EventCount}");
                    
                    if (result.HasAudioData)
                    {
                        Debug.Log($"[Dify Test] Audio data: {result.AudioData.Length} bytes");
                    }
                }
                else
                {
                    Debug.LogError($"[Dify Test] ❌ Query failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Dify Test] ❌ Unexpected error: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
            
            Debug.Log("=== Test Complete ===");
        }
        
        [MenuItem("Agent Tools/Test Dify Streaming", false, 201)]
        public static async void TestDifyStreaming()
        {
            Debug.Log("=== Dify Streaming Test ===");
            
            try
            {
                var config = new DifyServiceConfig
                {
                    ApiKey = DIFY_API_KEY,
                    ApiUrl = DIFY_API_URL,
                    EnableAudioProcessing = true
                };
                
                // カスタムイベントハンドラー付きのAPIクライアント
                var apiClient = new DifyApiClient();
                
                Debug.Log("[Dify Streaming] Starting streaming request...");
                
                var request = new DifyApiRequest
                {
                    query = "AIについて3つの重要なポイントを教えてください。",
                    user = "unity-streaming-test",
                    response_mode = "streaming"
                };
                
                int eventCount = 0;
                var textBuilder = new System.Text.StringBuilder();
                
                var result = await apiClient.SendStreamingRequestAsync(
                    request,
                    eventData =>
                    {
                        eventCount++;
                        Debug.Log($"[Dify Streaming] Event #{eventCount}: {eventData.@event}");
                        
                        if (!string.IsNullOrEmpty(eventData.answer))
                        {
                            textBuilder.Append(eventData.answer);
                            Debug.Log($"[Dify Streaming] Text chunk: {eventData.answer}");
                        }
                        
                        if (!string.IsNullOrEmpty(eventData.audio))
                        {
                            Debug.Log($"[Dify Streaming] Audio chunk received: {eventData.audio.Length} chars");
                        }
                    }
                );
                
                Debug.Log($"[Dify Streaming] ✅ Complete! Total events: {eventCount}");
                Debug.Log($"[Dify Streaming] Full response: {textBuilder}");
                Debug.Log($"[Dify Streaming] Processing time: {result.ProcessingTimeMs}ms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Dify Streaming] ❌ Error: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
            
            Debug.Log("=== Streaming Test Complete ===");
        }
        
    }
}