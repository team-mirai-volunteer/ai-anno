#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// 字幕音声再生タスク - IEventNode準拠のキューベース字幕付き音声再生
    /// </summary>
    public class SubtitleAudioTask : IEventNode
    {
        /// <summary>
        /// 処理が開始されたか
        /// </summary>
        public bool IsStarted { get; private set; } = false;
        
        /// <summary>
        /// 処理が完了したか
        /// </summary>
        public bool IsCompleted { get; private set; } = false;

        /// <summary>
        /// MainUIController用アクセサ - コメント
        /// </summary>
        public OneCommeComment Comment => comment;

        /// <summary>
        /// MainUIController用アクセサ - ユーザー名
        /// </summary>
        public string UserName => userName;

        /// <summary>
        /// チャンク開始イベント（字幕表示用）
        /// </summary>
        public static event Action<string>? OnChunkStarted;

        private readonly OneCommeComment comment;
        private readonly AudioClip[] answerAudios;
        private readonly List<DifyChunk> chunks;
        private readonly string userName;
        private SubtitleAudioRenderer? audioRenderer;
        private readonly bool debugLog;
        private readonly float gapAfterAudioSeconds;
        private readonly float gapBetweenAudioSeconds;
        private readonly string logPrefix = "[SubtitleAudioTask]";

        /// <summary>
        /// SubtitleAudioTaskを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="answerAudios">回答音声群</param>
        /// <param name="chunks">Difyチャンクデータ</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="gapBetweenAudioSeconds">音声再生前のギャップ秒数</param>
        /// <param name="gapAfterAudioSeconds">音声終了後のギャップ秒数</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public SubtitleAudioTask(
            OneCommeComment comment,
            AudioClip[] answerAudios,
            List<DifyChunk> chunks,
            string userName,
            float gapBetweenAudioSeconds = 1.0f,
            float gapAfterAudioSeconds = 2.0f,
            bool enableDebugLog = false)
        {
            this.comment = comment ?? throw new ArgumentNullException(nameof(comment));
            this.answerAudios = answerAudios ?? throw new ArgumentNullException(nameof(answerAudios));
            this.chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
            this.userName = userName ?? "匿名";
            this.audioRenderer = null; // AudioSourceは後で設定
            this.gapBetweenAudioSeconds = gapBetweenAudioSeconds;
            this.gapAfterAudioSeconds = gapAfterAudioSeconds;
            debugLog = enableDebugLog;

            if (debugLog) Debug.Log($"{logPrefix} タスク作成: [{userName}] 回答音声={answerAudios.Length}個, チャンク={chunks.Count}個");
        }

        /// <summary>
        /// AudioSourceを設定してSubtitleAudioRendererを初期化
        /// </summary>
        /// <param name="audioSource">使用するAudioSource</param>
        public void SetAudioSource(AudioSource audioSource)
        {
            if (audioSource == null) throw new ArgumentNullException(nameof(audioSource));
            
            audioRenderer = new SubtitleAudioRenderer(audioSource, debugLog);
            
            if (debugLog) Debug.Log($"{logPrefix} AudioSource設定完了: [{userName}]");
        }

        /// <summary>
        /// 字幕音声再生タスクを実行
        /// </summary>
        public async UniTask Execute()
        {
            IsStarted = true;
            
            try
            {
                // 音声再生前のギャップ
                if (gapBetweenAudioSeconds > 0)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 音声再生前ギャップ待機: [{userName}] {gapBetweenAudioSeconds}秒");
                    await UniTask.Delay((int)(gapBetweenAudioSeconds * 1000));
                }
                
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生開始: [{userName}]");

                // AudioRendererの初期化チェック
                if (audioRenderer == null)
                {
                    Debug.LogError($"{logPrefix} AudioRendererが未初期化: [{userName}] SetAudioSource()を先に呼び出してください");
                    IsCompleted = true;
                    return;
                }

                // 回答音声再生（チャンクごとに字幕付き）
                if (answerAudios.Length > 0)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 回答音声再生開始: [{userName}] {answerAudios.Length}個");

                    for (int i = 0; i < answerAudios.Length; i++)
                    {
                        var audioClip = answerAudios[i];
                        if (audioClip == null)
                        {
                            Debug.LogWarning($"{logPrefix} 無効な回答音声[{i}]: [{userName}]");
                            continue;
                        }

                        // 対応する字幕テキストを取得
                        string subtitleText = "";
                        if (i < chunks.Count)
                        {
                            subtitleText = chunks[i].Text ?? "";
                        }

                        if (debugLog) Debug.Log($"{logPrefix} 回答音声[{i}]再生開始: [{userName}] 字幕=\"{subtitleText}\"");

                        // 字幕イベント通知
                        if (!string.IsNullOrEmpty(subtitleText))
                        {
                            OnChunkStarted?.Invoke(subtitleText);
                        }

                        // 音声再生
                        await audioRenderer.RenderSingleAudio(audioClip, subtitleText);

                        if (debugLog) Debug.Log($"{logPrefix} 回答音声[{i}]再生完了: [{userName}]");
                    }

                    if (debugLog) Debug.Log($"{logPrefix} 回答音声再生完了: [{userName}]");
                }
                else
                {
                    Debug.LogWarning($"{logPrefix} 回答音声が0個: [{userName}]");
                }

                // 音声終了後のギャップ
                if (gapAfterAudioSeconds > 0)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 音声終了後ギャップ待機: [{userName}] {gapAfterAudioSeconds}秒");
                    await UniTask.Delay((int)(gapAfterAudioSeconds * 1000));
                }

                IsCompleted = true;
                
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生タスク完了: [{userName}]");
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生キャンセル: [{userName}]");
                IsCompleted = true; // キャンセルも完了扱い
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 字幕音声再生エラー: [{userName}] {ex.Message}");
                IsCompleted = true; // エラーも完了扱い
            }
            finally
            {
                // AudioClipリソース解放
                CleanupAudioResources();
            }
        }

        /// <summary>
        /// AudioClipリソースを解放
        /// </summary>
        private void CleanupAudioResources()
        {
            try
            {
                foreach (var audioClip in answerAudios)
                {
                    if (audioClip != null)
                    {
                        UnityEngine.Object.Destroy(audioClip);
                    }
                }
                
                if (debugLog) Debug.Log($"{logPrefix} 回答音声リソース解放: [{userName}] {answerAudios.Length}個");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} リソース解放エラー: [{userName}] {ex.Message}");
            }
        }

        /// <summary>
        /// タスク情報を文字列として取得（デバッグ用）
        /// </summary>
        /// <returns>タスク情報</returns>
        public override string ToString()
        {
            var status = !IsStarted ? "未開始" : IsCompleted ? "完了" : "実行中";
            var audioInfo = $"回答={answerAudios.Length}個";
            return $"[{userName}:{comment.data?.comment?.Substring(0, Math.Min(10, comment.data?.comment?.Length ?? 0))}...] {audioInfo} {status}";
        }
    }
}