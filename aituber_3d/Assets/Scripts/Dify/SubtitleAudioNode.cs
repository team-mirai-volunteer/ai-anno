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
    /// 字幕音声再生ノード - 自己参照連鎖リストによる字幕付き音声再生パイプライン
    /// </summary>
    public class SubtitleAudioNode
    {
        public OneCommeComment Comment { get; }
        public List<DifyChunk> Chunks { get; }
        public string UserName { get; }
        public float Gap { get; }
        public SubtitleAudioNode? Next { get; set; }

        public static event Action<SubtitleAudioNode>? OnPlayStart;

        /// <summary>
        /// チャンク開始イベント（字幕表示用）
        /// </summary>
        public static event Action<string>? OnChunkStarted;

        /// <summary>
        /// チェーン完了通知イベント
        /// </summary>
        public static event Action<SubtitleAudioNode>? OnChainCompleted;

        private readonly BufferedAudioPlayer bufferedAudioPlayer;
        private readonly bool debugLog;
        private readonly string logPrefix = "[SubtitleAudioNode]";

        /// <summary>
        /// SubtitleAudioNodeを作成
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <param name="chunks">音声・字幕チャンクリスト</param>
        /// <param name="userName">ユーザー名</param>
        /// <param name="gap">次のコメントまでのギャップ（秒）</param>
        /// <param name="bufferedAudioPlayer">バッファード音声プレイヤー</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public SubtitleAudioNode(OneCommeComment comment, List<DifyChunk> chunks, string userName, float gap, BufferedAudioPlayer bufferedAudioPlayer, bool enableDebugLog = false)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            Chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
            UserName = userName ?? "匿名";
            Gap = gap;
            this.bufferedAudioPlayer = bufferedAudioPlayer ?? throw new ArgumentNullException(nameof(bufferedAudioPlayer));
            debugLog = enableDebugLog;

            // BufferedAudioPlayerのチャンクイベントを購読
            this.bufferedAudioPlayer.OnChunkStarted += text => OnChunkStarted?.Invoke(text);

            // AudioPlaybackNodeカウント増加（既存のカウンターを流用）
            NodeChainController.IncrementAudioPlaybackNodeCount();
        }

        /// <summary>
        /// このノードを処理し、完了後に次のノードに継続
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        public async void ProcessAndContinue(CancellationToken cancellationToken = default)
        {
            SubtitleAudioNode? nextNode = null;

            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生開始: [{UserName}] チャンク数={Chunks.Count}");

                // 1. キャンセルチェック
                cancellationToken.ThrowIfCancellationRequested();

                // 2. 字幕付き音声再生
                if (Chunks.Count > 0)
                {
                    // 音声URLとテキストを抽出
                    var audioUrls = Chunks.Where(chunk => chunk.HasAudioUrl).Select(chunk => chunk.AudioUrl!).ToList();
                    var textChunks = Chunks.Select(chunk => chunk.Text).Where(text => !string.IsNullOrEmpty(text)).ToList();

                    if (audioUrls.Count > 0)
                    {
                        if (debugLog) Debug.Log($"{logPrefix} バッファード音声再生開始: 音声={audioUrls.Count}, 字幕={textChunks.Count}");
                        OnPlayStart?.Invoke(this); // 再生開始イベント通知

                        // 字幕付きバッファード再生
                        if (textChunks.Count > 0)
                        {
                            await bufferedAudioPlayer.PlayBufferedAsync(audioUrls, textChunks, cancellationToken);
                        }
                        else
                        {
                            await bufferedAudioPlayer.PlayBufferedAsync(audioUrls, cancellationToken);
                        }

                        if (debugLog) Debug.Log($"{logPrefix} バッファード音声再生完了");
                    }
                    else
                    {
                        if (debugLog) Debug.Log($"{logPrefix} 有効な音声URLなし");
                    }
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} チャンクデータなし");
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
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生キャンセル: [{UserName}]");
                return; // チェーン中断
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 字幕音声再生エラー: [{UserName}] {ex.Message}");
            }
            finally
            {
                // 4. AudioPlaybackNodeカウント減少
                NodeChainController.DecrementAudioPlaybackNodeCount();

                // 5. 次のノードへ継続またはチェーン終了通知
                nextNode = Next;
                Next = null; // 参照切断（GC対象化）

                // キャンセル状態でない場合のみ次のノードに継続
                if (nextNode != null && !cancellationToken.IsCancellationRequested)
                {
                    if (debugLog) Debug.Log($"{logPrefix} 次の字幕音声ノードに継続");
                    nextNode.ProcessAndContinue(cancellationToken);
                }
                else
                {
                    if (debugLog) Debug.Log($"{logPrefix} 字幕音声チェーン終了{(cancellationToken.IsCancellationRequested ? "（キャンセル）" : "")}");
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
            var hasChunks = Chunks.Count > 0 ? $"{Chunks.Count}Chunks" : "NoChunks";
            var hasNext = Next != null ? "→Next" : "End";
            return $"[{UserName}:{Comment.data?.comment?.Substring(0, Math.Min(10, Comment.data?.comment?.Length ?? 0))}...] {hasChunks} {hasNext}";
        }
    }
}