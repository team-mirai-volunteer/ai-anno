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
            Debug.Log($"[DifyResponseTester] Difyテスト開始: {testMessage}");
            
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
                var bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

                using (var request = new UnityWebRequest(difyUrl, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;
                        Debug.Log("[DifyResponseTester] レスポンス取得成功");
                        
                        // 音声URLを抽出してバッファリング再生
                        await ProcessAudioPlayback(responseText);
                    }
                    else
                    {
                        Debug.LogError($"[DifyResponseTester] リクエスト失敗: {request.error} (Code: {request.responseCode})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyResponseTester] 例外発生: {ex.Message}");
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
                    Debug.Log($"[DifyResponseTester] 音声URL抽出完了: {audioUrls.Count}チャンク");

                    // BufferedAudioPlayerはPure C#になったため、このテスターは非対応
                    Debug.LogWarning("[DifyResponseTester] BufferedAudioPlayerがPure C#になったため、テスト機能は無効化されています");

                    /* BufferedAudioPlayerがPure C#になったため、以下のテスト機能は無効化
                    // 字幕テキストイベント購読
                    if (subtitleText != null)
                    {
                        bufferedAudioPlayer.OnChunkStarted += OnChunkStarted;
                        bufferedAudioPlayer.OnPlaybackCompleted += OnPlaybackCompleted;
                    }

                    // バッファリング再生開始（テキスト同期対応）
                    Debug.Log("[DifyResponseTester] 字幕付きバッファリング再生開始");
                    
                    // テキストチャンクがあれば字幕付きで再生、なければ音声のみ
                    if (textChunks.Count > 0)
                    {
                        await bufferedAudioPlayer.PlayBufferedAsync(audioUrls, textChunks);
                    }
                    else
                    {
                        await bufferedAudioPlayer.PlayBufferedAsync(audioUrls);
                    }
                    
                    Debug.Log("[DifyResponseTester] 全体完了");
                    */
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