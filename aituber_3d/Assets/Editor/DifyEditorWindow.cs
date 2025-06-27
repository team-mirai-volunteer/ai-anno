using UnityEngine;
using UnityEditor;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Legacy.Dify;

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

        // サービス関連
        private DifyService _difyService;
        private CancellationTokenSource _cancellationTokenSource;

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
                    DifyEditorSettings.SetApiKey(_tempApiKey);
                    InitializeDifyService();
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
                    DifyEditorSettings.SetApiUrl(_tempApiUrl);
                    InitializeDifyService();
                }
                
                EditorGUILayout.Space(5);
                
                // Options
                EditorGUI.BeginChangeCheck();
                _tempDebugLogging = EditorGUILayout.Toggle("Enable Debug Logging", _tempDebugLogging);
                if (EditorGUI.EndChangeCheck())
                {
                    DifyEditorSettings.SetEnableDebugLogging(_tempDebugLogging);
                }
                
                EditorGUILayout.Space(10);
                
                // 設定状態表示
                EditorGUILayout.Space(5);
                var validationErrors = DifyEditorSettings.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    EditorGUILayout.LabelField("Configuration Errors:", _errorStyle);
                    foreach (var error in validationErrors)
                    {
                        EditorGUILayout.LabelField($"• {error}", _errorStyle);
                    }
                }
                else if (DifyEditorSettings.IsValid())
                {
                    EditorGUILayout.LabelField("✓ Configuration is valid", _successStyle);
                }
                
                EditorGUILayout.Space(5);
                
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
                using (new EditorGUI.DisabledScope(!DifyEditorSettings.IsValid() || _isProcessing))
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
                    using (new EditorGUI.DisabledScope(!DifyEditorSettings.IsValid() || string.IsNullOrWhiteSpace(_currentQuery) || _isProcessing))
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
            _tempApiKey = DifyEditorSettings.ApiKey;
            _tempApiUrl = DifyEditorSettings.ApiUrl;
            _tempDebugLogging = DifyEditorSettings.EnableDebugLogging;
            
            Debug.Log($"[DifyEditor] Settings loaded - URL: {_tempApiUrl}");
            Debug.Log($"[DifyEditor] Settings loaded: {DifyEditorSettings.GetConfigurationSummary()}");
        }


        #endregion

        #region Streaming Event Handling

        /// <summary>
        /// ストリーミングイベント受信コールバック
        /// UIスレッドで実行され、リアルタイムでレスポンスを更新
        /// </summary>
        /// <param name="streamEvent">受信したストリームイベント</param>
        private void OnStreamEventReceived(AiTuber.Services.Legacy.Dify.Data.DifyStreamEvent streamEvent)
        {
            if (streamEvent == null) return;

            _currentEventCount++;

            // テキストメッセージの場合はリアルタイム追加
            if (streamEvent.HasValidTextMessage)
            {
                _streamingResponse.Append(streamEvent.answer);
                _currentResponse = _streamingResponse.ToString();
                
                if (DifyEditorSettings.EnableDebugLogging)
                {
                    Debug.Log($"[DifyEditor] 📝 Stream event #{_currentEventCount}: '{streamEvent.answer}' (Total: {_currentResponse.Length} chars)");
                }
            }
            else if (DifyEditorSettings.EnableDebugLogging)
            {
                Debug.Log($"[DifyEditor] 🎯 Stream event #{_currentEventCount}: {streamEvent.@event}");
            }

            // EditorApplicationのコールバックでUI更新を予約
            EditorApplication.delayCall += () => {
                if (!_isProcessing) return; // 処理終了後は更新しない
                Repaint();
            };
        }

        #endregion

        #region API Operations

        /// <summary>
        /// Difyサービスの初期化
        /// </summary>
        private void InitializeDifyService()
        {
            if (!DifyEditorSettings.IsValid())
            {
                _difyService = null;
                return;
            }

            var config = new DifyServiceConfig
            {
                ApiKey = DifyEditorSettings.ApiKey,
                ApiUrl = DifyEditorSettings.ApiUrl,
                EnableAudioProcessing = false // エディタツールでは音声無効
            };

            var apiClient = DifyEditorSettings.CreateApiClient();
            _difyService = new DifyService(apiClient, config);
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
                InitializeDifyService();
                
                if (_difyService == null)
                {
                    _currentResponse = "Error: Invalid configuration. Please check your settings.";
                    return;
                }
                
                var isConnected = await _difyService.TestConnectionAsync(_cancellationTokenSource.Token);
                
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
                InitializeDifyService();
                
                if (_difyService == null)
                {
                    _currentResponse = "Error: Invalid configuration. Please check your settings.";
                    return;
                }
                
                // デバッグ情報出力
                Debug.Log($"[DifyEditor] Starting query with settings:");
                Debug.Log($"[DifyEditor] API Key from EditorPrefs: '{DifyEditorSettings.ApiKey}'");
                Debug.Log($"[DifyEditor] API Key from UI field: '{_tempApiKey}'");
                Debug.Log($"[DifyEditor] API URL: {DifyEditorSettings.ApiUrl}");
                Debug.Log($"[DifyEditor] Query: {_currentQuery}");
                
                var userId = $"editor-user-{System.DateTime.Now.Ticks}";
                var result = await _difyService.ProcessUserQueryAsync(
                    _currentQuery,
                    userId,
                    conversationId: null,
                    onStreamEvent: OnStreamEventReceived,
                    _cancellationTokenSource.Token);
                
                if (result.IsSuccess)
                {
                    _currentResponse = result.TextResponse ?? "No response received";
                    
                    if (DifyEditorSettings.EnableDebugLogging)
                    {
                        Debug.Log($"[DifyEditor] Query successful. Events: {result.EventCount}, " +
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
    }
}