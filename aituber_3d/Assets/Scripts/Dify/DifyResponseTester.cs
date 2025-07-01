#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

namespace AiTuber.Dify
{
    public class DifyRequestData
    {
        public Dictionary<string, object> inputs = new Dictionary<string, object>();
        public string query = "";
        public string response_mode = "blocking";
        public string conversation_id = "";
        public string user = "";
        public List<object> files = new List<object>();
    }

    /// <summary>
    /// Difyレスポンステスト用スクリプト
    /// </summary>
    public class DifyResponseTester : MonoBehaviour
    {
        [Header("Dify Settings")]
        [SerializeField] private string difyUrl = "";
        [SerializeField] private string apiKey = "";
        
        [Header("Test Message")]
        [SerializeField] private string testMessage = "こんにちは";
        [SerializeField] private string userName = "テストユーザー";
        
        [Header("Output Settings")]
        [SerializeField] private string outputFilePath = "dify_response.json";
        
        [Header("Audio Playback")]
        [SerializeField] private BufferedAudioPlayer? bufferedAudioPlayer;
        
        [Header("Subtitle Text")]
        [SerializeField] private Text? subtitleText;

        /// <summary>
        /// Difyレスポンステスト実行
        /// </summary>
        [ContextMenu("Test Dify Response")]
        public async void TestDifyResponse()
        {
            if (string.IsNullOrWhiteSpace(difyUrl))
            {
                Debug.LogError("[DifyResponseTester] Dify URLが設定されていません");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogError("[DifyResponseTester] API Keyが設定されていません");
                return;
            }

            testStartTime = Time.realtimeSinceStartup;
            Debug.Log($"<color=cyan>[DifyResponseTester] 🚀 Difyテスト開始: {testMessage} - 開始時刻: {testStartTime:F3}秒</color>");
            await SendDifyRequest();
        }

        private float testStartTime;

        /// <summary>
        /// Difyリクエスト送信（ブロッキング）
        /// </summary>
        private async UniTask SendDifyRequest()
        {
            try
            {
                var requestData = new DifyRequestData
                {
                    query = testMessage,
                    user = userName
                };

                var jsonData = JsonConvert.SerializeObject(requestData);
                Debug.Log($"[DifyResponseTester] 送信JSON: {jsonData}");
                var bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

                using (var request = new UnityWebRequest(difyUrl, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    Debug.Log("[DifyResponseTester] リクエスト送信中...");
                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;
                        var responseTime = Time.realtimeSinceStartup - testStartTime;
                        Debug.Log($"<color=yellow>[DifyResponseTester] 📥 レスポンス取得成功: {responseText.Length} 文字 - API応答時間: {responseTime:F3}秒</color>");
                        
                        SaveResponseToFile(responseText);
                        
                        // 音声URLを抽出してバッファリング再生
                        await ProcessAudioPlayback(responseText);
                    }
                    else
                    {
                        var errorMessage = $"リクエスト失敗: {request.error} (Code: {request.responseCode})";
                        Debug.LogError($"[DifyResponseTester] {errorMessage}");
                        
                        var errorResponse = request.downloadHandler?.text ?? "レスポンスなし";
                        SaveResponseToFile($"{{\"error\": \"{errorMessage}\", \"response\": \"{errorResponse}\"}}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyResponseTester] 例外発生: {ex.Message}");
                SaveResponseToFile($"{{\"exception\": \"{ex.Message}\"}}");
            }
        }

        /// <summary>
        /// レスポンスをファイルに保存
        /// </summary>
        /// <param name="responseText">レスポンステキスト</param>
        private void SaveResponseToFile(string responseText)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{timestamp}_{outputFilePath}";
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var filePath = Path.Combine(desktopPath, fileName);
                
                File.WriteAllText(filePath, responseText, System.Text.Encoding.UTF8);
                Debug.Log($"[DifyResponseTester] レスポンス保存完了: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyResponseTester] ファイル保存失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 音声再生処理
        /// </summary>
        /// <param name="responseText">Difyレスポンステキスト</param>
        private async UniTask ProcessAudioPlayback(string responseText)
        {
            if (bufferedAudioPlayer == null)
            {
                Debug.LogWarning("[DifyResponseTester] BufferedAudioPlayerが設定されていません");
                return;
            }

            try
            {
                // レスポンスからanswerフィールドを抽出
                var responseObj = JsonConvert.DeserializeObject<DifyApiResponse>(responseText);
                var answer = responseObj?.answer;
                
                if (string.IsNullOrEmpty(answer))
                {
                    Debug.LogWarning("[DifyResponseTester] answerフィールドが見つかりません");
                    return;
                }

                // text:とvoice:のペアを順序を保持して抽出
                var textChunks = new List<string>();
                var audioUrls = new List<string>();
                
                // ベースURL構築（DifyClientと同じロジック）
                var uri = new Uri(difyUrl);
                var baseUrl = $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}";

                // text:とvoice:のペアをまとめて抽出
                var pairMatches = Regex.Matches(answer, @"text:(.+?)\nvoice:(/files/.*?)(?=\n|$)", RegexOptions.Singleline);
                
                foreach (Match match in pairMatches)
                {
                    var text = match.Groups[1].Value.Trim();
                    var voicePath = match.Groups[2].Value.Trim();
                    
                    if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(voicePath))
                    {
                        textChunks.Add(text);
                        var fullUrl = baseUrl + voicePath;
                        audioUrls.Add(fullUrl);
                    }
                }

                if (audioUrls.Count > 0)
                {
                    Debug.Log($"[DifyResponseTester] 音声URL抽出完了: {audioUrls.Count}チャンク、テキストチャンク: {textChunks.Count}");
                    foreach (var url in audioUrls)
                    {
                        Debug.Log($"[DifyResponseTester] 音声URL: {url}");
                    }
                    foreach (var text in textChunks)
                    {
                        Debug.Log($"[DifyResponseTester] テキスト: {text}");
                    }

                    // BufferedAudioPlayerを初期化（必要に応じて）
                    var audioSource = bufferedAudioPlayer.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = bufferedAudioPlayer.gameObject.AddComponent<AudioSource>();
                    }
                    bufferedAudioPlayer.Initialize(audioSource);

                    // 字幕テキストイベント購読
                    if (subtitleText != null)
                    {
                        bufferedAudioPlayer.OnChunkStarted += OnChunkStarted;
                        bufferedAudioPlayer.OnPlaybackCompleted += OnPlaybackCompleted;
                    }

                    // バッファリング再生開始（テキスト同期対応）
                    var audioStartTime = Time.realtimeSinceStartup;
                    var timeToAudioStart = audioStartTime - testStartTime;
                    Debug.Log($"<color=green>[DifyResponseTester] 🎵 字幕付きバッファリング再生開始 - 会話開始までの時間: {timeToAudioStart:F3}秒</color>");
                    
                    // テキストチャンクがあれば字幕付きで再生、なければ音声のみ
                    if (textChunks.Count > 0)
                    {
                        await bufferedAudioPlayer.PlayBufferedAsync(audioUrls, textChunks);
                    }
                    else
                    {
                        await bufferedAudioPlayer.PlayBufferedAsync(audioUrls);
                    }
                    
                    var totalTime = Time.realtimeSinceStartup - testStartTime;
                    Debug.Log($"<color=lime>[DifyResponseTester] ✅ 全体完了 - 総時間: {totalTime:F3}秒</color>");
                }
                else
                {
                    Debug.LogWarning("[DifyResponseTester] 音声URLが見つかりませんでした");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyResponseTester] 音声再生処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// チャンク開始時の処理
        /// </summary>
        /// <param name="chunkIndex">チャンクインデックス</param>
        /// <param name="text">表示テキスト</param>
        private void OnChunkStarted(int chunkIndex, string text)
        {
            if (subtitleText != null)
            {
                subtitleText.text = text;
                Debug.Log($"[DifyResponseTester] 字幕更新: {text}");
            }
        }

        /// <summary>
        /// 再生完了時の処理
        /// </summary>
        private void OnPlaybackCompleted()
        {
            if (subtitleText != null)
            {
                subtitleText.text = "";
                Debug.Log("[DifyResponseTester] 字幕クリア");
            }
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
    }
}