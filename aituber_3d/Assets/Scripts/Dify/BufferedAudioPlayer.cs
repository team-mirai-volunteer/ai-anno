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
    /// 並列音声再生プレイヤー
    /// チャンク化された音声の並列ダウンロード・順次再生
    /// </summary>
    public class BufferedAudioPlayer
    {
        private readonly float gapBetweenChunks;
        private readonly bool debugLog;
        private AudioSource? audioSource;
        private readonly string logPrefix = "[BufferedAudioPlayer]";
        
        private bool isPlaying = false;
        private CancellationTokenSource? cancellationTokenSource;
        private List<string>? currentTextChunks;

        /// <summary>
        /// 再生完了イベント
        /// </summary>
        public event Action? OnPlaybackCompleted;
        
        /// <summary>
        /// エラーイベント
        /// </summary>
        public event Action<string>? OnPlaybackError;
        
        /// <summary>
        /// チャンク開始イベント（テキスト表示用）
        /// </summary>
        public event Action<string>? OnChunkStarted;

        /// <summary>
        /// BufferedAudioPlayerを作成
        /// </summary>
        /// <param name="audioSourceComponent">AudioSourceコンポーネント</param>
        /// <param name="gapBetweenChunks">チャンク間のギャップ（秒）</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public BufferedAudioPlayer(AudioSource audioSourceComponent, float gapBetweenChunks = 1.0f, bool enableDebugLog = false)
        {
            audioSource = audioSourceComponent ?? throw new ArgumentNullException(nameof(audioSourceComponent));
            this.gapBetweenChunks = gapBetweenChunks;
            debugLog = enableDebugLog;
            
            if (debugLog) Debug.Log($"{logPrefix} 初期化完了 - ギャップ: {gapBetweenChunks}秒");
        }

        /// <summary>
        /// バッファリング再生開始
        /// </summary>
        /// <param name="audioUrls">音声URLリスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async UniTask PlayBufferedAsync(List<string> audioUrls, CancellationToken cancellationToken = default)
        {
            await PlayBufferedAsync(audioUrls, null, cancellationToken);
        }

        /// <summary>
        /// バッファリング再生開始（テキスト同期対応）
        /// </summary>
        /// <param name="audioUrls">音声URLリスト</param>
        /// <param name="textChunks">対応するテキストチャンクリスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async UniTask PlayBufferedAsync(List<string> audioUrls, List<string>? textChunks, CancellationToken cancellationToken = default)
        {
            if (audioSource == null)
            {
                Debug.LogError($"{logPrefix} AudioSourceが未初期化です");
                return;
            }

            if (audioUrls.Count == 0)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声URLリストが空です");
                return;
            }

            try
            {
                // 前の再生をキャンセル
                StopPlayback();
                
                cancellationTokenSource = new CancellationTokenSource();
                
                // 外部キャンセルトークンと内部キャンセルトークンを結合
                using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
                var combinedToken = combinedTokenSource.Token;

                // テキストチャンクを保存
                currentTextChunks = textChunks;


                var startTime = Time.realtimeSinceStartup;
                if (debugLog) Debug.Log($"{logPrefix} 並列再生開始: {audioUrls.Count}チャンク - 開始時刻: {startTime:F3}秒");

                // 並列ダウンロード戦略のみ
                await PlayParallelDownload(audioUrls, combinedToken);
                
                var totalTime = Time.realtimeSinceStartup - startTime;
                if (debugLog) Debug.Log($"{logPrefix} ★並列再生完了★ 総時間: {totalTime:F3}秒");
                
                OnPlaybackCompleted?.Invoke();
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 再生がキャンセルされました");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 再生エラー: {ex.Message}");
                OnPlaybackError?.Invoke(ex.Message);
            }
            finally
            {
                isPlaying = false;
            }
        }


        /// <summary>
        /// 並列ダウンロード戦略
        /// </summary>
        /// <param name="audioUrls">音声URLリスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask PlayParallelDownload(List<string> audioUrls, CancellationToken cancellationToken)
        {
            try
            {
                var downloadStartTime = Time.realtimeSinceStartup;
                if (debugLog) Debug.Log($"{logPrefix} 並列ダウンロード開始: {audioUrls.Count}チャンク");

                // 全音声を並列ダウンロード（順序保持）
                if (debugLog)
                {
                    Debug.Log($"{logPrefix} === 音声URL順序確認 ===");
                    for (int i = 0; i < audioUrls.Count; i++)
                    {
                        Debug.Log($"{logPrefix} [{i}]: {audioUrls[i]}");
                    }
                }
                
                var downloadTasks = audioUrls.Select((url, index) => 
                    DownloadAudioClip(url, cancellationToken).ContinueWith(clip => new { Index = index, Clip = clip })
                ).ToList();
                var downloadResults = await UniTask.WhenAll(downloadTasks);
                
                // 順序でソートして配列に格納
                var audioClips = new AudioClip?[audioUrls.Count];
                foreach (var result in downloadResults)
                {
                    audioClips[result.Index] = result.Clip;
                    if (debugLog)
                    {
                        var status = result.Clip != null ? "成功" : "失敗";
                        Debug.Log($"{logPrefix} ダウンロード結果[{result.Index}]: {status}");
                    }
                }

                var downloadTime = Time.realtimeSinceStartup - downloadStartTime;
                if (debugLog) Debug.Log($"{logPrefix} 並列ダウンロード完了: {audioClips.Count(c => c != null)}チャンク成功 - ダウンロード時間: {downloadTime:F3}秒");

                // ダウンロード成功した音声を順次再生
                isPlaying = true;
                for (int i = 0; i < audioClips.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var audioClip = audioClips[i];
                    
                    // テキスト表示イベントは常に発火（音声の成功/失敗に関係なく）
                    var currentText = GetTextForChunk(i);
                    OnChunkStarted?.Invoke(currentText);
                    
                    if (audioClip != null)
                    {
                        if (debugLog) Debug.Log($"{logPrefix} チャンク{i + 1}/{audioClips.Length}再生開始: {currentText}");
                        
                        await PlayAudioClip(audioClip, cancellationToken);
                        
                        // AudioClip解放
                        UnityEngine.Object.DestroyImmediate(audioClip);
                    }
                    else
                    {
                        Debug.LogWarning($"{logPrefix} チャンク{i + 1}の音声ダウンロードに失敗しました（字幕のみ表示）: {currentText}");
                    }

                    // チャンク間のギャップ（音声の成功/失敗に関係なく）
                    if (gapBetweenChunks > 0 && i < audioClips.Length - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(gapBetweenChunks), cancellationToken: cancellationToken);
                    }
                }

                if (debugLog) Debug.Log($"{logPrefix} 並列再生完了");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 並列再生エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 音声URLから直接AudioClipをダウンロード
        /// </summary>
        /// <param name="audioUrl">音声URL</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>AudioClip</returns>
        private async UniTask<AudioClip?> DownloadAudioClip(string audioUrl, CancellationToken cancellationToken)
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード開始: {audioUrl}");

                // 音声データダウンロード
                using var request = UnityWebRequest.Get(audioUrl);
                await request.SendWebRequest().WithCancellation(cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{logPrefix} 音声ダウンロードエラー: {request.error}");
                    return null;
                }

                var audioData = request.downloadHandler.data;
                
                // MP3からAudioClipに変換
                var audioClip = await CreateAudioClipFromMp3(audioData, cancellationToken);
                
                if (audioClip != null && debugLog)
                {
                    Debug.Log($"{logPrefix} 音声変換完了: {audioData.Length} bytes → {audioClip.length}秒");
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
                Debug.LogError($"{logPrefix} 音声ダウンロード例外: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// MP3データからAudioClipを作成
        /// </summary>
        /// <param name="mp3Data">MP3バイトデータ</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>AudioClip</returns>
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

        /// <summary>
        /// AudioClipを再生して完了まで待機
        /// </summary>
        /// <param name="audioClip">再生するAudioClip</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask PlayAudioClip(AudioClip audioClip, CancellationToken cancellationToken)
        {
            if (audioSource == null) return;

            try
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                audioSource.clip = audioClip;
                audioSource.Play();

                if (debugLog) Debug.Log($"{logPrefix} チャンク再生開始: {audioClip.length}秒");

                // 再生完了まで待機（フレーム単位で監視）
                await UniTask.WaitUntil(() => !audioSource.isPlaying, cancellationToken: cancellationToken);

                if (debugLog) Debug.Log($"{logPrefix} チャンク再生完了");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} チャンク再生エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 再生停止
        /// </summary>
        public void StopPlayback()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            isPlaying = false;

            if (debugLog) Debug.Log($"{logPrefix} 再生停止");
        }

        /// <summary>
        /// 再生中かどうか
        /// </summary>
        public bool IsPlaying => isPlaying;

        /// <summary>
        /// 指定インデックスのテキストチャンクを取得
        /// </summary>
        /// <param name="index">チャンクインデックス</param>
        /// <returns>テキストチャンク</returns>
        private string GetTextForChunk(int index)
        {
            if (currentTextChunks == null || index < 0 || index >= currentTextChunks.Count)
            {
                return $"チャンク{index + 1}";
            }
            return currentTextChunks[index];
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        public void Dispose()
        {
            StopPlayback();
        }
    }
}