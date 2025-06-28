using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Application.Ports;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Infrastructure.Http;

#nullable enable

namespace AiTuber.Services.Dify.Application.UseCases
{
    /// <summary>
    /// Difyクエリ処理ユースケース
    /// Pure C# Application Layer、Clean Architecture準拠
    /// Legacy DifyServiceからリファクタリング済み
    /// </summary>
    public class ProcessQueryUseCase : IProcessQueryUseCase
    {
        private readonly IDifyStreamingPort _streamingPort;
        private readonly IResponseProcessor _responseProcessor;

        /// <summary>
        /// ProcessQueryUseCaseを作成
        /// </summary>
        /// <param name="streamingPort">ストリーミング通信ポート</param>
        /// <param name="responseProcessor">レスポンス処理サービス</param>
        /// <exception cref="ArgumentNullException">依存関係がnullの場合</exception>
        public ProcessQueryUseCase(
            IDifyStreamingPort streamingPort,
            IResponseProcessor responseProcessor)
        {
            _streamingPort = streamingPort ?? throw new ArgumentNullException(nameof(streamingPort));
            _responseProcessor = responseProcessor ?? throw new ArgumentNullException(nameof(responseProcessor));
        }

        /// <summary>
        /// クエリを実行してレスポンスを取得
        /// </summary>
        /// <param name="request">Difyリクエスト</param>
        /// <param name="onEventReceived">ストリーミングイベント受信コールバック</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>クエリレスポンス</returns>
        /// <exception cref="ArgumentNullException">リクエストがnullの場合</exception>
        /// <exception cref="ArgumentException">リクエストが無効な場合</exception>
        public async Task<QueryResponse> ExecuteAsync(
            DifyRequest request,
            Action<DifyStreamEvent>? onEventReceived = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidateRequest(request);

            try
            {
                // ストリーミングイベント処理付きでクエリ実行
                var response = await _streamingPort.ExecuteStreamingAsync(
                    request,
                    onEventReceived: evt => ProcessStreamEvent(evt, onEventReceived),
                    cancellationToken: cancellationToken);

                return response;
            }
            catch (OperationCanceledException)
            {
                throw; // キャンセルは再スロー
            }
            catch (Exception ex)
            {
                return QueryResponse.CreateError(ex.Message);
            }
        }

        /// <summary>
        /// リクエストの基本バリデーション
        /// </summary>
        /// <param name="request">検証対象のリクエスト</param>
        /// <exception cref="ArgumentException">リクエストが無効な場合</exception>
        public void ValidateRequest(DifyRequest request)
        {
            if (!request.IsValid())
                throw new ArgumentException("Invalid request: Query and User are required", nameof(request));
        }

        /// <summary>
        /// ストリーミングイベントを処理
        /// </summary>
        /// <param name="streamEvent">受信したストリームイベント</param>
        /// <param name="onEventReceived">外部コールバック</param>
        private void ProcessStreamEvent(DifyStreamEvent streamEvent, Action<DifyStreamEvent>? onEventReceived)
        {
            if (streamEvent == null) return;

            // イベントタイプ別処理
            if (streamEvent.IsAudioEvent)
            {
                _responseProcessor.ProcessAudioEvent(streamEvent);
            }
            else if (streamEvent.IsMessageEvent)
            {
                _responseProcessor.ProcessTextEvent(streamEvent);
            }

            // 外部コールバック呼び出し
            onEventReceived?.Invoke(streamEvent);
        }

        /// <summary>
        /// 設定情報を取得
        /// </summary>
        /// <returns>Dify設定</returns>
        public DifyConfiguration GetConfiguration()
        {
            return _streamingPort.GetConfiguration();
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>接続成功フラグ</returns>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            return await _streamingPort.TestConnectionAsync(cancellationToken);
        }
    }
}