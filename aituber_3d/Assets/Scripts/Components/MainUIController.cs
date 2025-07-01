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
        private void UpdateWaitList()
        {
            for (var i = 0; i < mainUI.QueueCount; i++)
            {
                var iconUrl = i < _waitComments.Count ? _waitComments[i].Comment.data.profileImage : null;
                mainUI.SetQueuedIcon(iconUrl, i);
            }
            mainUI.SetQueuedQuestionCount(_waitComments.Count);
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
            mainUI.SetQuestionText(comment.data.comment);
            // 回答、スライド
            mainUI.SetAnswerText(response.TextResponse);
            mainUI.SetSlideImageUrl(response.SlideUrl);

            UpdateWaitList();

            _totalAnswerCount++;
            mainUI.SetTotalAnswerCount(_totalAnswerCount);
            PlayerPrefs.SetInt(Constants.PlayerPrefs.TotalAnswerCount, _totalAnswerCount);
        }

        void OnDestroy()
        {
            CleanupEventHandlers();
        }
        private void SetupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed += CommentHandler;
            AudioPlaybackNode.OnPlayStart += CommentPlay;
        }
        private void CleanupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed -= CommentHandler;
            AudioPlaybackNode.OnPlayStart -= CommentPlay;
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
}
