#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// 字幕付き音声レンダラー - 時間ベース音声再生制御
    /// </summary>
    public class SubtitleAudioRenderer
    {
        private readonly AudioSource audioSource;
        private readonly bool debugLog;
        private readonly string logPrefix = "[SubtitleAudioRenderer]";
        
        private float playStartTime;
        private float expectedPlayDuration;
        private bool isTimePlaying = false;

        /// <summary>
        /// チャンク開始イベント（字幕表示用）
        /// </summary>
        public event Action<string>? OnChunkStarted;
        
        /// <summary>
        /// チャンク完了イベント
        /// </summary>
        public event Action<string>? OnChunkCompleted;
        
        /// <summary>
        /// 全チャンク完了イベント
        /// </summary>
        public event Action? OnAllChunksCompleted;

        /// <summary>
        /// 時間ベース再生中かどうか
        /// </summary>
        public bool IsTimePlaying => isTimePlaying && (Time.realtimeSinceStartup - playStartTime < expectedPlayDuration);

        /// <summary>
        /// SubtitleAudioRendererを作成
        /// </summary>
        /// <param name="audioSource">AudioSourceコンポーネント</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public SubtitleAudioRenderer(AudioSource audioSource, bool enableDebugLog = false)
        {
            this.audioSource = audioSource ?? throw new ArgumentNullException(nameof(audioSource));
            debugLog = enableDebugLog;
            
            if (debugLog) Debug.Log($"{logPrefix} 初期化完了");
        }

        /// <summary>
        /// 複数音声チャンクの順次再生
        /// </summary>
        /// <param name="audioClips">音声クリップ配列</param>
        /// <param name="textChunks">対応するテキストチャンク</param>
        /// <param name="gapBetweenChunks">チャンク間のギャップ（秒）</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async UniTask RenderAudio(
            AudioClip[] audioClips, 
            List<string> textChunks, 
            float gapBetweenChunks, 
            CancellationToken cancellationToken = default)
        {
            if (audioClips.Length == 0)
            {
                Debug.LogWarning($"{logPrefix} 音声クリップが空です");
                return;
            }

            if (debugLog) Debug.Log($"{logPrefix} 音声レンダリング開始: {audioClips.Length}チャンク");

            try
            {
                for (int i = 0; i < audioClips.Length; i++)
                {
                    var audioClip = audioClips[i];
                    var text = GetTextForChunk(textChunks, i);
                    
                    // 最後のチャンクかどうかをチェック
                    bool isLastChunk = (i == audioClips.Length - 1);
                    float gap = isLastChunk ? 0 : gapBetweenChunks;
                    
                    await RenderSingleChunk(audioClip, text, gap, cancellationToken);
                }
                
                OnAllChunksCompleted?.Invoke();
                if (debugLog) Debug.Log($"{logPrefix} 全チャンク再生完了");
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声レンダリングキャンセル");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 音声レンダリングエラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 単一音声の再生（質問読み上げ用）
        /// </summary>
        /// <param name="audioClip">音声クリップ</param>
        /// <param name="text">表示テキスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async UniTask RenderSingleAudio(
            AudioClip audioClip, 
            string text, 
            CancellationToken cancellationToken = default)
        {
            await RenderSingleChunk(audioClip, text, 0, cancellationToken);
        }

        /// <summary>
        /// 単一チャンクの再生処理
        /// </summary>
        /// <param name="audioClip">音声クリップ</param>
        /// <param name="text">表示テキスト</param>
        /// <param name="gapAfterPlay">再生後のギャップ（秒）</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask RenderSingleChunk(
            AudioClip audioClip, 
            string text, 
            float gapAfterPlay, 
            CancellationToken cancellationToken)
        {
            try
            {
                OnChunkStarted?.Invoke(text);
                
                // 時間計算（音声長 + ギャップ）
                expectedPlayDuration = audioClip.length + gapAfterPlay;
                playStartTime = Time.realtimeSinceStartup;
                isTimePlaying = true;
                
                if (debugLog) Debug.Log($"{logPrefix} チャンク再生開始: {text} ({audioClip.length:F3}秒 + ギャップ{gapAfterPlay:F3}秒)");
                
                // 音声再生開始
                audioSource.clip = audioClip;
                audioSource.Play();
                
                // 時間ベース待機
                await UniTask.WaitUntil(() => !IsTimePlaying, cancellationToken: cancellationToken);
                
                OnChunkCompleted?.Invoke(text);
                if (debugLog) Debug.Log($"{logPrefix} チャンク再生完了: {text}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} チャンク再生エラー: {text} - {ex.Message}");
                throw;
            }
            finally
            {
                isTimePlaying = false;
                
                // AudioClip解放
                if (audioClip != null)
                {
                    UnityEngine.Object.DestroyImmediate(audioClip);
                }
            }
        }

        /// <summary>
        /// 指定インデックスのテキストチャンクを取得
        /// </summary>
        /// <param name="textChunks">テキストチャンクリスト</param>
        /// <param name="index">チャンクインデックス</param>
        /// <returns>テキストチャンク</returns>
        private string GetTextForChunk(List<string> textChunks, int index)
        {
            if (textChunks == null || index < 0 || index >= textChunks.Count)
            {
                return $"チャンク{index + 1}";
            }
            return textChunks[index];
        }

        /// <summary>
        /// 再生停止
        /// </summary>
        public void StopPlayback()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            isTimePlaying = false;
            
            if (debugLog) Debug.Log($"{logPrefix} 再生停止");
        }
    }
}