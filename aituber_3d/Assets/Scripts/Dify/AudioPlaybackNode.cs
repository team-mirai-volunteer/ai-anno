#nullable enable
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// コメント処理ノード - 自己参照連鎖リストによる音声再生パイプライン
    /// </summary>
    public class AudioPlaybackNode
    {
        public OneCommeComment Comment { get; }
        public byte[]? AudioData { get; }
        public string UserName { get; }
        public float Gap { get; }
        public AudioPlaybackNode? Next { get; set; }
        
        /// <summary>
        /// チェーン完了通知イベント
        /// </summary>
        public static event Action<AudioPlaybackNode>? OnChainCompleted;
        
        private readonly AudioPlayer audioPlayer;
        private readonly bool debugLog;
        private readonly string logPrefix = "[AudioPlaybackNode]";

        /// <summary>
        /// AudioPlaybackNodeを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="audioData">音声データ</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="gap">次のコメントまでのギャップ（秒）</param>
        /// <param name="audioPlayer">音声再生プレイヤー</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public AudioPlaybackNode(OneCommeComment comment, byte[]? audioData, string userName, float gap, AudioPlayer audioPlayer, bool enableDebugLog = false)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            AudioData = audioData;
            UserName = userName ?? "匿名";
            Gap = gap;
            this.audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
            debugLog = enableDebugLog;
            
            // AudioPlaybackNodeカウント増加
            NodeChainController.IncrementAudioPlaybackNodeCount();
        }

        /// <summary>
        /// このノードを処理し、完了後に次のノードに継続
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async void ProcessAndContinue(CancellationToken cancellationToken = default)
        {
            AudioPlaybackNode? nextNode = null;
            
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} ノード処理開始: {Comment.data?.comment}");
                
                // 1. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();
                
                // 2. 音声再生
                if (AudioData != null && AudioData.Length > 0)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 音声再生開始: {AudioData.Length} bytes");
                    await audioPlayer.PlayAudioFromDataAsync(AudioData).AttachExternalCancellation(cancellationToken);
                    if (debugLog) Debug.Log($"{logPrefix} 音声再生完了");
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} 音声データなし");
                }
                
                // 3. ギャップ待機
                if (Gap > 0)
                {
                    if (debugLog) Debug.Log($"{logPrefix} ギャップ待機開始: {Gap}秒");
                    await UniTask.Delay(TimeSpan.FromSeconds(Gap), cancellationToken: cancellationToken);
                    if (debugLog) Debug.Log($"{logPrefix} ギャップ待機完了");
                }
            }
            catch (OperationCanceledException)
            {
                if (debugLog) Debug.Log($"{logPrefix} ノード処理キャンセル");
                return; // チェーン中断
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} ノード処理エラー: {ex.Message}");
            }
            finally
            {
                // 3. AudioPlaybackNodeカウント減少
                NodeChainController.DecrementAudioPlaybackNodeCount();
                
                // 4. 次のノードへ継続またはチェーン終了通知
                nextNode = Next;
                Next = null; // 参照切断（GC対象化）
                
                // キャンセル状態でない場合のみ次のノードに継続
                if (nextNode != null && !cancellationToken.IsCancellationRequested)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 次のノードに継続");
                    nextNode.ProcessAndContinue(cancellationToken);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} チェーン終了{(cancellationToken.IsCancellationRequested ? "（キャンセル）" : "")}");
                    OnChainCompleted?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// ノード情報を文字列として取得（デバッグ用）
        /// </summary>
        /// <returns>ノード情報</returns>
        public override string ToString()
        {
            var hasAudio = AudioData != null && AudioData.Length > 0 ? "Audio" : "NoAudio";
            var hasNext = Next != null ? "→Next" : "End";
            return $"[{UserName}:{Comment.data?.comment?.Substring(0, Math.Min(10, Comment.data?.comment?.Length ?? 0))}...] {hasAudio} {hasNext}";
        }
    }
}