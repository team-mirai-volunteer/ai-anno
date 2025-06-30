#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace AiTuber.Dify
{
    /// <summary>
    /// シンプルなDifyクライアント
    /// わんコメ統合用 - 音声ダウンロード対応
    /// </summary>
    public class DifyClient : IDisposable
    {
        private readonly string difyUrl;
        private readonly string difyBaseUrl;
        private readonly string apiKey;
        private readonly bool debugLog;
        private readonly string logPrefix;

        /// <summary>
        /// DifyClientを作成
        /// </summary>
        /// <param name="url">DifyサーバーURL (例: http://localhost/v1/chat-messages)</param>
        /// <param name="key">APIキー</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        /// <param name="logPrefix">ログプレフィックス</param>
        public DifyClient(string url, string key, bool enableDebugLog = true, string logPrefix = "[DifyClient]")
        {
            difyUrl = url ?? throw new ArgumentNullException(nameof(url));
            apiKey = key ?? throw new ArgumentNullException(nameof(key));
            debugLog = enableDebugLog;
            this.logPrefix = logPrefix;

            // ベースURL抽出 (例: http://localhost/v1/chat-messages → http://localhost)
            var uri = new Uri(difyUrl);
            difyBaseUrl = $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}";

            if (debugLog) Debug.Log($"{this.logPrefix} 初期化完了 URL: {difyUrl}");
        }

        /// <summary>
        /// Difyにクエリを送信してレスポンスを取得
        /// </summary>
        /// <param name="query">クエリテキスト</param>
        /// <param name="user">ユーザー名</param>
        /// <returns>DifyBlockingResponse</returns>
        public async UniTask<DifyBlockingResponse> SendQueryAsync(string query, string user)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (debugLog) Debug.LogWarning($"{logPrefix} 空のクエリが送信されました");
                return DifyBlockingResponse.Empty;
            }

            try
            {
                // リクエストボディ作成
                var requestBody = CreateRequestBody(query, user);
                var jsonData = JsonConvert.SerializeObject(requestBody);
                
                if (debugLog) Debug.Log($"{logPrefix} リクエスト送信: {query}");

                // UnityWebRequest作成
                using var request = new UnityWebRequest(difyUrl, "POST");
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                // リクエスト送信
                await request.SendWebRequest();

                // レスポンス処理
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{logPrefix} リクエストエラー: {request.error}");
                    return DifyBlockingResponse.Error(request.error);
                }

                var responseText = request.downloadHandler.text;
                
                if (debugLog) Debug.Log($"{logPrefix} レスポンス受信完了");

                // レスポンス解析
                return await ParseResponse(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 送信エラー: {ex.Message}");
                return DifyBlockingResponse.Error(ex.Message);
            }
        }

        /// <summary>
        /// Difyリクエストボディを作成
        /// </summary>
        /// <param name="query">クエリテキスト</param>
        /// <param name="user">ユーザー名</param>
        /// <returns>リクエストボディオブジェクト</returns>
        private object CreateRequestBody(string query, string user)
        {
            return new
            {
                inputs = new { },
                query = query,
                response_mode = "blocking",
                conversation_id = "",
                user = user,
                files = new object[] { }
            };
        }

        /// <summary>
        /// Difyレスポンスを解析
        /// </summary>
        /// <param name="responseJson">レスポンスJSON</param>
        /// <returns>解析されたレスポンス</returns>
        private async UniTask<DifyBlockingResponse> ParseResponse(string responseJson)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DifyApiResponse>(responseJson);
                if (response == null)
                {
                    Debug.LogError($"{logPrefix} レスポンスのデシリアライズに失敗");
                    return DifyBlockingResponse.Error("デシリアライズ失敗");
                }

                // answerからテキストと音声URLを抽出
                var parsedContent = ParseAnswerContent(response.answer ?? "");
                
                byte[]? audioData = null;
                if (!string.IsNullOrEmpty(parsedContent.VoiceUrl))
                {
                    // 音声ファイルをダウンロード
                    audioData = await DownloadAudioFile(parsedContent.VoiceUrl);
                }

                return new DifyBlockingResponse
                {
                    IsSuccess = true,
                    TextResponse = parsedContent.Text,
                    AudioData = audioData,
                    ConversationId = response.conversation_id ?? "",
                    MessageId = response.message_id ?? "",
                    SlideUrl = parsedContent.SlideUrl
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} レスポンス解析エラー: {ex.Message}");
                return DifyBlockingResponse.Error(ex.Message);
            }
        }

        /// <summary>
        /// answerコンテンツを解析
        /// </summary>
        /// <param name="answer">answerフィールドの内容</param>
        /// <returns>解析結果</returns>
        private ParsedContent ParseAnswerContent(string answer)
        {
            var result = new ParsedContent();

            // text: で始まる部分を抽出
            var textMatch = Regex.Match(answer, @"text:(.*?)(?=\n(?:slideUrl:|voice:)|$)", RegexOptions.Singleline);
            if (textMatch.Success)
            {
                result.Text = textMatch.Groups[1].Value.Trim();
            }

            // slideUrl: の部分を抽出
            var slideMatch = Regex.Match(answer, @"slideUrl:(.+?)(?=\n|$)");
            if (slideMatch.Success)
            {
                result.SlideUrl = slideMatch.Groups[1].Value.Trim();
            }

            // voice: の部分を抽出 - voice:[filename](path)
            var voiceMatch = Regex.Match(answer, @"voice:\[.*?\]\((/files/.*?)\)");
            if (voiceMatch.Success)
            {
                var voicePath = voiceMatch.Groups[1].Value;
                result.VoiceUrl = difyBaseUrl + voicePath;
            }

            return result;
        }

        /// <summary>
        /// 音声ファイルをダウンロード
        /// </summary>
        /// <param name="voiceUrl">音声ファイルURL</param>
        /// <returns>音声データ</returns>
        private async UniTask<byte[]?> DownloadAudioFile(string voiceUrl)
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード開始: {voiceUrl}");

                using var request = UnityWebRequest.Get(voiceUrl);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{logPrefix} 音声ダウンロードエラー: {request.error}");
                    return null;
                }

                var audioData = request.downloadHandler.data;
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード完了: {audioData.Length} bytes");

                return audioData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 音声ダウンロード例外: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        public void Dispose()
        {
            if (debugLog) Debug.Log($"{logPrefix} リソース解放");
        }

        /// <summary>
        /// Dify APIレスポンス用データクラス
        /// </summary>
        [System.Serializable]
        private class DifyApiResponse
        {
            public string? answer;
            public string? conversation_id;
            public string? message_id;
            public string? @event;
            public string? task_id;
            public string? id;
            public string? mode;
        }

        /// <summary>
        /// answerコンテンツ解析結果
        /// </summary>
        private class ParsedContent
        {
            public string Text { get; set; } = "";
            public string? SlideUrl { get; set; }
            public string? VoiceUrl { get; set; }
        }
    }

    /// <summary>
    /// Difyブロッキングレスポンス
    /// </summary>
    public class DifyBlockingResponse
    {
        public bool IsSuccess { get; set; }
        public string TextResponse { get; set; } = "";
        public byte[]? AudioData { get; set; }
        public string ConversationId { get; set; } = "";
        public string MessageId { get; set; } = "";
        public string? SlideUrl { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 音声データを持っているかどうか
        /// </summary>
        public bool HasAudioData => AudioData != null && AudioData.Length > 0;

        /// <summary>
        /// 空のレスポンス
        /// </summary>
        public static DifyBlockingResponse Empty => new DifyBlockingResponse { IsSuccess = false };

        /// <summary>
        /// エラーレスポンス
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>エラーレスポンス</returns>
        public static DifyBlockingResponse Error(string errorMessage) => new DifyBlockingResponse 
        { 
            IsSuccess = false, 
            ErrorMessage = errorMessage 
        };
    }
}