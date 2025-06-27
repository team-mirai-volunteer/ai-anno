using UnityEngine;
using UnityEditor;
using AiTuber.Services.Legacy.Dify;
using AiTuber.Services.Legacy.Dify.Data;
using Cysharp.Threading.Tasks;

namespace AiTuber.Editor.Agent
{
    public static class SimpleDifyTest
    {
        [MenuItem("Agent Tools/Simple Dify Test", false, 220)]
        public static async void RunSimpleDifyTest()
        {
            Debug.Log("=== Simple Dify Test Start ===");
            
            var config = new DifyServiceConfig
            {
                ApiKey = AiTuber.Editor.Dify.DifyEditorSettings.ApiKey,
                ApiUrl = AiTuber.Editor.Dify.DifyEditorSettings.ApiUrl,
                EnableAudioProcessing = false
            };
            
            var apiClient = new DifyApiClient();
            var service = new DifyService(apiClient, config);
            
            try
            {
                Debug.Log("Sending request...");
                var result = await service.ProcessUserQueryAsync(
                    "こんにちは",
                    "test-user-" + System.DateTime.Now.Ticks,
                    conversationId: null,
                    cancellationToken: default
                );
                
                Debug.Log($"Success: {result.IsSuccess}");
                Debug.Log($"Text Response: '{result.TextResponse}'");
                Debug.Log($"Text Length: {result.TextResponse?.Length ?? 0}");
                Debug.Log($"Has Text: {result.HasTextResponse}");
                Debug.Log($"Event Count: {result.EventCount}");
                Debug.Log($"Conversation ID: {result.ConversationId}");
                Debug.Log($"Message ID: {result.MessageId}");
                
                if (!result.IsSuccess)
                {
                    Debug.LogError($"Error: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
            
            Debug.Log("=== Test Complete ===");
        }
        
    }
}