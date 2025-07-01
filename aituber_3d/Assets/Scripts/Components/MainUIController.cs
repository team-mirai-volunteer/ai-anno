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
        private MainUI mainUI;

        private int _totalAnswerCount = 0;

        private List<MainCommentContext> _waitComments = new();
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
            mainUI.SetSlideImageUrl("https://storage.googleapis.com/ai-anno-ai-anno-manifest-images-staging/250701/slides/003_introduction.png");
            UpdateWaitList();
        }

        private void CommentHandler(MainCommentContext context)
        {
            _waitComments.Add(context);
            UpdateWaitList();
        }

        private void ChunkedCommentHandler(MainChunkedCommentContext context)
        {
            _waitChunkedComments.Add(context);
            UpdateWaitList();
        }
        private void UpdateWaitList()
        {
            var totalWaitCount = _waitComments.Count + _waitChunkedComments.Count;
            
            for (var i = 0; i < mainUI.QueueCount; i++)
            {
                string iconUrl = null;
                
                if (i < _waitComments.Count)
                {
                    iconUrl = _waitComments[i].Comment.data.profileImage;
                }
                else if (i - _waitComments.Count < _waitChunkedComments.Count)
                {
                    iconUrl = _waitChunkedComments[i - _waitComments.Count].Comment.data?.profileImage;
                }
                
                mainUI.SetQueuedIcon(iconUrl, i);
            }
            mainUI.SetQueuedQuestionCount(totalWaitCount);
        }

        private void CommentPlay(AudioPlaybackNode commentNode)
        {
            MainCommentContext? commentContext = null;
            foreach (var context in _waitComments)
            {
                if (context.AudioNode == commentNode)
                {
                    commentContext = context;
                    break;
                }
            }

            if (commentContext == null)
            {
                Debug.LogWarning($"CommentPlay: No matching comment context found for node: {commentNode.Comment.data.comment}");
                return;
            }

            _waitComments.Remove(commentContext);

            var comment = commentContext.Comment;
            var response = commentContext.Response;

            // コメント再生時の処理
            // 質問、質問者名、質問者アイコン
            mainUI.SetQuestionerName(comment.data.displayName ?? "匿名");
            mainUI.SetQuestionerIconUrl(comment.data.profileImage);
            mainUI.SetQuestionText(comment.data.speechText);
            // 回答、スライド
            mainUI.SetAnswerText(response.TextResponse);
            mainUI.SetSlideImageUrl(response.SlideUrl);

            UpdateWaitList();

            _totalAnswerCount++;
            mainUI.SetTotalAnswerCount(_totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, _totalAnswerCount);
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
            mainUI.SetSlideImageUrl(response.SiteUrl ?? "");

            UpdateWaitList();

            _totalAnswerCount++;
            mainUI.SetTotalAnswerCount(_totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, _totalAnswerCount);
        }

        private void HandleChunkStarted(string chunkText)
        {
            mainUI.SetAnswerText(chunkText);
        }

        void OnDestroy()
        {
            CleanupEventHandlers();
        }
        private void SetupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed += CommentHandler;
            DifyProcessingChunkedNode.OnCommentProcessed += ChunkedCommentHandler;
            AudioPlaybackNode.OnPlayStart += CommentPlay;
            SubtitleAudioNode.OnPlayStart += ChunkedCommentPlay;
            SubtitleAudioNode.OnChunkStarted += HandleChunkStarted;
        }
        private void CleanupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed -= CommentHandler;
            DifyProcessingChunkedNode.OnCommentProcessed -= ChunkedCommentHandler;
            AudioPlaybackNode.OnPlayStart -= CommentPlay;
            SubtitleAudioNode.OnPlayStart -= ChunkedCommentPlay;
            SubtitleAudioNode.OnChunkStarted -= HandleChunkStarted;
        }
    }

    public class MainCommentContext
    {
        public OneCommeComment Comment { get; set; }
        public DifyBlockingResponse Response { get; set; }
        public AudioPlaybackNode AudioNode { get; set; }

        public MainCommentContext(OneCommeComment comment, DifyBlockingResponse response, AudioPlaybackNode audioNode)
        {
            Comment = comment;
            Response = response;
            AudioNode = audioNode;
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
