using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace AiTuber.Services.Dify.Infrastructure.Http
{
    /// <summary>
    /// HTTP通信インターフェース
    /// Infrastructure層 Clean Architecture準拠
    /// Push型ストリーミング対応
    /// </summary>
    public interface IHttpClient
    {
        /// <summary>
        /// ストリーミングリクエストを送信
        /// Server-Sent Events (SSE) 対応
        /// </summary>
        /// <param name="request">HTTPリクエスト</param>
        /// <param name="onDataReceived">データ受信時のコールバック（Push型）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>HTTPレスポンス</returns>
        Task<HttpResponse> SendStreamingRequestAsync(
            HttpRequest request, 
            Action<string>? onDataReceived, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="url">テスト対象URL</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>接続成功フラグ</returns>
        Task<bool> TestConnectionAsync(
            string url, 
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// HTTPリクエストデータ
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// リクエストURL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// HTTPメソッド
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// リクエストボディ
        /// </summary>
        public string? Body { get; }

        /// <summary>
        /// HTTPヘッダー
        /// </summary>
        public System.Collections.Generic.Dictionary<string, string> Headers { get; }

        /// <summary>
        /// HTTPリクエストを作成
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="method">HTTPメソッド</param>
        /// <param name="body">リクエストボディ</param>
        /// <param name="headers">HTTPヘッダー</param>
        public HttpRequest(
            string url, 
            string method = "POST", 
            string? body = null, 
            System.Collections.Generic.Dictionary<string, string>? headers = null)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Body = body;
            Headers = headers ?? new System.Collections.Generic.Dictionary<string, string>();
        }
    }

    /// <summary>
    /// HTTPレスポンスデータ
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// 成功フラグ
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// レスポンスボディ
        /// </summary>
        public string ResponseBody { get; }

        /// <summary>
        /// HTTPレスポンスを作成
        /// </summary>
        /// <param name="isSuccess">成功フラグ</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="responseBody">レスポンスボディ</param>
        public HttpResponse(bool isSuccess, string errorMessage, string responseBody)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage ?? "";
            ResponseBody = responseBody ?? "";
        }
    }
}