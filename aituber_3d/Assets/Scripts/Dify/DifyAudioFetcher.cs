#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// Dify音声取得クラス - 複数音声URLの並列ダウンロード・MP3変換
    /// </summary>
    public class DifyAudioFetcher
    {
        private readonly bool debugLog;
        private readonly int timeoutSeconds;
        private readonly string logPrefix = "[DifyAudioFetcher]";

        /// <summary>
        /// DifyAudioFetcherを作成
        /// </summary>
        /// <param name="timeoutSeconds">各URLのダウンロードタイムアウト（秒）</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public DifyAudioFetcher(int timeoutSeconds = 60, bool enableDebugLog = false)
        {
            this.timeoutSeconds = timeoutSeconds;
            debugLog = enableDebugLog;
            
            if (debugLog) Debug.Log($"{logPrefix} 初期化完了 - タイムアウト: {timeoutSeconds}秒/URL");
        }

        /// <summary>
        /// 質問音声と回答音声群を取得
        /// </summary>
        /// <param name="questionAudioUrl">質問音声URL（nullの場合はスキップ）</param>
        /// <param name="answerAudioUrls">回答音声URLリスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>質問音声と回答音声群のタプル</returns>
        public async UniTask<(AudioClip? questionAudio, AudioClip[] answerAudios)> FetchAudioChunks(
            string? questionAudioUrl,
            List<string> answerAudioUrls, 
            CancellationToken cancellationToken = default)
        {
            if (answerAudioUrls.Count == 0)
            {
                Debug.LogWarning($"{logPrefix} 回答音声URLが空です");
                return (null, Array.Empty<AudioClip>());
            }

            try
            {
                var downloadStartTime = Time.realtimeSinceStartup;
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード開始: 質問={questionAudioUrl != null}, 回答={answerAudioUrls.Count}個");

                // 質問音声ダウンロード
                var questionTask = string.IsNullOrEmpty(questionAudioUrl) 
                    ? UniTask.FromResult<AudioClip?>(null)
                    : DownloadAudioClip(questionAudioUrl, cancellationToken);

                // 回答音声群ダウンロード（並列）
                var answerTasks = answerAudioUrls.Select(url => 
                    DownloadAudioClip(url, cancellationToken)
                ).ToList();

                // 全てのダウンロード完了を待機
                var questionAudio = await questionTask;
                var answerResults = await UniTask.WhenAll(answerTasks);

                // null除去（ダウンロード失敗した音声を除外）
                var answerAudios = answerResults.Where(clip => clip != null).Cast<AudioClip>().ToArray();

                var downloadTime = Time.realtimeSinceStartup - downloadStartTime;
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード完了: 質問={questionAudio != null}, 回答={answerAudios.Length}/{answerAudioUrls.Count}個成功 - 時間={downloadTime:F3}秒");

                return (questionAudio, answerAudios);
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロードキャンセル");
                return (null, Array.Empty<AudioClip>());
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 音声ダウンロードエラー: {ex.Message}");
                return (null, Array.Empty<AudioClip>());
            }
        }

        /// <summary>
        /// 音声URLから直接AudioClipをダウンロード
        /// </summary>
        /// <param name="audioUrl">音声URL</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>AudioClip（失敗時はnull）</returns>
        private async UniTask<AudioClip?> DownloadAudioClip(string audioUrl, CancellationToken cancellationToken)
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード開始: {audioUrl}");

                // 音声データダウンロード
                using var request = UnityWebRequest.Get(audioUrl);
                request.timeout = timeoutSeconds;
                await request.SendWebRequest().WithCancellation(cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{logPrefix} 音声ダウンロードエラー: {request.error} - URL: {audioUrl}");
                    return null;
                }

                var audioData = request.downloadHandler.data;
                if (audioData == null || audioData.Length == 0)
                {
                    Debug.LogError($"{logPrefix} 音声データが空です - URL: {audioUrl}");
                    return null;
                }

                // MP3からAudioClipに変換
                var audioClip = await CreateAudioClipFromMp3(audioData, cancellationToken);

                if (audioClip != null && debugLog)
                {
                    Debug.Log($"{logPrefix} 音声変換完了: {audioData.Length} bytes → {audioClip.length:F3}秒");
                }

                return audioClip;
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロードキャンセル: {audioUrl}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 音声ダウンロード例外: {ex.Message} - URL: {audioUrl}");
                return null;
            }
        }

        /// <summary>
        /// MP3データからAudioClipを作成
        /// </summary>
        /// <param name="mp3Data">MP3バイトデータ</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>AudioClip（失敗時はnull）</returns>
        private async UniTask<AudioClip?> CreateAudioClipFromMp3(byte[] mp3Data, CancellationToken cancellationToken)
        {
            try
            {
                var tempPath = Path.GetTempFileName() + ".mp3";
                File.WriteAllBytes(tempPath, mp3Data);

                try
                {
                    using var request = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG);
                    await request.SendWebRequest().WithCancellation(cancellationToken);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return DownloadHandlerAudioClip.GetContent(request);
                    }
                    else
                    {
                        Debug.LogError($"{logPrefix} MP3変換エラー: {request.error}");
                        return null;
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} MP3変換例外: {ex.Message}");
                return null;
            }
        }
    }
}