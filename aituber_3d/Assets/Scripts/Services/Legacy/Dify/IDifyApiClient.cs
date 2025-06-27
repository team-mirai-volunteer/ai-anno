using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Legacy.Dify.Data;

namespace AiTuber.Services.Legacy.Dify
{
    /// <summary>
    /// Dify Chat Messages API クライアントのインターフェース
    /// TDD実装、Pure C#なので依存注入・モックによるテスト可能
    /// </summary>
    public interface IDifyApiClient
    {
        /// <summary>
        /// API キー設定
        /// </summary>
        string ApiKey { get; set; }
        
        /// <summary>
        /// Dify API エンドポイント URL
        /// </summary>
        string ApiUrl { get; set; }
        
        
        /// <summary>
        /// Dify Chat Messages API にストリーミングリクエストを送信
        /// Server-Sent Events (SSE) でレスポンスを受信し、イベントごとにコールバック実行
        /// </summary>
        /// <param name="request">リクエストデータ</param>
        /// <param name="onEventReceived">SSEイベント受信時のコールバック</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>処理結果（会話ID、メッセージID、エラー情報等）</returns>
        /// <exception cref="ArgumentNullException">request が null の場合</exception>
        /// <exception cref="ArgumentException">request が無効な場合</exception>
        /// <exception cref="InvalidOperationException">API設定が不正な場合</exception>
        /// <exception cref="HttpRequestException">HTTP通信エラーの場合</exception>
        /// <exception cref="TaskCanceledException">タイムアウト・キャンセルされた場合</exception>
        Task<DifyProcessingResult> SendStreamingRequestAsync(
            DifyApiRequest request,
            Action<DifyStreamEvent> onEventReceived,
            CancellationToken cancellationToken = default);
        
        
        /// <summary>
        /// API設定の妥当性チェック
        /// </summary>
        /// <returns>設定が有効であれば true、無効であれば false</returns>
        bool IsConfigurationValid();
        
        /// <summary>
        /// API接続テスト（軽量なヘルスチェック用）
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>接続成功であれば true、失敗であれば false</returns>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}