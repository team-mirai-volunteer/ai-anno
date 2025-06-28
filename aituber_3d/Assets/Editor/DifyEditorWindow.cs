using UnityEngine;
using UnityEditor;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Presentation.Controllers;
using AiTuber.Services.Dify.Presentation;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Application.Ports;
using AiTuber.Services.Dify.Infrastructure.Http;
using AiTuber.Services.Dify.InterfaceAdapters.Translators;
using AiTuber.Services.Dify.Mock;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Editor.Dify
{
    /// <summary>
    /// Difyエディタツールウィンドウ
    /// APIKey/URL設定とリアルタイムストリーミング応答テスト機能を提供
    /// </summary>
    public class DifyEditorWindow : EditorWindow
    {
        #region Private Fields

        private Vector2 _scrollPosition;
        private string _currentQuery = "";
        private string _currentResponse = "";
        private bool _isProcessing = false;
        private float _lastRepaintTime = 0f;
        
        // SSEストリーミング関連
        private int _currentEventCount = 0;
        private System.Text.StringBuilder _streamingResponse;
        private System.DateTime _streamingStartTime;
        
        // UI用一時変数
        private string _tempApiKey = "";
        private string _tempApiUrl = "";
        private bool _tempDebugLogging = true;
        private string _tempSSERecordingPath = "";
        
        // Mock/Real切り替え
        private enum ClientMode { Mock, Real }
        private ClientMode _clientMode = ClientMode.Mock;

        // Clean Architecture サービス関連
        private DifyController? _difyController;
        private CancellationTokenSource? _cancellationTokenSource;

        // UI スタイリング
        private GUIStyle _headerStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _successStyle;
        private GUIStyle _responseStyle;

        #endregion

        #region Unity Editor Menu

        /// <summary>
        /// Difyエディタツールウィンドウを開く
        /// </summary>
        [MenuItem("AiTuber/Dify Editor Tool", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<DifyEditorWindow>("Dify Editor Tool");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        #endregion

        #region Unity Editor Lifecycle

        /// <summary>
        /// ウィンドウ有効化時の初期化
        /// </summary>
        private void OnEnable()
        {
            LoadSettings();
        }

        /// <summary>
        /// ウィンドウ無効化時のクリーンアップ
        /// </summary>
        private void OnDisable()
        {
            CancelCurrentOperation();
        }

        /// <summary>
        /// GUI描画
        /// </summary>
        private void OnGUI()
        {
            InitializeStyles();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawHeader();
            DrawModeSection();
            DrawSettingsSection();
            DrawConnectionTestSection();
            DrawQuerySection();
            DrawResponseSection();
            
            EditorGUILayout.EndScrollView();
            
            // 自動更新（処理中のみ）
            if (_isProcessing && Time.realtimeSinceStartup - _lastRepaintTime > 0.1f)
            {
                _lastRepaintTime = Time.realtimeSinceStartup;
                Repaint();
            }
        }

        #endregion

        #region GUI Drawing Methods

        /// <summary>
        /// ヘッダー描画
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dify Editor Tool", _headerStyle);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "Dify APIの設定とリアルタイムストリーミング応答をテストできます。\n" +
                "設定はEditorPrefsに自動保存されます。",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// 設定セクション描画
        /// </summary>
        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("API Settings", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // API Key
                EditorGUILayout.LabelField("API Key", EditorStyles.label);
                EditorGUI.BeginChangeCheck();
                _tempApiKey = EditorGUILayout.TextField(_tempApiKey);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetString("DifyEditor.ApiKey", _tempApiKey);
                    InitializeDifyController();
                }
                
                if (!string.IsNullOrEmpty(_tempApiKey) && !_tempApiKey.StartsWith("app-"))
                {
                    EditorGUILayout.HelpBox("API Key should start with 'app-'", MessageType.Warning);
                }
                
                EditorGUILayout.Space(5);
                
                // API URL
                EditorGUILayout.LabelField("API URL", EditorStyles.label);
                EditorGUI.BeginChangeCheck();
                _tempApiUrl = EditorGUILayout.TextField(_tempApiUrl);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetString("DifyEditor.ApiUrl", _tempApiUrl);
                    InitializeDifyController();
                }
                
                EditorGUILayout.Space(5);
                
                // Options
                EditorGUI.BeginChangeCheck();
                _tempDebugLogging = EditorGUILayout.Toggle("Enable Debug Logging", _tempDebugLogging);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool("DifyEditor.DebugLogging", _tempDebugLogging);
                }
                
                EditorGUILayout.Space(10);
                
                // 設定状態表示
                EditorGUILayout.Space(5);
                if (IsConfigurationValid())
                {
                    EditorGUILayout.LabelField("✓ Configuration is valid", _successStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("Configuration Errors:", _errorStyle);
                    if (string.IsNullOrWhiteSpace(_tempApiKey))
                        EditorGUILayout.LabelField("• API Key is required", _errorStyle);
                    if (string.IsNullOrWhiteSpace(_tempApiUrl))
                        EditorGUILayout.LabelField("• API URL is required", _errorStyle);
                    if (!string.IsNullOrWhiteSpace(_tempApiUrl) && !System.Uri.IsWellFormedUriString(_tempApiUrl, System.UriKind.Absolute))
                        EditorGUILayout.LabelField("• API URL must be a valid URL", _errorStyle);
                }
                
                EditorGUILayout.Space(5);
                
            }
            
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// Mock/Real切り替えセクション描画
        /// </summary>
        private void DrawModeSection()
        {
            EditorGUILayout.LabelField("Client Mode", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Choose HTTP Client Implementation:", EditorStyles.label);
                
                EditorGUI.BeginChangeCheck();
                _clientMode = (ClientMode)EditorGUILayout.EnumPopup("Mode", _clientMode);
                if (EditorGUI.EndChangeCheck())
                {
                    // モード変更時にコントローラーを再初期化
                    InitializeDifyController();
                }
                
                EditorGUILayout.Space(5);
                
                // モード説明
                switch (_clientMode)
                {
                    case ClientMode.Mock:
                        EditorGUILayout.HelpBox(
                            "Mock Mode: Uses SSERecordings data to reproduce Dify events perfectly.\n" +
                            "• No OpenAI token consumption\n" +
                            "• Perfect timing reproduction\n" +
                            "• Ideal for development and testing",
                            MessageType.Info);
                        
                        // SSE録画ファイルパス設定
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("SSE Recording File", EditorStyles.label);
                        EditorGUI.BeginChangeCheck();
                        _tempSSERecordingPath = EditorGUILayout.TextField(_tempSSERecordingPath);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetString("DifyEditor.SSERecordingPath", _tempSSERecordingPath);
                            InitializeDifyController();
                        }
                        
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Browse SSE File", GUILayout.Width(120)))
                            {
                                var selectedPath = EditorUtility.OpenFilePanel("Select SSE Recording", "SSERecordings", "json");
                                if (!string.IsNullOrEmpty(selectedPath))
                                {
                                    // プロジェクトルートからの相対パスに変換
                                    var projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
                                    if (selectedPath.StartsWith(projectPath))
                                    {
                                        _tempSSERecordingPath = System.IO.Path.GetRelativePath(projectPath, selectedPath);
                                    }
                                    else
                                    {
                                        _tempSSERecordingPath = selectedPath; // 絶対パスのまま
                                    }
                                    EditorPrefs.SetString("DifyEditor.SSERecordingPath", _tempSSERecordingPath);
                                    InitializeDifyController();
                                }
                            }
                            
                            if (GUILayout.Button("Reset Default", GUILayout.Width(100)))
                            {
                                _tempSSERecordingPath = "SSERecordings/dify_sse_recording.json";
                                EditorPrefs.SetString("DifyEditor.SSERecordingPath", _tempSSERecordingPath);
                                InitializeDifyController();
                            }
                        }
                        
                        // ファイル状態表示
                        var fullPath = System.IO.Path.Combine(Application.dataPath, "..", _tempSSERecordingPath);
                        if (System.IO.File.Exists(fullPath))
                        {
                            EditorGUILayout.LabelField("✓ SSE Recording file found", _successStyle);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("✗ SSE Recording file not found", _errorStyle);
                        }
                        
                        break;
                        
                    case ClientMode.Real:
                        EditorGUILayout.HelpBox(
                            "Real Mode: Uses UnityWebRequest to connect to actual Dify API.\n" +
                            "• Consumes OpenAI tokens\n" +
                            "• Real network communication\n" +
                            "• Production-ready implementation",
                            MessageType.Warning);
                        break;
                }
                
                EditorGUILayout.Space(5);
                
                // モード状態表示
                var modeColor = _clientMode == ClientMode.Mock ? Color.green : Color.yellow;
                var oldColor = GUI.color;
                GUI.color = modeColor;
                EditorGUILayout.LabelField($"Current Mode: {_clientMode}", EditorStyles.boldLabel);
                GUI.color = oldColor;
            }
            
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// 接続テストセクション描画
        /// </summary>
        private void DrawConnectionTestSection()
        {
            EditorGUILayout.LabelField("Connection Test", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUI.DisabledScope(!IsConfigurationValid() || _isProcessing))
                {
                    if (GUILayout.Button("Test Connection"))
                    {
                        TestConnectionAsync();
                    }
                }
                
                if (_isProcessing)
                {
                    EditorGUILayout.LabelField("Testing connection...", EditorStyles.label);
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), 0.5f, "");
                }
            }
            
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// クエリセクション描画
        /// </summary>
        private void DrawQuerySection()
        {
            EditorGUILayout.LabelField("Query Test", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Enter your question:", EditorStyles.label);
                _currentQuery = EditorGUILayout.TextArea(_currentQuery, GUILayout.Height(80));
                
                EditorGUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!IsConfigurationValid() || string.IsNullOrWhiteSpace(_currentQuery) || _isProcessing))
                    {
                        if (GUILayout.Button("Send Query (Streaming)"))
                        {
                            SendQueryAsync();
                        }
                    }
                    
                    using (new EditorGUI.DisabledScope(!_isProcessing))
                    {
                        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                        {
                            CancelCurrentOperation();
                        }
                    }
                }
                
                if (_isProcessing)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Processing query...", EditorStyles.label);
                    
                    // ストリーミング進捗表示
                    var elapsed = System.DateTime.Now - _streamingStartTime;
                    var progressText = $"Streaming... Events: {_currentEventCount}, Time: {elapsed.TotalSeconds:F1}s";
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), 0.5f, progressText);
                    
                    // リアルタイム文字数表示
                    if (_streamingResponse != null && _streamingResponse.Length > 0)
                    {
                        EditorGUILayout.LabelField($"Characters received: {_streamingResponse.Length}", EditorStyles.miniLabel);
                    }
                }
            }
            
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// レスポンスセクション描画
        /// </summary>
        private void DrawResponseSection()
        {
            EditorGUILayout.LabelField("Response", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (string.IsNullOrEmpty(_currentResponse))
                {
                    EditorGUILayout.LabelField("No response yet. Send a query to see results.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    var responseRect = EditorGUILayout.GetControlRect(false, 200);
                    _currentResponse = EditorGUI.TextArea(responseRect, _currentResponse, _responseStyle);
                }
                
                EditorGUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clear Response"))
                    {
                        _currentResponse = "";
                    }
                    
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_currentResponse)))
                    {
                        if (GUILayout.Button("Copy to Clipboard"))
                        {
                            EditorGUIUtility.systemCopyBuffer = _currentResponse;
                            Debug.Log("[DifyEditor] Response copied to clipboard");
                        }
                    }
                }
            }
        }

        #endregion

        #region Settings Management

        /// <summary>
        /// 設定を読み込み
        /// </summary>
        private void LoadSettings()
        {
            _tempApiKey = EditorPrefs.GetString("DifyEditor.ApiKey", "");
            _tempApiUrl = EditorPrefs.GetString("DifyEditor.ApiUrl", "https://api.dify.ai/v1/chat-messages");
            _tempDebugLogging = EditorPrefs.GetBool("DifyEditor.DebugLogging", true);
            _tempSSERecordingPath = EditorPrefs.GetString("DifyEditor.SSERecordingPath", "SSERecordings/dify_sse_recording.json");
            
            Debug.Log($"[DifyEditor] Settings loaded - URL: {_tempApiUrl}");
            Debug.Log($"[DifyEditor] Settings loaded - API Key: {(!string.IsNullOrEmpty(_tempApiKey) ? "configured" : "not set")}");
            Debug.Log($"[DifyEditor] Settings loaded - SSE Recording: {_tempSSERecordingPath}");
        }
        
        /// <summary>
        /// 設定の妥当性確認
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        private bool IsConfigurationValid()
        {
            return !string.IsNullOrWhiteSpace(_tempApiKey) &&
                   !string.IsNullOrWhiteSpace(_tempApiUrl) &&
                   System.Uri.IsWellFormedUriString(_tempApiUrl, System.UriKind.Absolute);
        }


        #endregion

        #region Streaming Event Handling

        /// <summary>
        /// ストリーミングイベント受信コールバック
        /// UIスレッドで実行され、リアルタイムでレスポンスを更新
        /// </summary>
        /// <param name="streamEvent">受信したストリームイベント</param>
        private void OnStreamEventReceived(DifyStreamEvent streamEvent)
        {
            if (streamEvent == null) return;

            _currentEventCount++;

            // デバッグ：受信したイベントの詳細ログ
            if (_tempDebugLogging)
            {
                Debug.Log($"[DifyEditor] 🔍 Received event #{_currentEventCount}:");
                Debug.Log($"[DifyEditor]   EventType: {streamEvent.EventType}");
                Debug.Log($"[DifyEditor]   IsMessageEvent: {streamEvent.IsMessageEvent}");
                Debug.Log($"[DifyEditor]   Answer: '{streamEvent.Answer}'");
                Debug.Log($"[DifyEditor]   ConversationId: {streamEvent.ConversationId}");
            }

            // テキストメッセージの場合はリアルタイム追加
            if (streamEvent.IsMessageEvent && !string.IsNullOrEmpty(streamEvent.Answer))
            {
                _streamingResponse.Append(streamEvent.Answer);
                _currentResponse = _streamingResponse.ToString();
                
                if (_tempDebugLogging)
                {
                    Debug.Log($"[DifyEditor] 📝 Adding text: '{streamEvent.Answer}' (Total: {_currentResponse.Length} chars)");
                }
            }
            else if (_tempDebugLogging)
            {
                var reason = !streamEvent.IsMessageEvent ? "not message event" : "empty answer";
                Debug.Log($"[DifyEditor] 🎯 Skipping event: {streamEvent.EventType} ({reason})");
            }

            // EditorApplicationのコールバックでUI更新を予約
            EditorApplication.delayCall += () => {
                // ウィンドウが存在する限り更新（_isProcessingは非同期処理で変わるため除外）
                if (this != null)
                {
                    Repaint();
                }
            };
        }

        #endregion

        #region API Operations

        /// <summary>
        /// DifyControllerの初期化
        /// </summary>
        private void InitializeDifyController()
        {
            if (!IsConfigurationValid())
            {
                _difyController = null;
                return;
            }

            try
            {
                switch (_clientMode)
                {
                    case ClientMode.Mock:
                        _difyController = CreateMockController(_tempApiKey, _tempApiUrl, _tempDebugLogging);
                        Debug.Log("[DifyEditor] Mock DifyController initialized (SSERecordings)");
                        break;
                        
                    case ClientMode.Real:
                        _difyController = CreateProductionController(_tempApiKey, _tempApiUrl, _tempDebugLogging);
                        Debug.Log("[DifyEditor] Production DifyController initialized (Real HTTP)");
                        break;
                }
            }
            catch (System.Exception ex)
            {
                _difyController = null;
                Debug.LogError($"[DifyEditor] Failed to initialize DifyController: {ex.Message}");
            }
        }

        /// <summary>
        /// 接続テスト実行
        /// </summary>
        private async void TestConnectionAsync()
        {
            if (_isProcessing) return;
            
            _isProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                InitializeDifyController();
                
                if (_difyController == null)
                {
                    _currentResponse = "Error: Invalid configuration. Please check your settings.";
                    return;
                }
                
                var isConnected = await _difyController.TestConnectionAsync(_cancellationTokenSource.Token);
                
                _currentResponse = isConnected 
                    ? "✓ Connection successful! Dify API is reachable."
                    : "✗ Connection failed. Please check your API Key and URL.";
                    
                Debug.Log($"[DifyEditor] Connection test result: {isConnected}");
            }
            catch (System.Exception ex)
            {
                _currentResponse = $"Connection test error: {ex.Message}";
                Debug.LogError($"[DifyEditor] Connection test failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                Repaint();
            }
        }

        /// <summary>
        /// クエリ送信（ストリーミング）
        /// </summary>
        private async void SendQueryAsync()
        {
            if (_isProcessing || string.IsNullOrWhiteSpace(_currentQuery)) return;
            
            _isProcessing = true;
            _currentResponse = "";
            _currentEventCount = 0;
            _streamingResponse = new System.Text.StringBuilder();
            _streamingStartTime = System.DateTime.Now;
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                InitializeDifyController();
                
                if (_difyController == null)
                {
                    _currentResponse = "Error: Invalid configuration. Please check your settings.";
                    return;
                }
                
                // デバッグ情報出力
                Debug.Log($"[DifyEditor] Starting query with settings:");
                Debug.Log($"[DifyEditor] API Key: {(!string.IsNullOrEmpty(_tempApiKey) ? "configured" : "not set")}");
                Debug.Log($"[DifyEditor] API URL: {_tempApiUrl}");
                Debug.Log($"[DifyEditor] Query: {_currentQuery}");
                
                var userId = $"editor-user-{System.DateTime.Now.Ticks}";
                var result = await _difyController.SendQueryStreamingAsync(
                    _currentQuery,
                    userId,
                    onEventReceived: OnStreamEventReceived,
                    _cancellationTokenSource.Token);
                
                if (result.IsSuccess)
                {
                    _currentResponse = result.TextResponse ?? "No response received";
                    
                    if (_tempDebugLogging)
                    {
                        Debug.Log($"[DifyEditor] Query successful. " +
                                 $"Processing time: {result.ProcessingTimeMs}ms, " +
                                 $"Response length: {result.TextResponse?.Length ?? 0} chars");
                    }
                }
                else
                {
                    _currentResponse = $"Error: {result.ErrorMessage ?? "Unknown error occurred"}";
                    Debug.LogError($"[DifyEditor] Query failed: {result.ErrorMessage}");
                }
            }
            catch (System.OperationCanceledException)
            {
                _currentResponse = "Operation cancelled by user.";
                Debug.Log("[DifyEditor] Query cancelled by user");
            }
            catch (System.Exception ex)
            {
                _currentResponse = $"Unexpected error: {ex.Message}\nStack trace: {ex.StackTrace}";
                Debug.LogError($"[DifyEditor] Query failed with exception: {ex}");
            }
            finally
            {
                _isProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                Repaint();
            }
        }

        /// <summary>
        /// 現在の操作をキャンセル
        /// </summary>
        private void CancelCurrentOperation()
        {
            _cancellationTokenSource?.Cancel();
            _isProcessing = false;
        }

        #endregion

        #region UI Styling

        /// <summary>
        /// UIスタイル初期化
        /// </summary>
        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.red }
                };
            }
            
            if (_successStyle == null)
            {
                _successStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.green }
                };
            }
            
            if (_responseStyle == null)
            {
                _responseStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    fontSize = 12
                };
            }
        }

        #endregion

        #region Factory Methods (Editor Only)

        /// <summary>
        /// Mock用DifyController作成（EditorWindow専用）
        /// </summary>
        private DifyController CreateMockController(string apiKey, string apiUrl, bool enableDebugLogging)
        {
            // Mock例外領域: SSERecordings再生
            var recordingReader = new SSERecordingReader(_tempSSERecordingPath);
            var simulator = new SSERecordingSimulator(1.0f);
            var mockHttpClient = new MockHttpClient(recordingReader, simulator);

            // Infrastructure Layer
            var configuration = new DifyConfiguration(
                apiKey,
                apiUrl,
                enableAudioProcessing: false, // EditorWindow用は音声無効
                enableDebugLogging: enableDebugLogging);

            var httpAdapter = new DifyHttpAdapter(mockHttpClient, configuration);

            // Application Layer
            var responseProcessor = new MockResponseProcessor();
            var useCase = new ProcessQueryUseCase(httpAdapter, responseProcessor);

            // Presentation Layer
            return new DifyController(useCase);
        }

        /// <summary>
        /// Production用DifyController作成（EditorWindow専用）
        /// </summary>
        private DifyController CreateProductionController(string apiKey, string apiUrl, bool enableDebugLogging)
        {
            // Infrastructure Layer
            var configuration = new DifyConfiguration(
                apiKey,
                apiUrl,
                enableAudioProcessing: false, // EditorWindow用は音声無効
                enableDebugLogging: enableDebugLogging);

            var httpClient = new UnityWebRequestHttpClient(configuration);
            var httpAdapter = new DifyHttpAdapter(httpClient, configuration);

            // Application Layer
            var responseProcessor = new MockResponseProcessor(); // EditorWindow用は軽量実装
            var useCase = new ProcessQueryUseCase(httpAdapter, responseProcessor);

            // Presentation Layer
            return new DifyController(useCase);
        }

        /// <summary>
        /// EditorWindow用レスポンス処理サービス
        /// </summary>
        private class MockResponseProcessor : IResponseProcessor
        {
            public void ProcessAudioEvent(DifyStreamEvent streamEvent)
            {
                // Audio処理は無効（Editor用）
                Debug.Log($"[MockResponseProcessor] Audio event ignored: {streamEvent.EventType}");
            }

            public void ProcessTextEvent(DifyStreamEvent streamEvent)
            {
                // Text処理の基本実装
                Debug.Log($"[MockResponseProcessor] Text event processed: {streamEvent.EventType}");
            }
        }

        #endregion
    }
}