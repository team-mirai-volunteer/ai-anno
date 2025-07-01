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
    /// バッファリング型音声再生プロトタイプ
    /// チャンク化された音声の先読み再生
    /// </summary>
    public class BufferedAudioPlayer : MonoBehaviour
    {
        [Header("Download Strategy")]
        [SerializeField] private bool useParallelDownload = false;
        
        [Header("Buffer Settings")]
        [SerializeField] private int bufferSize = 3;
        [SerializeField] private float gapBetweenChunks = 0.5f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = true;
        
        [Header("Test Settings")]
        [SerializeField] private List<string> testAudioUrls = new List<string>();
        
        private AudioSource? audioSource;
        private readonly Queue<AudioClip> audioBuffer = new Queue<AudioClip>();
        private readonly Queue<string> pendingUrls = new Queue<string>();
        private readonly string logPrefix = "[BufferedAudioPlayer]";
        
        private bool isPlaying = false;
        private bool isBuffering = false;
        private CancellationTokenSource? cancellationTokenSource;
        private List<string>? currentTextChunks;
        private int currentChunkIndex;

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
        public event Action<int, string>? OnChunkStarted;

        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="audioSourceComponent">AudioSourceコンポーネント</param>
        public void Initialize(AudioSource audioSourceComponent)
        {
            audioSource = audioSourceComponent ?? throw new ArgumentNullException(nameof(audioSourceComponent));
            if (debugLog) Debug.Log($"{logPrefix} 初期化完了");
        }

        /// <summary>
        /// バッファリング再生開始
        /// </summary>
        /// <param name="audioUrls">音声URLリスト</param>
        public async UniTask PlayBufferedAsync(List<string> audioUrls)
        {
            await PlayBufferedAsync(audioUrls, null);
        }

        /// <summary>
        /// バッファリング再生開始（テキスト同期対応）
        /// </summary>
        /// <param name="audioUrls">音声URLリスト</param>
        /// <param name="textChunks">対応するテキストチャンクリスト</param>
        public async UniTask PlayBufferedAsync(List<string> audioUrls, List<string>? textChunks)
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
                var cancellationToken = cancellationTokenSource.Token;

                // テキストチャンクを保存
                currentTextChunks = textChunks;
                currentChunkIndex = 0;

                // URLキューに追加
                pendingUrls.Clear();
                foreach (var url in audioUrls)
                {
                    pendingUrls.Enqueue(url);
                }

                var startTime = Time.realtimeSinceStartup;
                if (debugLog) Debug.Log($"{logPrefix} {(useParallelDownload ? "並列" : "バッファリング")}再生開始: {audioUrls.Count}チャンク - 開始時刻: {startTime:F3}秒");

                if (useParallelDownload)
                {
                    // 並列ダウンロード戦略
                    await PlayParallelDownload(audioUrls, cancellationToken);
                }
                else
                {
                    // バッファリング戦略
                    await InitialBuffering(cancellationToken);
                    await PlayBufferedChunks(cancellationToken);
                }
                
                var totalTime = Time.realtimeSinceStartup - startTime;
                if (debugLog) Debug.Log($"{logPrefix} ★{(useParallelDownload ? "並列" : "バッファリング")}再生完了★ 総時間: {totalTime:F3}秒");
                
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
                CleanupBuffers();
                isPlaying = false;
                isBuffering = false;
            }
        }

        /// <summary>
        /// 初期バッファリング（最初のN個をダウンロード）
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask InitialBuffering(CancellationToken cancellationToken)
        {
            isBuffering = true;
            var tasks = new List<UniTask>();
            var initialBufferCount = Math.Min(bufferSize, pendingUrls.Count);
            var bufferStartTime = Time.realtimeSinceStartup;

            if (debugLog) Debug.Log($"{logPrefix} 初期バッファリング開始: {initialBufferCount}チャンク");

            for (int i = 0; i < initialBufferCount; i++)
            {
                if (pendingUrls.Count > 0)
                {
                    var url = pendingUrls.Dequeue();
                    tasks.Add(DownloadAndBufferAudio(url, cancellationToken));
                }
            }

            await UniTask.WhenAll(tasks);
            isBuffering = false;
            
            var bufferTime = Time.realtimeSinceStartup - bufferStartTime;
            if (debugLog) Debug.Log($"{logPrefix} 初期バッファリング完了: {audioBuffer.Count}チャンク準備完了 - バッファ時間: {bufferTime:F3}秒");
        }

        /// <summary>
        /// バッファされたチャンクを順次再生
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask PlayBufferedChunks(CancellationToken cancellationToken)
        {
            isPlaying = true;

            while (audioBuffer.Count > 0 || pendingUrls.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // バッファに音声があれば再生
                if (audioBuffer.Count > 0)
                {
                    var audioClip = audioBuffer.Dequeue();
                    
                    // テキスト表示イベント発火
                    var currentText = GetTextForChunk(currentChunkIndex);
                    OnChunkStarted?.Invoke(currentChunkIndex, currentText);
                    
                    // 次のチャンクをバックグラウンドでダウンロード開始
                    if (pendingUrls.Count > 0 && audioBuffer.Count < bufferSize - 1)
                    {
                        var nextUrl = pendingUrls.Dequeue();
                        _ = DownloadAndBufferAudio(nextUrl, cancellationToken);
                    }

                    // 現在のチャンクを再生
                    await PlayAudioClip(audioClip, cancellationToken);
                    currentChunkIndex++;
                    
                    // AudioClip解放
                    if (audioClip != null)
                    {
                        DestroyImmediate(audioClip);
                    }

                    // チャンク間のギャップ
                    if (gapBetweenChunks > 0)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(gapBetweenChunks), cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    // バッファが空の場合は少し待機
                    if (debugLog) Debug.Log($"{logPrefix} バッファ待機中...");
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                }
            }

            if (debugLog) Debug.Log($"{logPrefix} 全チャンク再生完了");
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

                // 全音声を並列ダウンロード
                var downloadTasks = audioUrls.Select(url => DownloadAudioClip(url, cancellationToken)).ToList();
                var audioClips = await UniTask.WhenAll(downloadTasks);

                var downloadTime = Time.realtimeSinceStartup - downloadStartTime;
                if (debugLog) Debug.Log($"{logPrefix} 並列ダウンロード完了: {audioClips.Count(c => c != null)}チャンク成功 - ダウンロード時間: {downloadTime:F3}秒");

                // ダウンロード成功した音声を順次再生
                isPlaying = true;
                for (int i = 0; i < audioClips.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var audioClip = audioClips[i];
                    if (audioClip != null)
                    {
                        // テキスト表示イベント発火
                        var currentText = GetTextForChunk(i);
                        OnChunkStarted?.Invoke(i, currentText);
                        
                        if (debugLog) Debug.Log($"{logPrefix} チャンク{i + 1}/{audioClips.Length}再生開始: {currentText}");
                        
                        await PlayAudioClip(audioClip, cancellationToken);
                        
                        // AudioClip解放
                        DestroyImmediate(audioClip);

                        // チャンク間のギャップ
                        if (gapBetweenChunks > 0 && i < audioClips.Length - 1)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(gapBetweenChunks), cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"{logPrefix} チャンク{i + 1}のダウンロードに失敗しました");
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
        /// 音声ダウンロードしてバッファに追加
        /// </summary>
        /// <param name="audioUrl">音声URL</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask DownloadAndBufferAudio(string audioUrl, CancellationToken cancellationToken)
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
                    return;
                }

                var audioData = request.downloadHandler.data;
                
                // MP3からAudioClipに変換
                var audioClip = await CreateAudioClipFromMp3(audioData, cancellationToken);
                
                if (audioClip != null)
                {
                    audioBuffer.Enqueue(audioClip);
                    if (debugLog) Debug.Log($"{logPrefix} バッファに追加完了: {audioData.Length} bytes → バッファサイズ: {audioBuffer.Count}");
                }
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロードキャンセル: {audioUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 音声ダウンロード例外: {ex.Message}");
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

                // 再生完了まで待機
                while (audioSource.isPlaying)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Delay(50, cancellationToken: cancellationToken);
                }

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

            CleanupBuffers();
            isPlaying = false;
            isBuffering = false;

            if (debugLog) Debug.Log($"{logPrefix} 再生停止");
        }

        /// <summary>
        /// バッファクリーンアップ
        /// </summary>
        private void CleanupBuffers()
        {
            while (audioBuffer.Count > 0)
            {
                var clip = audioBuffer.Dequeue();
                if (clip != null)
                {
                    DestroyImmediate(clip);
                }
            }
            
            pendingUrls.Clear();
        }

        /// <summary>
        /// 再生中かどうか
        /// </summary>
        public bool IsPlaying => isPlaying;

        /// <summary>
        /// バッファリング中かどうか
        /// </summary>
        public bool IsBuffering => isBuffering;

        /// <summary>
        /// 現在のバッファサイズ
        /// </summary>
        public int CurrentBufferSize => audioBuffer.Count;

        /// <summary>
        /// テスト用バッファリング再生
        /// </summary>
        [ContextMenu("Test Buffered Playback")]
        public async void TestBufferedPlayback()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    Debug.LogError($"{logPrefix} AudioSourceコンポーネントが見つかりません");
                    return;
                }
                Initialize(audioSource);
            }

            if (testAudioUrls.Count == 0)
            {
                Debug.LogWarning($"{logPrefix} テスト用音声URLが設定されていません");
                return;
            }

            Debug.Log($"{logPrefix} テスト開始: {testAudioUrls.Count}チャンク");
            await PlayBufferedAsync(testAudioUrls);
        }

        /// <summary>
        /// テスト停止
        /// </summary>
        [ContextMenu("Stop Test Playback")]
        public void StopTestPlayback()
        {
            StopPlayback();
            Debug.Log($"{logPrefix} テスト停止");
        }

        /// <summary>
        /// バッファ状態表示
        /// </summary>
        [ContextMenu("Show Buffer Status")]
        public void ShowBufferStatus()
        {
            Debug.Log($"{logPrefix} バッファ状態:");
            Debug.Log($"  - IsPlaying: {IsPlaying}");
            Debug.Log($"  - IsBuffering: {IsBuffering}");
            Debug.Log($"  - CurrentBufferSize: {CurrentBufferSize}");
            Debug.Log($"  - PendingUrls: {pendingUrls.Count}");
        }

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
        /// MonoBehaviour破棄時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            StopPlayback();
        }
    }
}