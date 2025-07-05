#nullable enable
using System.Collections.Generic;
using System.Linq;
using AiTuber.Dify;
using UnityEngine;

namespace AiTuber
{
    /// <summary>
    /// MainUIController - 新キューベースシステム専用UIコントローラー
    /// </summary>
    [RequireComponent(typeof(MainUI))]
    public class MainUIController : MonoBehaviour
    {
        private const string DefaultSlideUrl = "https://storage.googleapis.com/ai-anno-ai-anno-manifest-images-production/250704/slides/001_seisaku_manifesto.png";
        
        private MainUI mainUI = null!;
        private bool enableDebugLog = true;

        // データ構造（既存のMainChunkedCommentContextと同じ）
        private class MainChunkedCommentContext
        {
            public OneCommeComment Comment { get; set; } = null!;
            public string UserName { get; set; } = "";
            public DifyChunkedResponse? Response { get; set; }
            public SubtitleAudioTask? AudioTask { get; set; }
        }

        private List<MainChunkedCommentContext> waitQueue = new();
        private int totalAnswerCount = 0;

        private void Awake()
        {
            mainUI = GetComponent<MainUI>();
        }

        private void Start()
        {
            // PlayerPrefsから総回答数復元
            totalAnswerCount = PlayerPrefs.GetInt(Constants.PlayerPrefs.TotalAnswerCount, 0);
            InitializeUI();
        }

        /// <summary>
        /// UI初期化処理（既存のInitializeUIと等価）
        /// </summary>
        private void InitializeUI()
        {
            mainUI.SetTotalAnswerCount(totalAnswerCount);
            mainUI.SetQuestionerName("");
            mainUI.SetQuestionText("質問をお待ちしています...");
            mainUI.SetAnswerText("質問をお待ちしています...");
            mainUI.SetSlideImageUrl(DefaultSlideUrl);
            UpdateWaitList();
        }

        // ========== 新システムイベントハンドラー ==========

        /// <summary>
        /// 既存のChunkedCommentHandlerと等価
        /// </summary>
        public void HandleNewCommentQueued(OneCommeComment comment, string userName)
        {
            var context = new MainChunkedCommentContext
            {
                Comment = comment,
                UserName = userName,
                Response = null, // 後でDifyProcessingTask完了時に設定
                AudioTask = null // 後でSubtitleAudioTask作成時に設定
            };
            
            waitQueue.Add(context);
            UpdateWaitList();
            
            if (enableDebugLog)
            {
                Debug.Log($"[MainUIController] 待機キューに追加: [{userName}] {comment.data.comment}");
            }
        }

        /// <summary>
        /// DifyProcessingTask完了時のコンテキスト更新
        /// </summary>
        public void HandleProcessingCompleted(DifyProcessingTask task, DifyChunkedResponse response)
        {
            // 待機キューから該当コメントを検索してレスポンス設定
            var context = waitQueue.Find(c => c.Comment == task.Comment);
            if (context != null)
            {
                context.Response = response;
            }
            
            if (enableDebugLog)
            {
                Debug.Log($"[MainUIController] Dify処理完了: [{task.UserName}]");
            }
        }

        /// <summary>
        /// 既存のChunkedCommentPlayと等価
        /// </summary>
        public void HandleSubtitleTaskPlayStart(SubtitleAudioTask task)
        {
            // 待機キューから該当コメントを検索（publicプロパティアクセス）
            var context = waitQueue.Find(c => c.Comment == task.Comment);
            if (context == null) return;
            
            // 待機キューから削除
            waitQueue.Remove(context);
            
            // UI更新（既存システムと同じ順序・内容）
            var comment = context.Comment;
            var response = context.Response;
            
            mainUI.SetQuestionerName(comment.data?.displayName ?? context.UserName ?? "匿名");
            mainUI.SetQuestionerIconUrl(comment.data?.profileImage);
            mainUI.SetQuestionText(comment.data?.speechText ?? comment.data?.comment ?? "");
            mainUI.SetAnswerText(""); // 初期化（字幕で更新される）
            
            // スライド画像設定
            string slideUrl = response?.SiteUrl ?? DefaultSlideUrl;
            mainUI.SetSlideImageUrl(slideUrl);
            
            // 総回答数更新（既存システムと同じタイミング）
            totalAnswerCount++;
            mainUI.SetTotalAnswerCount(totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, totalAnswerCount);
            
            // 待機リスト更新
            UpdateWaitList();
            
            if (enableDebugLog)
            {
                Debug.Log($"[MainUIController] 音声再生開始: [{context.UserName}] 総回答数:{totalAnswerCount}");
            }
        }

        /// <summary>
        /// 既存のHandleChunkStartedと等価
        /// </summary>
        public void HandleChunkStarted(string chunkText)
        {
            // 字幕表示（既存システムと同じ）
            mainUI.SetAnswerText(chunkText);
            
            if (enableDebugLog)
            {
                Debug.Log($"[MainUIController] 字幕更新: {chunkText}");
            }
        }

        /// <summary>
        /// 既存のHandleChainCompletedと等価
        /// </summary>
        public void HandleSubtitleTaskCompleted(SubtitleAudioTask task)
        {
            // UI状態をデフォルトに復帰（既存システムと同じ）
            mainUI.SetSlideImageUrl(DefaultSlideUrl);
            mainUI.SetQuestionerName("");
            mainUI.SetQuestionText("質問をお待ちしています...");
            mainUI.SetAnswerText("質問をお待ちしています...");
            mainUI.SetQuestionerIconUrl(null);
            
            if (enableDebugLog)
            {
                Debug.Log($"[MainUIController] 音声再生完了・UI復帰: [{task.UserName}]");
            }
        }

        // ========== 内部処理メソッド ==========

        /// <summary>
        /// 既存のUpdateWaitListと等価
        /// </summary>
        private void UpdateWaitList()
        {
            // 待機アイコン更新
            for (var i = 0; i < mainUI.QueueCount; i++)
            {
                string? iconUrl = null;
                
                if (i < waitQueue.Count)
                {
                    iconUrl = waitQueue[i].Comment.data?.profileImage;
                }
                
                mainUI.SetQueuedIcon(iconUrl, i);
            }
            
            // 待機数更新
            mainUI.SetQueuedQuestionCount(waitQueue.Count);
        }

        /// <summary>
        /// 総回答数リセット機能（既存システムと同じ）
        /// </summary>
        public void ResetAnswerCount()
        {
            totalAnswerCount = 0;
            mainUI.SetTotalAnswerCount(totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, totalAnswerCount);
            
            if (enableDebugLog)
            {
                Debug.Log("[MainUIController] 総回答数リセット");
            }
        }
    }
}