using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

#nullable enable

namespace AiTuber.Services.Dify.Infrastructure.Http
{
    /// <summary>
    /// UnityWebRequestベースのHTTPクライアント実装
    /// Infrastructure層 Clean Architecture準拠
    /// Legacy DifyApiClient.cs SSE実装パターン踏襲
    /// リアルタイムストリーミング対応
    /// </summary>
    public class UnityWebRequestHttpClient : IHttpClient
    {
        private readonly DifyConfiguration _configuration;

        /// <summary>
        /// UnityWebRequestHttpClientを作成
        /// </summary>
        /// <param name="configuration">Dify設定</param>
        /// <exception cref="ArgumentNullException">設定がnullの場合</exception>
        /// <exception cref="ArgumentException">無効な設定の場合</exception>
        public UnityWebRequestHttpClient(DifyConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            if (!_configuration.IsValid())
                throw new ArgumentException("Invalid configuration provided", nameof(configuration));
        }

        /// <summary>
        /// ストリーミングリクエストを送信
        /// Legacy DifyApiClient.cs のSSE実装パターン踏襲
        /// lastProcessedLength による差分処理でリアルタイム配信
        /// </summary>
        /// <param name="request">HTTPリクエスト</param>
        /// <param name="onDataReceived">データ受信時のコールバック（Push型）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>HTTPレスポンス</returns>
        /// <exception cref="ArgumentNullException">リクエストがnullの場合</exception>
        public async Task<HttpResponse> SendStreamingRequestAsync(
            HttpRequest request, 
            Action<string>? onDataReceived, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // キャンセル即座確認
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var unityRequest = CreateUnityWebRequest(request);
                var operation = unityRequest.SendWebRequest();

                // Legacy実装パターン踏襲: lastProcessedLength による差分処理
                var lastProcessedLength = 0;
                var responseBuilder = new StringBuilder();

                // リアルタイムストリーミング処理ループ
                while (!operation.isDone && !cancellationToken.IsCancellationRequested)
                {
                    var currentData = unityRequest.downloadHandler.text ?? "";
                    
                    // 新着データ検出時の即座処理
                    if (currentData.Length > lastProcessedLength)
                    {
                        var newData = currentData.Substring(lastProcessedLength);
                        lastProcessedLength = currentData.Length;
                        
                        // 新着データを即座にコールバック
                        ProcessNewStreamData(newData, onDataReceived);
                        responseBuilder.Append(newData);
                    }
                    
                    // 非ブロッキング待機（Unity 1フレーム）
                    await UniTask.Yield();
                }

                // キャンセル確認
                cancellationToken.ThrowIfCancellationRequested();

                // 最終データ処理（リクエスト完了後の残りデータ）
                var finalData = unityRequest.downloadHandler.text ?? "";
                if (finalData.Length > lastProcessedLength)
                {
                    var remainingData = finalData.Substring(lastProcessedLength);
                    ProcessNewStreamData(remainingData, onDataReceived);
                    responseBuilder.Append(remainingData);
                }

                // レスポンス結果判定
                var isSuccess = unityRequest.result == UnityWebRequest.Result.Success;
                var errorMessage = isSuccess ? "" : unityRequest.error ?? "Unknown error";
                
                return new HttpResponse(
                    isSuccess,
                    errorMessage,
                    responseBuilder.ToString());
            }
            catch (OperationCanceledException)
            {
                throw; // キャンセル例外は再スロー
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnityWebRequest] Exception occurred: {ex.Message}");
                return new HttpResponse(
                    false,
                    $"Streaming request failed: {ex.Message}",
                    "");
            }
        }

        /// <summary>
        /// 接続テスト
        /// 軽量なヘルスチェック用
        /// </summary>
        /// <param name="url">テスト対象URL</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>接続成功フラグ</returns>
        /// <exception cref="ArgumentNullException">URLがnullの場合</exception>
        public async Task<bool> TestConnectionAsync(
            string url, 
            CancellationToken cancellationToken = default)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            try
            {
                // 軽量なGETリクエストで接続確認
                using var unityRequest = new UnityWebRequest(url, "GET");
                unityRequest.downloadHandler = new DownloadHandlerBuffer();
                unityRequest.timeout = 5; // 5秒タイムアウト

                var operation = unityRequest.SendWebRequest();
                await operation.ToUniTask(cancellationToken: cancellationToken);

                // 成功もしくは認証エラー（サーバー応答あり）なら接続OK
                return unityRequest.result == UnityWebRequest.Result.Success ||
                       unityRequest.responseCode == 401; // Unauthorized = server is reachable
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// UnityWebRequestを作成
        /// Legacy DifyApiClient.cs パターン踏襲
        /// </summary>
        /// <param name="request">HTTPリクエスト</param>
        /// <returns>UnityWebRequest</returns>
        private UnityWebRequest CreateUnityWebRequest(HttpRequest request)
        {
            var webRequest = new UnityWebRequest(request.Url, request.Method);
            
            // リクエストボディ設定
            if (!string.IsNullOrEmpty(request.Body))
            {
                var bodyBytes = Encoding.UTF8.GetBytes(request.Body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            }
            
            // ダウンロードハンドラー設定（ストリーミング対応）
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            
            // HTTPヘッダー設定
            foreach (var header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }
            
            // SSEストリーミング用タイムアウト設定
            webRequest.timeout = 60; // 60秒（長時間ストリーミング対応）
            
            return webRequest;
        }

        /// <summary>
        /// 新着ストリーミングデータを処理
        /// SSE形式の解析とコールバック実行
        /// Legacy実装パターン踏襲
        /// </summary>
        /// <param name="newData">新着データ</param>
        /// <param name="onDataReceived">データ受信コールバック</param>
        private void ProcessNewStreamData(string newData, Action<string>? onDataReceived)
        {
            if (string.IsNullOrWhiteSpace(newData) || onDataReceived == null)
                return;

            // 行単位でSSE処理
            var lines = newData.Split('\n');
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                    
                // SSEフォーマット準拠: "data: " プリフィックス確認
                if (line.StartsWith("data: "))
                {
                    // SSE形式でコールバック実行（改行付加でSSE準拠）
                    onDataReceived(line + "\n\n");
                }
                else if (line.Trim() == "data: [DONE]")
                {
                    // Dify SSE終了マーカー
                    onDataReceived(line + "\n\n");
                }
            }
        }
    }
}