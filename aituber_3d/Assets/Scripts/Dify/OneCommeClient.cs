#nullable enable
using System;
using System.Collections.Concurrent;
using UnityEngine;
using Newtonsoft.Json;

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
            webSocketClient.OnConnected += () => connectionQueue.Enqueue(true);
            webSocketClient.OnConnectionError += (error) => errorQueue.Enqueue(error);
            webSocketClient.OnDisconnected += () => disconnectionQueue.Enqueue(true);
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
        public void Install(string url, bool autoConnectFlag, bool debugLogEnabled)
        {
            oneCommeUrl = url;
            autoConnect = autoConnectFlag;
            debugLog = debugLogEnabled;
            
            Debug.Log($"[OneCommeClient] Install完了 - URL: {url}, AutoConnect: {autoConnectFlag}, DebugLog: {debugLogEnabled}");
            
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
            // コメントキューを処理
            while (commentQueue.TryDequeue(out var comment))
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
        /// Unity終了時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            CleanupWebSocket();
        }

        /// <summary>
        /// アプリケーション終了時のクリーンアップ
        /// </summary>
        private void OnApplicationQuit()
        {
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

    }
}