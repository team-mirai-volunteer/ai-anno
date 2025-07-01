#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace AiTuber.Dify
{
    /// <summary>
    /// チャンク対応Difyクライアント
    /// 複数のtext/voiceペアを処理
    /// </summary>
    public class DifyChunkedClient : IDisposable
    {
        private readonly string difyUrl;
        private readonly string difyBaseUrl;
        private readonly string apiKey;
        private readonly bool debugLog;
        private readonly string logPrefix;

        /// <summary>
        /// DifyChunkedClientを作成
        /// </summary>
        /// <param name="url">DifyサーバーURL</param>
        /// <param name="key">APIキー</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        /// <param name="logPrefix">ログプレフィックス</param>
        public DifyChunkedClient(string url, string key, bool enableDebugLog = true, string logPrefix = "[DifyChunkedClient]")
        {
            difyUrl = url ?? throw new ArgumentNullException(nameof(url));
            apiKey = key ?? throw new ArgumentNullException(nameof(key));
            debugLog = enableDebugLog;
            this.logPrefix = logPrefix;

            var uri = new Uri(difyUrl);
            difyBaseUrl = $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}";

            if (debugLog) Debug.Log($"{this.logPrefix} 初期化完了 URL: {difyUrl}");
        }

        /// <summary>
        /// Difyにクエリを送信してチャンクレスポンスを取得
        /// </summary>
        /// <param name="query">クエリテキスト</param>
        /// <param name="user">ユーザー名</param>
        /// <returns>DifyChunkedResponse</returns>
        public async UniTask<DifyChunkedResponse> SendQueryAsync(string query, string user)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (debugLog) Debug.LogWarning($"{logPrefix} 空のクエリが送信されました");
                return DifyChunkedResponse.Empty;
            }

            try
            {
                var requestBody = CreateRequestBody(query, user);
                var jsonData = JsonConvert.SerializeObject(requestBody);
                
                if (debugLog) Debug.Log($"{logPrefix} リクエスト送信: {query}");

                using var request = new UnityWebRequest(difyUrl, "POST");
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{logPrefix} リクエストエラー: {request.error}");
                    return DifyChunkedResponse.Error(request.error);
                }

                var responseText = request.downloadHandler.text;
                if (debugLog) Debug.Log($"{logPrefix} レスポンス受信完了");

                return ParseResponse(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 送信エラー: {ex.Message}");
                return DifyChunkedResponse.Error(ex.Message);
            }
        }

        /// <summary>
        /// リクエストボディ作成
        /// </summary>
        /// <param name="query">クエリテキスト</param>
        /// <param name="user">ユーザー名</param>
        /// <returns>リクエストボディ</returns>
        private object CreateRequestBody(string query, string user)
        {
            return new
            {
                inputs = new Dictionary<string, object>(),
                query = query,
                response_mode = "blocking",
                conversation_id = "",
                user = user,
                files = new List<object>()
            };
        }

        /// <summary>
        /// チャンクレスポンス解析
        /// </summary>
        /// <param name="responseJson">レスポンスJSON</param>
        /// <returns>解析されたレスポンス</returns>
        private DifyChunkedResponse ParseResponse(string responseJson)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DifyApiResponse>(responseJson);
                if (response == null)
                {
                    Debug.LogError($"{logPrefix} レスポンスのデシリアライズに失敗");
                    return DifyChunkedResponse.Error("デシリアライズ失敗");
                }

                var parsedContent = ParseAnswerContent(response.answer ?? "");
                
                // 各チャンクの音声URLとテキストペアを作成
                var chunks = new List<DifyChunk>();
                for (int i = 0; i < parsedContent.TextChunks.Count; i++)
                {
                    var textChunk = parsedContent.TextChunks[i];
                    string? audioUrl = null;
                    
                    if (i < parsedContent.VoiceUrls.Count && !string.IsNullOrEmpty(parsedContent.VoiceUrls[i]))
                    {
                        // 相対URLを絶対URLに変換
                        var voiceUrl = parsedContent.VoiceUrls[i];
                        audioUrl = voiceUrl.StartsWith("http") ? voiceUrl : $"{difyBaseUrl}{voiceUrl}";
                    }

                    chunks.Add(new DifyChunk
                    {
                        Text = textChunk,
                        AudioUrl = audioUrl
                    });
                }

                return new DifyChunkedResponse
                {
                    IsSuccess = true,
                    Chunks = chunks,
                    SiteUrl = parsedContent.SiteUrl,
                    ConversationId = response.conversation_id ?? "",
                    MessageId = response.message_id ?? ""
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} レスポンス解析エラー: {ex.Message}");
                return DifyChunkedResponse.Error(ex.Message);
            }
        }

        /// <summary>
        /// answerコンテンツからチャンクを解析
        /// </summary>
        /// <param name="answer">answerフィールドの内容</param>
        /// <returns>解析結果</returns>
        private ParsedChunkedContent ParseAnswerContent(string answer)
        {
            var result = new ParsedChunkedContent();

            // siteUrl: を抽出
            var siteMatch = Regex.Match(answer, @"siteUrl:(.+?)(?=\n|$)");
            if (siteMatch.Success)
            {
                result.SiteUrl = siteMatch.Groups[1].Value.Trim();
            }

            // text: を全て抽出
            var textMatches = Regex.Matches(answer, @"text:(.+?)(?=\n(?:voice:|text:|$)|$)", RegexOptions.Singleline);
            foreach (Match match in textMatches)
            {
                var text = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    result.TextChunks.Add(text);
                }
            }

            // voice: を全て抽出
            var voiceMatches = Regex.Matches(answer, @"voice:(/files/.*?)(?=\n|$)");
            foreach (Match match in voiceMatches)
            {
                var voicePath = match.Groups[1].Value.Trim();
                var voiceUrl = difyBaseUrl + voicePath;
                result.VoiceUrls.Add(voiceUrl);
            }

            if (debugLog)
            {
                Debug.Log($"{logPrefix} パース結果: テキストチャンク数={result.TextChunks.Count}, 音声URL数={result.VoiceUrls.Count}");
            }

            return result;
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
        /// チャンクコンテンツ解析結果
        /// </summary>
        private class ParsedChunkedContent
        {
            public string? SiteUrl { get; set; }
            public List<string> TextChunks { get; set; } = new List<string>();
            public List<string> VoiceUrls { get; set; } = new List<string>();
        }
    }

    /// <summary>
    /// Difyチャンクレスポンス
    /// </summary>
    public class DifyChunkedResponse
    {
        public bool IsSuccess { get; set; }
        public List<DifyChunk> Chunks { get; set; } = new List<DifyChunk>();
        public string? SiteUrl { get; set; }
        public string ConversationId { get; set; } = "";
        public string MessageId { get; set; } = "";
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// チャンク数
        /// </summary>
        public int ChunkCount => Chunks.Count;

        /// <summary>
        /// 空のレスポンス
        /// </summary>
        public static DifyChunkedResponse Empty => new DifyChunkedResponse { IsSuccess = false };

        /// <summary>
        /// エラーレスポンス
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>エラーレスポンス</returns>
        public static DifyChunkedResponse Error(string errorMessage) => new DifyChunkedResponse 
        { 
            IsSuccess = false, 
            ErrorMessage = errorMessage 
        };
    }

    /// <summary>
    /// Difyチャンク（テキスト+音声URLのペア）
    /// </summary>
    public class DifyChunk
    {
        public string Text { get; set; } = "";
        public string? AudioUrl { get; set; }

        /// <summary>
        /// 音声URLを持っているかどうか
        /// </summary>
        public bool HasAudioUrl => !string.IsNullOrEmpty(AudioUrl);
    }
}