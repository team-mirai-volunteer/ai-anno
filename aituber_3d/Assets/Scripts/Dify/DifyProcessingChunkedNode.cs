#nullable enable
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// Difyチャンク処理ノード - 自己参照連鎖リストによるAI処理パイプライン（チャンク対応）
    /// </summary>
    public class DifyProcessingChunkedNode
    {
        public OneCommeComment Comment { get; }
        public string UserName { get; }
        public DifyProcessingChunkedNode? Next { get; set; }

        /// <summary>
        /// Difyチャンク処理チェーン完了通知イベント
        /// </summary>
        public static event Action<DifyProcessingChunkedNode>? OnDifyProcessingChainCompleted;

        /// <summary>
        /// SubtitleAudioNode作成完了イベント
        /// </summary>
        // public static event Action<SubtitleAudioNode>? OnSubtitleAudioNodeCreated;

        /// <summary>
        /// コメント処理完了イベント
        /// </summary>
        public static event Action<MainCommentContext>? OnCommentProcessed;

        private readonly DifyChunkedClient difyClient;
        private readonly float difyGap;
        private readonly bool debugLog;
        private readonly string logPrefix = "[DifyProcessingChunkedNode]";

        /// <summary>
        /// DifyProcessingChunkedNodeを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="difyClient">Difyチャンククライアント</param>
        /// <param name="difyGap">Dify API呼び出し間隔</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public DifyProcessingChunkedNode(OneCommeComment comment, string userName, DifyChunkedClient difyClient, float difyGap, bool enableDebugLog = false)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            UserName = userName ?? "匿名";
            this.difyClient = difyClient ?? throw new ArgumentNullException(nameof(difyClient));
            this.difyGap = difyGap;
            debugLog = enableDebugLog;

            // DifyProcessingNodeカウント増加（既存のカウンターを流用）
            NodeChainController.IncrementDifyProcessingNodeCount();
        }

        /// <summary>
        /// このノードを処理し、完了後に次のノードに継続
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async void ProcessAndContinue(CancellationToken cancellationToken = default)
        {
            DifyProcessingChunkedNode? nextNode = null;

            try
            {
                if (debugLog) Debug.Log($"{logPrefix} チャンク処理開始: [{UserName}] {Comment.data?.comment}");

                // 1. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();

                // 2. Difyチャンク処理
                var commentText = Comment.data?.comment ?? "";
                var response = await difyClient.SendQueryAsync(commentText, UserName);

                // 3. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();

                // 4. 成功時はSubtitleAudioNode作成
                if (response.IsSuccess && response.ChunkCount > 0)
                {
                    // TODO: SubtitleAudioNode実装後に有効化
                    /*
                    var subtitleAudioNode = new SubtitleAudioNode(
                        Comment,
                        response.Chunks,
                        UserName,
                        debugLog
                    );

                    if (debugLog) Debug.Log($"{logPrefix} SubtitleAudioNode作成完了: [{UserName}] チャンク数={response.ChunkCount}");
                    OnSubtitleAudioNodeCreated?.Invoke(subtitleAudioNode);
                    */

                    if (debugLog) Debug.Log($"{logPrefix} Difyチャンク処理成功: [{UserName}] チャンク数={response.ChunkCount}");

                    // TODO: MainCommentContextをチャンク対応にする必要があるかもしれない
                    // var commentContext = new MainCommentContext(Comment, response);
                    // OnCommentProcessed?.Invoke(commentContext);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} Difyチャンク処理失敗 - スキップ: [{UserName}] {response.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} チャンク処理キャンセル: [{UserName}]");
                return; // チェーン中断
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} チャンク処理エラー - スキップ: [{UserName}] {ex.Message}");
                // エラーでもスキップして次に継続
            }
            finally
            {
                // 5. DifyProcessingNodeカウント減少
                NodeChainController.DecrementDifyProcessingNodeCount();

                // 6. 次のノードへ継続またはチェーン終了通知
                nextNode = Next;
                Next = null; // 参照切断（GC対象化）

                // キャンセル状態でない場合のみ次のノードに継続
                if (nextNode != null && !cancellationToken.IsCancellationRequested)
                {
                    // Dify API呼び出し間隔制御
                    if (difyGap > 0)
                    {
                        if (debugLog) Debug.Log($"{logPrefix} Dify API間隔待機開始: {difyGap}秒");
                        await UniTask.Delay(TimeSpan.FromSeconds(difyGap), cancellationToken: cancellationToken);
                        if (debugLog) Debug.Log($"{logPrefix} Dify API間隔待機完了");
                    }
                    
                    if (debugLog) Debug.Log($"{logPrefix} 次のチャンク処理ノードに継続");
                    nextNode.ProcessAndContinue(cancellationToken);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} チャンク処理チェーン終了{(cancellationToken.IsCancellationRequested ? "（キャンセル）" : "")}");
                    OnDifyProcessingChainCompleted?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// ノード情報を文字列として取得（デバッグ用）
        /// </summary>
        /// <returns>ノード情報</returns>
        public override string ToString()
        {
            var hasNext = Next != null ? "→Next" : "End";
            return $"[{UserName}:{Comment.data?.comment?.Substring(0, Math.Min(10, Comment.data?.comment?.Length ?? 0))}...] {hasNext}";
        }
    }
}