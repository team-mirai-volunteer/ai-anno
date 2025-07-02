using System.Collections.Generic;
using AiTuber.Dify;
using UnityEngine;

namespace AiTuber
{
    /// <summary>
    /// MainUIController - MainUIのコントローラー
    /// </summary>
    [RequireComponent(typeof(MainUI))]
    public class MainUIController : MonoBehaviour
    {
        private const string DefaultSlideUrl = "https://storage.googleapis.com/ai-anno-ai-anno-manifest-images-staging/250701/slides/003_introduction.png";
        
        private MainUI mainUI;

        private int _totalAnswerCount = 0;

        private List<MainChunkedCommentContext> _waitChunkedComments = new();

        void Start()
        {
            mainUI = GetComponent<MainUI>();
            _totalAnswerCount = PlayerPrefs.GetInt(Constants.PlayerPrefs.TotalAnswerCount, 0);
            SetupEventHandlers();

            // 初期化処理
            InitializeUI();
        }

        void InitializeUI()
        {
            mainUI.SetTotalAnswerCount(_totalAnswerCount);
            mainUI.SetQuestionerName("");
            mainUI.SetQuestionText("質問をお待ちしています...");
            mainUI.SetAnswerText("質問をお待ちしています...");
            mainUI.SetSlideImageUrl(DefaultSlideUrl);
            UpdateWaitList();
        }


        private void ChunkedCommentHandler(MainChunkedCommentContext context)
        {
            _waitChunkedComments.Add(context);
            UpdateWaitList();
        }
        private void UpdateWaitList()
        {
            for (var i = 0; i < mainUI.QueueCount; i++)
            {
                string iconUrl = null;
                
                if (i < _waitChunkedComments.Count)
                {
                    iconUrl = _waitChunkedComments[i].Comment.data?.profileImage;
                }
                
                mainUI.SetQueuedIcon(iconUrl, i);
            }
            mainUI.SetQueuedQuestionCount(_waitChunkedComments.Count);
        }


        private void ChunkedCommentPlay(SubtitleAudioNode subtitleAudioNode)
        {
            MainChunkedCommentContext? commentContext = null;
            foreach (var context in _waitChunkedComments)
            {
                if (context.AudioNode == subtitleAudioNode)
                {
                    commentContext = context;
                    break;
                }
            }

            if (commentContext == null)
            {
                return;
            }

            _waitChunkedComments.Remove(commentContext);

            var comment = commentContext.Comment;
            var response = commentContext.Response;

            // chunkedコメント再生時の処理
            // 質問、質問者名、質問者アイコン
            mainUI.SetQuestionerName(comment.data?.displayName ?? "匿名");
            mainUI.SetQuestionerIconUrl(comment.data?.profileImage);
            mainUI.SetQuestionText(comment.data?.speechText ?? "");
            // 回答は空（字幕で表示）、スライドURL設定
            mainUI.SetAnswerText("");
            var slideUrl = string.IsNullOrEmpty(response.SiteUrl) 
                ? DefaultSlideUrl
                : response.SiteUrl;
            
            Debug.Log($"[MainUIController] Setting slide URL: {slideUrl} (Original: {response.SiteUrl ?? "null"})");
            mainUI.SetSlideImageUrl(slideUrl);

            UpdateWaitList();

            _totalAnswerCount++;
            mainUI.SetTotalAnswerCount(_totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, _totalAnswerCount);
        }

        private void HandleChunkStarted(string chunkText)
        {
            mainUI.SetAnswerText(chunkText);
        }

        /// <summary>
        /// 音声再生チェーン完了時の処理 - デフォルト画像とUI状態に復帰
        /// </summary>
        /// <param name="completedNode">完了したノード</param>
        private void HandleChainCompleted(SubtitleAudioNode completedNode)
        {
            // デフォルト画像に復帰
            mainUI.SetSlideImageUrl(DefaultSlideUrl);
            
            // UI状態をデフォルトに復帰
            mainUI.SetQuestionerName("");
            mainUI.SetQuestionText("質問をお待ちしています...");
            mainUI.SetAnswerText("質問をお待ちしています...");
            mainUI.SetQuestionerIconUrl(null);
            
            Debug.Log("[MainUIController] 字幕音声チェーン完了 - UI状態をデフォルトに復帰");
        }

        void OnDestroy()
        {
            CleanupEventHandlers();
        }
        private void SetupEventHandlers()
        {
            DifyProcessingChunkedNode.OnCommentProcessed += ChunkedCommentHandler;
            SubtitleAudioNode.OnPlayStart += ChunkedCommentPlay;
            SubtitleAudioNode.OnChunkStarted += HandleChunkStarted;
            SubtitleAudioNode.OnChainCompleted += HandleChainCompleted;
        }
        private void CleanupEventHandlers()
        {
            DifyProcessingChunkedNode.OnCommentProcessed -= ChunkedCommentHandler;
            SubtitleAudioNode.OnPlayStart -= ChunkedCommentPlay;
            SubtitleAudioNode.OnChunkStarted -= HandleChunkStarted;
            SubtitleAudioNode.OnChainCompleted -= HandleChainCompleted;
        }
    }


    public class MainChunkedCommentContext
    {
        public OneCommeComment Comment { get; set; }
        public DifyChunkedResponse Response { get; set; }
        public SubtitleAudioNode AudioNode { get; set; }

        public MainChunkedCommentContext(OneCommeComment comment, DifyChunkedResponse response, SubtitleAudioNode audioNode)
        {
            Comment = comment;
            Response = response;
            AudioNode = audioNode;
        }
    }
}
