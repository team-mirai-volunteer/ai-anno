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

        void Start()
        {
            mainUI = GetComponent<MainUI>();
            SetupEventHandlers();

            // 初期化処理
            InitializeUI();
        }

        void InitializeUI()
        {
            mainUI.SetQuestionerName("");
            mainUI.SetQuestionText("質問をお待ちしています...");
            mainUI.SetAnswerText("質問をお待ちしています...");
        }

        void Update()
        {
            var (activeDifyProcessingNodeCount, activeAudioPlaybackNodeCount) = NodeChainController.GetCurrentNodeCounts();
            // 受付待ちは AudioPlaybackNode の数にしておく（DifyProcessingNode は失敗することもあるので）
            mainUI.SetQueuedQuestionCount(activeAudioPlaybackNodeCount);
        }

        private void CommentHandler(MainCommentContext context)
        {
            var comment = context.Comment;
            var response = context.Response;

            // コメント再生時の処理
            // 質問、質問者名、質問者アイコン
            mainUI.SetQuestionerName(comment.data.displayName ?? "匿名");
            mainUI.SetQuestionerIconUrl(comment.data.iconUrl);
            mainUI.SetQuestionText(comment.data.comment);
            // 回答、スライド
            mainUI.SetAnswerText(response.TextResponse);
            mainUI.SetSlideImageUrl(response.SlideUrl);
        }

        void OnDestroy()
        {
            CleanupEventHandlers();
        }
        private void SetupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed += CommentHandler;
        }
        private void CleanupEventHandlers()
        {
            DifyProcessingNode.OnCommentProcessed -= CommentHandler;
        }
    }

    public class MainCommentContext
    {
        public OneCommeComment Comment { get; set; }
        public DifyBlockingResponse Response { get; set; }

        public MainCommentContext(OneCommeComment comment, DifyBlockingResponse response)
        {
            Comment = comment;
            Response = response;
        }
    }
}
