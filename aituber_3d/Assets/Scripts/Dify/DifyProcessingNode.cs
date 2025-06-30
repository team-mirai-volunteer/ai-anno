#nullable enable
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// Dify処理ノード - 自己参照連鎖リストによるAI処理パイプライン
    /// </summary>
    public class DifyProcessingNode
    {
        public OneCommeComment Comment { get; }
        public string UserName { get; }
        public DifyProcessingNode? Next { get; set; }
        
        /// <summary>
        /// Dify処理チェーン完了通知イベント
        /// </summary>
        public static event Action<DifyProcessingNode>? OnDifyProcessingChainCompleted;
        
        /// <summary>
        /// AudioPlaybackNode作成完了イベント
        /// </summary>
        public static event Action<AudioPlaybackNode>? OnAudioPlaybackNodeCreated;
        
        private readonly DifyClient difyClient;
        private readonly AudioPlayer audioPlayer;
        private readonly float gap;
        private readonly bool debugLog;
        private readonly string logPrefix = "[DifyProcessingNode]";

        /// <summary>
        /// DifyProcessingNodeを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="difyClient">Difyクライアント</param>
        /// <param name="audioPlayer">音声プレイヤー</param>
        /// <param name="gap">音声間のギャップ</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public DifyProcessingNode(OneCommeComment comment, string userName, DifyClient difyClient, AudioPlayer audioPlayer, float gap, bool enableDebugLog = false)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            UserName = userName ?? "匿名";
            this.difyClient = difyClient ?? throw new ArgumentNullException(nameof(difyClient));
            this.audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
            this.gap = gap;
            debugLog = enableDebugLog;
            
            // DifyProcessingNodeカウント増加
            NodeChainController.IncrementDifyProcessingNodeCount();
        }

        /// <summary>
        /// このノードを処理し、完了後に次のノードに継続
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async void ProcessAndContinue(CancellationToken cancellationToken = default)
        {
            DifyProcessingNode? nextNode = null;
            
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} ダウンロード処理開始: [{UserName}] {Comment.data?.comment}");
                
                // 1. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();
                
                // 2. Dify処理
                var commentText = Comment.data?.comment ?? "";
                var response = await difyClient.SendQueryAsync(commentText, UserName);
                
                // 3. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();
                
                // 4. 成功時はAudioPlaybackNode作成
                if (response.IsSuccess)
                {
                    var commentNode = new AudioPlaybackNode(
                        Comment,
                        response.AudioData,
                        UserName,
                        gap,
                        audioPlayer,
                        debugLog
                    );
                    
                    if (debugLog) Debug.Log($"{logPrefix} AudioPlaybackNode作成完了: [{UserName}]");
                    OnAudioPlaybackNodeCreated?.Invoke(commentNode);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} Dify処理失敗 - スキップ: [{UserName}] {response.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} ダウンロード処理キャンセル: [{UserName}]");
                return; // チェーン中断
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} ダウンロード処理エラー - スキップ: [{UserName}] {ex.Message}");
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
                    if (debugLog) Debug.Log($"{logPrefix} 次のダウンロードノードに継続");
                    nextNode.ProcessAndContinue(cancellationToken);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} ダウンロードチェーン終了{(cancellationToken.IsCancellationRequested ? "（キャンセル）" : "")}");
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