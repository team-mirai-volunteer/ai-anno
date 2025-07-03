#nullable enable
using System;
using System.Collections.Concurrent;
using UnityEngine;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// OneComme専用クライアント - WebSocketClientのラッパー
    /// OneComme: https://onecomme.com/
    /// </summary>
    public class OneCommeClient : MonoBehaviour
    {
        private string oneCommeUrl = "ws://localhost:11180/";
        private bool autoConnect = true;
        private bool debugLog;
        
        // 自動再接続設定
        private bool autoReconnectEnabled = true;
        private float reconnectIntervalSeconds = 0.5f;
        private bool isAutoReconnecting = false;

        public event Action<OneCommeComment>? OnCommentReceived;
        public event Action? OnConnected;
        public event Action<string>? OnConnectionError;
        public event Action? OnDisconnected;

        private WebSocketClient? webSocketClient;

        // スレッドセーフなキュー群
        private readonly ConcurrentQueue<OneCommeComment> commentQueue = new();
        private readonly ConcurrentQueue<bool> connectionQueue = new();
        private readonly ConcurrentQueue<string> errorQueue = new();
        private readonly ConcurrentQueue<bool> disconnectionQueue = new();

        /// <summary>
        /// Unity開始時の初期化
        /// </summary>
        private void Start()
        {
            // Configure()で初期化済みなのでautoConnectのみチェック
            if (autoConnect)
            {
                Connect();
            }
        }

        /// <summary>
        /// WebSocketClientを初期化
        /// </summary>
        private void InitializeWebSocketClient()
        {
            webSocketClient = new WebSocketClient(oneCommeUrl, debugLog, "[OneComme]");

            webSocketClient.OnRawMessageReceived += OnRawMessageReceivedFromThread;
            webSocketClient.OnConnected += () => {
                connectionQueue.Enqueue(true);
                StopAutoReconnect(); // 接続成功時に再接続ループ停止
            };
            webSocketClient.OnConnectionError += (error) => errorQueue.Enqueue(error);
            webSocketClient.OnDisconnected += () => {
                disconnectionQueue.Enqueue(true);
                StartAutoReconnect(); // 切断時に再接続ループ開始
            };
        }

        /// <summary>
        /// OneCommeサーバーに接続
        /// </summary>
        public void Connect()
        {
            webSocketClient?.Connect();
        }

        /// <summary>
        /// 接続切断
        /// </summary>
        public void Disconnect()
        {
            webSocketClient?.Disconnect();
        }

        /// <summary>
        /// 接続状態を取得
        /// </summary>
        public bool IsConnected => webSocketClient?.IsConnected ?? false;

        /// <summary>
        /// 依存注入（インストーラーから一括設定）
        /// </summary>
        /// <param name="url">OneComme WebSocket URL</param>
        /// <param name="autoConnectFlag">自動接続フラグ</param>
        /// <param name="debugLogEnabled">DebugLog有効フラグ</param>
        /// <param name="enableAutoReconnect">自動再接続有効フラグ</param>
        /// <param name="reconnectInterval">再接続間隔（秒）</param>
        public void Install(string url, bool autoConnectFlag, bool debugLogEnabled, bool enableAutoReconnect, float reconnectInterval)
        {
            oneCommeUrl = url;
            autoConnect = autoConnectFlag;
            debugLog = debugLogEnabled;
            autoReconnectEnabled = enableAutoReconnect;
            reconnectIntervalSeconds = reconnectInterval;
            
            Debug.Log($"[OneCommeClient] Install完了 - URL: {url}, AutoConnect: {autoConnectFlag}, DebugLog: {debugLogEnabled}, AutoReconnect: {enableAutoReconnect}, ReconnectInterval: {reconnectInterval}秒");
            
            // WebSocketClient再初期化
            InitializeWebSocketClient();
        }

        /// <summary>
        /// 別スレッドからの生メッセージ受信イベント（キューにエンキュー）
        /// </summary>
        private void OnRawMessageReceivedFromThread(string rawMessage)
        {
            try
            {
                
                var message = JsonConvert.DeserializeObject<OneCommeMessage>(rawMessage);
                
                
                if (message != null && message.type == "comments" && message.data?.comments != null)
                {
                    
                    foreach (var comment in message.data.comments)
                    {
                        if (comment != null)
                        {
                            commentQueue.Enqueue(comment);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // スレッドセーフなログ出力
                Debug.LogError($"[OneComme] JSONデシリアライズエラー: {ex.Message}");
                Debug.LogError($"[OneComme] 生メッセージ先頭100文字: {rawMessage.Substring(0, Math.Min(100, rawMessage.Length))}");
            }
        }

        /// <summary>
        /// Unity Updateメソッド - キューからメインスレッドでイベント配信
        /// </summary>
        private void Update()
        {
            // コメントキューを処理（1フレーム1コメント）
            if (commentQueue.TryDequeue(out var comment))
            {
                OnCommentReceived?.Invoke(comment);
            }

            // 接続キューを処理
            while (connectionQueue.TryDequeue(out var _))
            {
                OnConnected?.Invoke();
            }

            // エラーキューを処理
            while (errorQueue.TryDequeue(out var error))
            {
                if (debugLog) Debug.LogError($"[OneComme] 接続エラー: {error}");
                OnConnectionError?.Invoke(error);
            }

            // 切断キューを処理
            while (disconnectionQueue.TryDequeue(out var _))
            {
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// 自動再接続開始
        /// </summary>
        private async void StartAutoReconnect()
        {
            if (!autoReconnectEnabled || isAutoReconnecting)
            {
                return;
            }
            
            isAutoReconnecting = true;
            if (debugLog) Debug.Log("[OneCommeClient] 自動再接続開始");
            
            while (!IsConnected && autoReconnectEnabled && isAutoReconnecting)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(reconnectIntervalSeconds));
                
                if (!IsConnected && autoReconnectEnabled && isAutoReconnecting)
                {
                    if (debugLog) Debug.Log("[OneCommeClient] 再接続試行");
                    Connect();
                }
            }
            
            isAutoReconnecting = false;
            if (debugLog) Debug.Log("[OneCommeClient] 自動再接続終了");
        }
        
        /// <summary>
        /// 自動再接続停止
        /// </summary>
        private void StopAutoReconnect()
        {
            if (isAutoReconnecting)
            {
                isAutoReconnecting = false;
                if (debugLog) Debug.Log("[OneCommeClient] 自動再接続停止");
            }
        }

        /// <summary>
        /// Unity終了時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            StopAutoReconnect();
            CleanupWebSocket();
        }

        /// <summary>
        /// アプリケーション終了時のクリーンアップ
        /// </summary>
        private void OnApplicationQuit()
        {
            StopAutoReconnect();
            CleanupWebSocket();
        }

        /// <summary>
        /// WebSocket関連のリソース解放
        /// </summary>
        private void CleanupWebSocket()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnRawMessageReceived -= OnRawMessageReceivedFromThread;
                webSocketClient.Dispose();
                webSocketClient = null;
            }

            // キューをクリア
            while (commentQueue.TryDequeue(out var _)) { }
            while (connectionQueue.TryDequeue(out var _)) { }
            while (errorQueue.TryDequeue(out var _)) { }
            while (disconnectionQueue.TryDequeue(out var _)) { }
        }

        /// <summary>
        /// テスト用擬似コメント流し込み（Editor専用）
        /// </summary>
        /// <param name="comment">擬似コメント文</param>
        /// <param name="userName">擬似ユーザー名</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void InjectTestComment(string comment, string userName = "テストユーザー")
        {
            var testComment = new OneCommeComment
            {
                id = System.Guid.NewGuid().ToString(),
                data = new OneCommeCommentData
                {
                    comment = comment,
                    speechText = comment,
                    name = userName,
                    displayName = userName,
                    service = "editor_test",
                    timestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
                    profileImage = null
                }
            };
            
            if (debugLog) Debug.Log($"[OneComme] テストコメント注入: [{userName}] {comment}");
            
            // 直接イベント発火（メインスレッドで実行）
            OnCommentReceived?.Invoke(testComment);
        }

    }
}