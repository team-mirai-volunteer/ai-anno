#nullable enable
using System;
using UnityEngine;
using WebSocketSharp;

namespace AiTuber.Dify
{
    /// <summary>
    /// 汎用WebSocketクライアント - 再利用可能
    /// Pure C#でUnity非依存
    /// </summary>
    public class WebSocketClient
    {
        private readonly string webSocketUrl;
        private readonly bool debugLog;
        private readonly string logPrefix;

        public event Action<string>? OnRawMessageReceived;
        public event Action? OnConnected;
        public event Action<string>? OnConnectionError;
        public event Action? OnDisconnected;

        private WebSocket? webSocket;
        private bool isConnected = false;

        /// <summary>
        /// WebSocketClientコンストラクタ
        /// </summary>
        /// <param name="url">接続先URL</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        /// <param name="logPrefix">ログプレフィックス</param>
        public WebSocketClient(string url, bool enableDebugLog = true, string logPrefix = "[WebSocket]")
        {
            this.webSocketUrl = url;
            this.debugLog = enableDebugLog;
            this.logPrefix = logPrefix;
            
            if (debugLog) Debug.Log($"{logPrefix} キュー管理開始 (最大サイズ: {url})");
        }

        /// <summary>
        /// WebSocketサーバーに接続
        /// </summary>
        public void Connect()
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} 接続開始: {webSocketUrl}");

                webSocket = new WebSocket(webSocketUrl);

                webSocket.OnOpen += OnWebSocketOpen;
                webSocket.OnMessage += OnWebSocketMessage;
                webSocket.OnError += OnWebSocketError;
                webSocket.OnClose += OnWebSocketClose;

                webSocket.Connect();
            }
            catch (Exception ex)
            {
                if (debugLog) Debug.LogError($"{logPrefix} 接続エラー: {ex.Message}");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// WebSocketサーバーに接続（URL指定）
        /// </summary>
        /// <param name="url">接続先URL</param>
        public void Connect(string url)
        {
            throw new NotSupportedException("URLを変更する場合は新しいインスタンスを作成してください");
        }

        /// <summary>
        /// 接続切断
        /// </summary>
        public void Disconnect()
        {
            if (webSocket != null && isConnected)
            {
                if (debugLog) Debug.Log($"{logPrefix} 接続切断");
                webSocket.Close();
            }
        }

        /// <summary>
        /// メッセージ送信
        /// </summary>
        /// <param name="message">送信メッセージ</param>
        public void SendMessage(string message)
        {
            if (webSocket != null && isConnected)
            {
                if (debugLog) Debug.Log($"{logPrefix} メッセージ送信: {message}");
                webSocket.Send(message);
            }
            else
            {
                if (debugLog) Debug.LogWarning($"{logPrefix} 未接続のためメッセージ送信失敗: {message}");
            }
        }

        /// <summary>
        /// 接続状態を取得
        /// </summary>
        /// <returns>接続中の場合true</returns>
        public bool IsConnected => isConnected;

        /// <summary>
        /// WebSocket接続成功イベント
        /// </summary>
        private void OnWebSocketOpen(object sender, EventArgs e)
        {
            isConnected = true;
            if (debugLog) Debug.Log($"{logPrefix} 接続成功");
            OnConnected?.Invoke();
        }

        /// <summary>
        /// WebSocketメッセージ受信イベント
        /// </summary>
        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            if (debugLog) Debug.Log($"{logPrefix} メッセージ受信: {e.Data}");
            OnRawMessageReceived?.Invoke(e.Data);
        }

        /// <summary>
        /// WebSocketエラーイベント
        /// </summary>
        private void OnWebSocketError(object sender, ErrorEventArgs e)
        {
            if (debugLog) Debug.LogError($"{logPrefix} WebSocketエラー: {e.Message}");
            OnConnectionError?.Invoke(e.Message);
        }

        /// <summary>
        /// WebSocket接続切断イベント
        /// </summary>
        private void OnWebSocketClose(object sender, CloseEventArgs e)
        {
            isConnected = false;
            if (debugLog) Debug.Log($"{logPrefix} 接続切断 - Code: {e.Code}, Reason: {e.Reason}");
            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            
            OnRawMessageReceived = null;
            OnConnected = null;
            OnConnectionError = null;
            OnDisconnected = null;
        }
    }
}