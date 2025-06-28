using UnityEngine;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Application.Ports;
using AiTuber.Services.Dify.Infrastructure.Http;
using AiTuber.Services.Dify.InterfaceAdapters.Translators;
using AiTuber.Services.Dify.Presentation.Controllers;
using AiTuber.Services.Dify.Mock;
using System;

#nullable enable

namespace AiTuber.Services.Dify.Presentation
{
    /// <summary>
    /// Unity標準DI - MonoBehaviour Installer
    /// Factory パターンに代わるUnityエコシステム準拠の依存注入
    /// Inspector設定によるMock/Real切り替えサポート
    /// </summary>
    public class DifyInstaller : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Dify API Configuration")]
        [SerializeField] private string _apiKey = "";
        [SerializeField] private string _apiUrl = "https://api.dify.ai/v1/chat-messages";
        [SerializeField] private bool _enableDebugLogging = true;
        [SerializeField] private bool _enableAudioProcessing = true;

        [Header("Testing Configuration")]
        [SerializeField] private bool _useMockForTesting = false;
        [SerializeField] private float _mockPlaybackSpeed = 1.0f;

        #endregion

        #region Public Properties

        /// <summary>
        /// 依存注入済みDifyController
        /// 外部システムからの参照用
        /// </summary>
        public DifyController? Controller { get; private set; }

        /// <summary>
        /// インストーラーの初期化完了状態
        /// </summary>
        public bool IsInitialized { get; private set; }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// MonoBehaviour初期化
        /// 依存注入とDifyController構築を実行
        /// </summary>
        private void Awake()
        {
            try
            {
                InitializeDependencies();
                IsInitialized = true;
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[DifyInstaller] Initialized successfully. Mode: {(_useMockForTesting ? "Mock" : "Real")}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyInstaller] Initialization failed: {ex.Message}");
                IsInitialized = false;
            }
        }

        /// <summary>
        /// GameObject破棄時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            Controller = null;
            IsInitialized = false;
        }

        #endregion

        #region Dependency Injection

        /// <summary>
        /// Clean Architecture準拠の依存注入実行
        /// Inspector設定に基づきMock/Real実装を切り替え
        /// </summary>
        private void InitializeDependencies()
        {
            ValidateConfiguration();

            if (_useMockForTesting)
            {
                InitializeMockImplementation();
            }
            else
            {
                InitializeProductionImplementation();
            }
        }

        /// <summary>
        /// Mock実装の依存注入（テスト・開発用）
        /// SSERecordings完全再現によるOpenAIトークン節約
        /// </summary>
        private void InitializeMockImplementation()
        {
            // Clean Architecture例外領域: Mock実装使用
            var recordingReader = new SSERecordingReader("SSERecordings/dify_sse_recording.json");
            var simulator = new SSERecordingSimulator(_mockPlaybackSpeed);
            var mockHttpClient = new MockHttpClient(recordingReader, simulator);

            // Infrastructure Layer
            var configuration = new DifyConfiguration(
                _apiKey,
                _apiUrl,
                enableAudioProcessing: _enableAudioProcessing,
                enableDebugLogging: _enableDebugLogging);

            var httpAdapter = new DifyHttpAdapter(mockHttpClient, configuration);

            // Application Layer - Mock用軽量実装
            var responseProcessor = new MockResponseProcessor();
            var useCase = new ProcessQueryUseCase(httpAdapter, responseProcessor);

            // Presentation Layer
            Controller = new DifyController(useCase);

            if (_enableDebugLogging)
            {
                Debug.Log("[DifyInstaller] Mock implementation initialized (SSERecordings)");
            }
        }

        /// <summary>
        /// Production実装の依存注入（本番用）
        /// UnityWebRequestによる実際のHTTP通信
        /// </summary>
        private void InitializeProductionImplementation()
        {
            // Infrastructure Layer - 実際のHTTP通信
            var configuration = new DifyConfiguration(
                _apiKey,
                _apiUrl,
                enableAudioProcessing: _enableAudioProcessing,
                enableDebugLogging: _enableDebugLogging);

            var httpClient = new UnityWebRequestHttpClient(configuration);
            var httpAdapter = new DifyHttpAdapter(httpClient, configuration);

            // Application Layer - 本番用レスポンス処理
            var responseProcessor = new DefaultResponseProcessor();
            var useCase = new ProcessQueryUseCase(httpAdapter, responseProcessor);

            // Presentation Layer
            Controller = new DifyController(useCase);

            if (_enableDebugLogging)
            {
                Debug.Log("[DifyInstaller] Production implementation initialized (Real HTTP)");
            }
        }

        #endregion

        #region Configuration Validation

        /// <summary>
        /// Inspector設定の妥当性検証
        /// </summary>
        /// <exception cref="InvalidOperationException">設定が無効な場合</exception>
        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("API Key is required. Please set it in Inspector.");
            }

            if (string.IsNullOrWhiteSpace(_apiUrl))
            {
                throw new InvalidOperationException("API URL is required. Please set it in Inspector.");
            }

            if (!Uri.TryCreate(_apiUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Invalid API URL format: {_apiUrl}");
            }

            if (_mockPlaybackSpeed <= 0)
            {
                throw new InvalidOperationException("Mock playback speed must be positive.");
            }
        }

        #endregion

        #region Mock Response Processor

        /// <summary>
        /// Mock用レスポンス処理サービス
        /// テスト・開発用の軽量実装
        /// </summary>
        private class MockResponseProcessor : IResponseProcessor
        {
            public void ProcessAudioEvent(AiTuber.Services.Dify.Domain.Entities.DifyStreamEvent streamEvent)
            {
                // Audio処理は無効（Mock用）
                Debug.Log($"[MockResponseProcessor] Audio event ignored: {streamEvent.EventType}");
            }

            public void ProcessTextEvent(AiTuber.Services.Dify.Domain.Entities.DifyStreamEvent streamEvent)
            {
                // Text処理の基本実装
                Debug.Log($"[MockResponseProcessor] Text event processed: {streamEvent.EventType}");
            }
        }

        #endregion

        #region Default Response Processor

        /// <summary>
        /// 本番用レスポンス処理サービス
        /// 実際の音声・テキスト処理を実装
        /// </summary>
        private class DefaultResponseProcessor : IResponseProcessor
        {
            public void ProcessAudioEvent(AiTuber.Services.Dify.Domain.Entities.DifyStreamEvent streamEvent)
            {
                if (string.IsNullOrEmpty(streamEvent.Audio))
                    return;

                try
                {
                    // Base64音声データをデコード
                    var audioBytes = Convert.FromBase64String(streamEvent.Audio);
                    
                    // 実際の音声再生システムへ渡す（実装は別モジュール）
                    Debug.Log($"[DefaultResponseProcessor] Processing audio data: {audioBytes.Length} bytes");
                    
                    // TODO: AudioManager.PlayAudioClip(audioBytes) 等の実装
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DefaultResponseProcessor] Audio processing failed: {ex.Message}");
                }
            }

            public void ProcessTextEvent(AiTuber.Services.Dify.Domain.Entities.DifyStreamEvent streamEvent)
            {
                if (string.IsNullOrEmpty(streamEvent.Answer))
                    return;

                // 実際のテキスト表示システムへ渡す
                Debug.Log($"[DefaultResponseProcessor] Text response: {streamEvent.Answer}");
                
                // TODO: UIManager.DisplayText(streamEvent.Answer) 等の実装
            }
        }

        #endregion
    }
}