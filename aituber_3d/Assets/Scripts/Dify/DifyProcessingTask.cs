#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Linq;
using AiTuber;

namespace AiTuber.Dify
{
    /// <summary>
    /// Dify処理タスク - 自律型AI処理・音声ダウンロード・バリデーション
    /// </summary>
    public class DifyProcessingTask
    {
        public OneCommeComment Comment { get; }
        public string UserName { get; }
        
        private readonly DifyChunkedClient difyClient;
        private readonly DifyAudioFetcher audioFetcher;
        private readonly float timeoutSeconds;
        private readonly float gapBetweenAudioSeconds;
        private readonly float gapAfterAudioSeconds;
        private readonly bool debugLog;
        private readonly string logPrefix = "[DifyProcessingTask]";
        
        private bool isCompleted = false;
        private bool hasError = false;
        private float startTime;
        private IEventNode? completedEventNode = null;
        
        /// <summary>
        /// 処理が完了したか
        /// </summary>
        public bool IsCompleted => isCompleted || IsTimedOut;
        
        /// <summary>
        /// エラーが発生したか
        /// </summary>
        public bool HasError => hasError || IsTimedOut;
        
        /// <summary>
        /// 処理完了またはエラーで取り出し可能か
        /// </summary>
        public bool IsReadyToDequeue => IsCompleted || HasError;
        
        /// <summary>
        /// タイムアウトしたか
        /// </summary>
        public bool IsTimedOut => Time.realtimeSinceStartup - startTime > timeoutSeconds;
        
        /// <summary>
        /// 完了したイベントノード
        /// </summary>
        public IEventNode? CompletedEventNode => completedEventNode;
        
        /// <summary>
        /// Difyレスポンス（MainUIController用）
        /// </summary>
        public DifyChunkedResponse? DifyResponse { get; private set; }

        /// <summary>
        /// DifyProcessingTaskを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="difyClient">Difyクライアント</param>
        /// <param name="audioFetcher">音声取得クライアント</param>
        /// <param name="timeoutSeconds">タイムアウト（秒）</param>
        /// <param name="gapBetweenAudioSeconds">音声再生前のギャップ秒数</param>
        /// <param name="gapAfterAudioSeconds">音声終了後のギャップ秒数</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public DifyProcessingTask(
            OneCommeComment comment,
            string userName,
            DifyChunkedClient difyClient,
            DifyAudioFetcher audioFetcher,
            float timeoutSeconds = 60.0f,
            float gapBetweenAudioSeconds = 1.0f,
            float gapAfterAudioSeconds = 2.0f,
            bool enableDebugLog = false)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            this.difyClient = difyClient ?? throw new ArgumentNullException(nameof(difyClient));
            this.audioFetcher = audioFetcher ?? throw new ArgumentNullException(nameof(audioFetcher));
            this.timeoutSeconds = timeoutSeconds;
            this.gapBetweenAudioSeconds = gapBetweenAudioSeconds;
            this.gapAfterAudioSeconds = gapAfterAudioSeconds;
            debugLog = enableDebugLog;
            
            if (debugLog) Debug.Log($"{logPrefix} タスク作成: [{UserName}] {comment.data?.comment}");
        }

        /// <summary>
        /// 自律処理開始
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async UniTask StartProcessing(CancellationToken cancellationToken = default)
        {
            startTime = Time.realtimeSinceStartup;
            
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 自律処理開始: [{UserName}]");
                
                // 1. Dify API呼び出し
                var commentText = Comment.data?.comment ?? "";
                if (string.IsNullOrEmpty(commentText))
                {
                    Debug.LogWarning($"{logPrefix} コメントテキストが空です: [{UserName}]");
                    hasError = true;
                    return;
                }
                
                var response = await difyClient.SendQueryAsync(commentText, UserName);
                DifyResponse = response; // UI用に保存
                
                if (!response.IsSuccess)
                {
                    Debug.LogError($"{logPrefix} Dify API失敗: [{UserName}] {response.ErrorMessage}");
                    hasError = true;
                    return;
                }
                
                if (debugLog) Debug.Log($"{logPrefix} Dify API完了: [{UserName}] チャンク数={response.ChunkCount}");
                
                // 2. 音声ダウンロード（回答音声のみ）
                var answerAudioUrls = response.Chunks
                    .Where(chunk => chunk.HasAudioUrl)
                    .Select(chunk => chunk.AudioUrl!)
                    .ToList();
                
                var (questionAudio, answerAudios) = await audioFetcher.FetchAudioChunks(
                    null, // 質問音声はなし
                    answerAudioUrls,
                    cancellationToken
                );
                
                if (debugLog) Debug.Log($"{logPrefix} 音声ダウンロード完了: [{UserName}] 質問={questionAudio != null}, 回答={answerAudios.Length}個");
                
                // 3. バリデーション
                if (!ValidateAudioData(questionAudio, answerAudios, response))
                {
                    hasError = true;
                    return;
                }
                
                // 4. SubtitleAudioTask作成
                completedEventNode = CreateSubtitleAudioTask(questionAudio, answerAudios, response);
                
                isCompleted = true;
                
                var processingTime = Time.realtimeSinceStartup - startTime;
                if (debugLog) Debug.Log($"{logPrefix} 自律処理完了: [{UserName}] 処理時間={processingTime:F3}秒");
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} 自律処理キャンセル: [{UserName}]");
                hasError = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 自律処理エラー: [{UserName}] {ex.Message}");
                hasError = true;
            }
        }

        /// <summary>
        /// 音声データのバリデーション
        /// </summary>
        /// <param name="questionAudio">質問音声</param>
        /// <param name="answerAudios">回答音声群</param>
        /// <param name="response">Difyレスポンス</param>
        /// <returns>バリデーション結果</returns>
        private bool ValidateAudioData(AudioClip? questionAudio, AudioClip[] answerAudios, DifyChunkedResponse response)
        {
            // 回答音声が1つもない場合はエラー
            if (answerAudios.Length == 0)
            {
                Debug.LogError($"{logPrefix} バリデーション失敗: 回答音声が0個 [{UserName}]");
                return false;
            }
            
            // 回答音声数とテキストチャンク数の整合性チェック
            if (answerAudios.Length != response.ChunkCount)
            {
                Debug.LogWarning($"{logPrefix} バリデーション警告: 音声数とチャンク数不一致 [{UserName}] 音声={answerAudios.Length}, チャンク={response.ChunkCount}");
            }
            
            // 各AudioClipの有効性チェック
            for (int i = 0; i < answerAudios.Length; i++)
            {
                if (!IsValidAudioClip(answerAudios[i]))
                {
                    Debug.LogError($"{logPrefix} バリデーション失敗: 無効な回答音声[{i}] [{UserName}]");
                    return false;
                }
            }
            
            // 質問音声の有効性チェック（ある場合のみ）
            if (questionAudio != null && !IsValidAudioClip(questionAudio))
            {
                Debug.LogError($"{logPrefix} バリデーション失敗: 無効な質問音声 [{UserName}]");
                return false;
            }
            
            if (debugLog) Debug.Log($"{logPrefix} バリデーション成功: [{UserName}] 質問={questionAudio != null}, 回答={answerAudios.Length}個");
            return true;
        }

        /// <summary>
        /// AudioClipの有効性をチェック
        /// </summary>
        /// <param name="clip">チェック対象のAudioClip</param>
        /// <returns>有効かどうか</returns>
        private bool IsValidAudioClip(AudioClip clip)
        {
            if (clip == null) return false;
            if (clip.length <= 0) return false;
            if (clip.samples <= 0) return false;
            if (clip.channels <= 0) return false;
            
            return true;
        }

        /// <summary>
        /// SubtitleAudioTaskを作成
        /// </summary>
        /// <param name="questionAudio">質問音声（現在は使用しない）</param>
        /// <param name="answerAudios">回答音声群</param>
        /// <param name="response">Difyレスポンス</param>
        /// <returns>作成されたSubtitleAudioTask</returns>
        private IEventNode CreateSubtitleAudioTask(AudioClip? questionAudio, AudioClip[] answerAudios, DifyChunkedResponse response)
        {
            if (debugLog) Debug.Log($"{logPrefix} SubtitleAudioTask作成: [{UserName}] 回答={answerAudios.Length}個");
            
            // SubtitleAudioTaskを作成（回答音声のみ、AudioSourceは後でAudioQueueManagerから設定）
            var subtitleAudioTask = new SubtitleAudioTask(
                comment: Comment,
                answerAudios: answerAudios,
                chunks: response.Chunks,
                userName: UserName,
                gapBetweenAudioSeconds: gapBetweenAudioSeconds,
                gapAfterAudioSeconds: gapAfterAudioSeconds,
                enableDebugLog: debugLog
            );
            
            if (debugLog) Debug.Log($"{logPrefix} SubtitleAudioTask作成完了: [{UserName}]");
            
            return subtitleAudioTask;
        }

        /// <summary>
        /// タスクの文字列表現
        /// </summary>
        /// <returns>タスク情報</returns>
        public override string ToString()
        {
            var status = IsCompleted ? "完了" : hasError ? "エラー" : IsTimedOut ? "タイムアウト" : "処理中";
            return $"[{UserName}:{Comment.data?.comment}...] {status}";
        }
    }
}