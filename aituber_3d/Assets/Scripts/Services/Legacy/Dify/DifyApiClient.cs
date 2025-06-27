using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Legacy.Dify.Data;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using AiTuber.Services.Legacy.Dify.Infrastructure;
using Newtonsoft.Json;

namespace AiTuber.Services.Legacy.Dify
{
    /// <summary>
    /// Dify Chat Messages API クライアント実装
    /// UnityWebRequest使用、Unity Test Framework対応でユニットテスト可能
    /// </summary>
    public class DifyApiClient : IDifyApiClient
    {
        /// <summary>
        /// API キー
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Dify API エンドポイント URL
        /// </summary>
        public string ApiUrl { get; set; }


        /// <summary>
        /// Dify Chat Messages API にストリーミングリクエストを送信
        /// Server-Sent Events (SSE) でレスポンスを受信し、イベントごとにコールバック実行
        /// </summary>
        /// <param name="request">リクエストデータ</param>
        /// <param name="onEventReceived">SSEイベント受信時のコールバック</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>処理結果（会話ID、メッセージID、エラー情報等）</returns>
        public async Task<DifyProcessingResult> SendStreamingRequestAsync(
            DifyApiRequest request,
            Action<DifyStreamEvent> onEventReceived,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!request.IsValid())
                throw new ArgumentException("Request validation failed", nameof(request));

            if (!IsConfigurationValid())
                throw new InvalidOperationException("API configuration is invalid");

            var result = new DifyProcessingResult
            {
                TotalEventCount = 0
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ストリーミングモードに強制設定
                request.response_mode = "streaming";
                
                using var webRequest = CreateUnityWebRequest(request);
                var operation = webRequest.SendWebRequest();

                // 真のリアルタイム処理：データを逐次監視
                var lastProcessedLength = 0;
                var textBuilder = new StringBuilder();
                
                while (!operation.isDone && !cancellationToken.IsCancellationRequested)
                {
                    var currentData = webRequest.downloadHandler.text ?? "";
                    
                    // 新しいデータがある場合は即座に処理
                    if (currentData.Length > lastProcessedLength)
                    {
                        var newData = currentData.Substring(lastProcessedLength);
                        lastProcessedLength = currentData.Length;
                        
                        // 新しいデータをリアルタイム処理
                        await ProcessPartialStreamData(newData, onEventReceived, result, textBuilder, cancellationToken);
                    }
                    
                    // 非ブロッキング待機（1フレーム）
                    await UniTask.Yield();
                }

                // リクエスト完了チェック
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"HTTP error: {webRequest.error}";
                    return result;
                }

                // 最後の残りデータ処理
                var finalData = webRequest.downloadHandler.text ?? "";
                if (finalData.Length > lastProcessedLength)
                {
                    var remainingData = finalData.Substring(lastProcessedLength);
                    await ProcessPartialStreamData(remainingData, onEventReceived, result, textBuilder, cancellationToken);
                }

                result.TextResponse = textBuilder.ToString();
                result.IsSuccess = true;
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Request cancelled";
                throw;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }


        /// <summary>
        /// API設定の妥当性チェック
        /// </summary>
        /// <returns>設定が有効であれば true、無効であれば false</returns>
        public bool IsConfigurationValid()
        {
            return !string.IsNullOrWhiteSpace(ApiKey) &&
                   !string.IsNullOrWhiteSpace(ApiUrl) &&
                   IsValidUrl(ApiUrl);
        }

        /// <summary>
        /// URL形式の妥当性チェック
        /// </summary>
        /// <param name="url">チェック対象のURL</param>
        /// <returns>有効なURL形式であればtrue</returns>
        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == System.Uri.UriSchemeHttp || uriResult.Scheme == System.Uri.UriSchemeHttps);
        }

        /// <summary>
        /// API接続テスト（軽量なヘルスチェック用）
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>接続成功であれば true、失敗であれば false</returns>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigurationValid())
                return false;

            try
            {
                // 軽量なヘルスチェック用リクエスト
                var testRequest = new DifyApiRequest
                {
                    query = "ping",
                    user = "health-check",
                    response_mode = "blocking"
                };

                using var webRequest = CreateUnityWebRequest(testRequest);
                
                var operation = webRequest.SendWebRequest();
                await operation.ToUniTask(cancellationToken: cancellationToken);

                return webRequest.result == UnityWebRequest.Result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// UnityWebRequest作成
        /// </summary>
        /// <param name="request">リクエストデータ</param>
        /// <returns>UnityWebRequest</returns>
        private UnityWebRequest CreateUnityWebRequest(DifyApiRequest request)
        {
            // JSON シリアライズ（JsonUtilityはDictionaryをサポートしないため手動で構築）
            var jsonContent = CreateJsonString(request);
            var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);

            var webRequest = new UnityWebRequest(ApiUrl, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // ヘッダー設定
            webRequest.SetRequestHeader("Authorization", $"Bearer {ApiKey}");
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            if (request.response_mode == "streaming")
            {
                webRequest.SetRequestHeader("Accept", "text/event-stream");
            }

            // タイムアウト設定（固定30秒）
            webRequest.timeout = 30;

            return webRequest;
        }

        /// <summary>
        /// Server-Sent Events ストリーム処理
        /// </summary>
        /// <param name="responseText">レスポンステキスト</param>
        /// <param name="onEventReceived">イベント受信コールバック</param>
        /// <param name="result">処理結果（更新対象）</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <summary>
        /// DifyApiRequestをJSON文字列に変換（JsonUtility使用）
        /// 型安全なシリアライゼーション
        /// </summary>
        /// <param name="request">リクエストデータ</param>
        /// <returns>JSON文字列</returns>
        private string CreateJsonString(DifyApiRequest request)
        {
            var jsonRequest = new DifyJsonRequest
            {
                inputs = new object(),
                query = request.query,
                response_mode = request.response_mode,
                user = request.user,
                files = new object[0]
            };
            
            // conversation_idは空でない場合のみ設定
            if (!string.IsNullOrEmpty(request.conversation_id))
            {
                jsonRequest.conversation_id = request.conversation_id;
            }
            
            return JsonConvert.SerializeObject(jsonRequest);
        }
        


        /// <summary>
        /// 部分的なストリームデータをリアルタイム処理
        /// </summary>
        /// <param name="newData">新着データ</param>
        /// <param name="onEventReceived">イベント受信コールバック</param>
        /// <param name="result">処理結果</param>
        /// <param name="textBuilder">テキスト蓄積用</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask ProcessPartialStreamData(
            string newData,
            Action<DifyStreamEvent> onEventReceived,
            DifyProcessingResult result,
            StringBuilder textBuilder,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(newData))
                return;

            var lines = newData.Split('\n');
            
            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE形式のdata行を処理
                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring(6).Trim();

                    if (jsonData == "[DONE]")
                    {
                        break;
                    }

                    try
                    {
                        // SSEParserでパース
                        var parseResult = SSEParser.ParseSingleLine(line);
                        
                        if (parseResult != null && parseResult.IsValid && parseResult.Event != null)
                        {
                            var streamEvent = parseResult.Event;
                            result.TotalEventCount++;

                            // リアルタイムログ

                            // コールバック実行
                            onEventReceived?.Invoke(streamEvent);

                            // テキスト蓄積
                            if (streamEvent.HasValidTextMessage)
                            {
                                textBuilder.Append(streamEvent.answer);
                            }

                            // 結果更新
                            if (!string.IsNullOrEmpty(streamEvent.conversation_id))
                                result.ConversationId = streamEvent.conversation_id;
                            if (!string.IsNullOrEmpty(streamEvent.message_id))
                                result.MessageId = streamEvent.message_id;
                        }
                    }
                    catch (Exception ex)
                    {
                        // パース失敗は無視（リアルタイム処理のためログのみ）
                        Debug.LogWarning($"[DifyApiClient] SSE parsing failed for line: {line}. Error: {ex.Message}");
                    }
                }

                // リアルタイム感を保つためのyield
                await UniTask.Yield();
            }
        }

        private async Task ProcessServerSentEventsAsync(
            string responseText,
            Action<DifyStreamEvent> onEventReceived,
            DifyProcessingResult result,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                return;
            }

            var textBuilder = new StringBuilder();
            
            // SSEParserを使用してイベントを解析
            try
            {
                var events = SSEParser.ParseEvents(responseText);
                foreach (var streamEvent in events)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    result.TotalEventCount++;

                    // デバッグログ（最初の10イベントのみ）
                    if (result.TotalEventCount <= 10)
                    {
                        Debug.Log($"[DifyApiClient] 🎯 Event #{result.TotalEventCount}: event='{streamEvent.@event}', answer='{streamEvent.answer}', HasValidTextMessage={streamEvent.HasValidTextMessage}");
                    }

                    // コールバック実行
                    onEventReceived?.Invoke(streamEvent);

                    // 結果に反映
                    ProcessStreamEvent(streamEvent, result, textBuilder);

                    // 非同期処理のためのyield（UIスレッドブロック防止）
                    await UniTask.Yield();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyApiClient] SSE parsing failed: {ex.Message}");
                // パース失敗時はイベント数だけ設定
                result.TotalEventCount = 1;
            }

            result.TextResponse = textBuilder.ToString();
            Debug.Log($"[DifyApiClient] 📊 SSE processing complete. Events: {result.TotalEventCount}, Text chars: {result.TextResponse?.Length ?? 0}");
        }

        /// <summary>
        /// ストリームイベント処理
        /// </summary>
        /// <param name="streamEvent">ストリームイベント</param>
        /// <param name="result">処理結果（更新対象）</param>
        /// <param name="textBuilder">テキスト蓄積用</param>
        private void ProcessStreamEvent(
            DifyStreamEvent streamEvent,
            DifyProcessingResult result,
            StringBuilder textBuilder)
        {
            // メタデータ更新
            if (!string.IsNullOrEmpty(streamEvent.conversation_id))
                result.ConversationId = streamEvent.conversation_id;

            if (!string.IsNullOrEmpty(streamEvent.message_id))
                result.MessageId = streamEvent.message_id;

            // テキストメッセージ処理
            if (streamEvent.HasValidTextMessage)
            {
                textBuilder.Append(streamEvent.answer);
                Debug.Log($"[DifyApiClient] Appended text: '{streamEvent.answer}' (Total: {textBuilder.Length} chars)");
            }
            else if (streamEvent.@event == "message")
            {
                Debug.LogWarning($"[DifyApiClient] Message event with empty/null answer. Event: {streamEvent.@event}, Answer: '{streamEvent.answer}', HasValidTextMessage: {streamEvent.HasValidTextMessage}");
            }
            
            // 全イベントをデバッグ出力（最初の10個のみ）
            if (result.TotalEventCount <= 10)
            {
                Debug.Log($"[DifyApiClient] Event #{result.TotalEventCount}: event='{streamEvent.@event}', answer='{streamEvent.answer}', HasValidTextMessage={streamEvent.HasValidTextMessage}");
            }

            // 音声データ処理
            if (streamEvent.HasValidAudioData)
            {
                try
                {
                    var audioBytes = Convert.FromBase64String(streamEvent.audio);
                    result.AudioChunks.Add(audioBytes);
                }
                catch (FormatException)
                {
                    // Base64デコードエラーは無視（ログのみ）
                }
            }
        }
    }

    
    /// <summary>
    /// Dify API JSON リクエスト用データクラス
    /// Newtonsoft.Json シリアライゼーション用
    /// </summary>
    internal class DifyJsonRequest
    {
        [JsonProperty("inputs")]
        public object inputs { get; set; }
        
        [JsonProperty("query")]
        public string query { get; set; }
        
        [JsonProperty("response_mode")]
        public string response_mode { get; set; }
        
        [JsonProperty("user")]
        public string user { get; set; }
        
        [JsonProperty("conversation_id", NullValueHandling = NullValueHandling.Ignore)]
        public string conversation_id { get; set; }
        
        [JsonProperty("files")]
        public object[] files { get; set; }
    }
}