#nullable enable
using UnityEngine;
using UnityEditor;
using AiTuber.Dify;

namespace AiTuber.Editor
{
    /// <summary>
    /// OneCommeクライアント用テストウィンドウ - 擬似コメント送信
    /// </summary>
    public class OneCommeTestWindow : EditorWindow
    {
        private string testComment = "こんにちは！テストコメントです。";
        private string testUserName = "テストユーザー";
        private OneCommeClient? oneCommeClient;

        [MenuItem("AiTuber/OneComme Test Window")]
        public static void ShowWindow()
        {
            GetWindow<OneCommeTestWindow>("OneComme Test");
        }

        void OnGUI()
        {
            GUILayout.Label("OneComme テストコメント送信", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // OneCommeClient自動検索
            if (oneCommeClient == null)
            {
                oneCommeClient = FindObjectOfType<OneCommeClient>();
            }
            
            // クライアント状態表示
            if (oneCommeClient == null)
            {
                EditorGUILayout.HelpBox("OneCommeClientが見つかりません。\nシーンにSystemプレハブが配置されているか確認してください。", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField("接続状態", oneCommeClient.IsConnected ? "接続中" : "切断中");
            EditorGUILayout.Space();
            
            // 入力フィールド
            EditorGUILayout.LabelField("テストコメント設定", EditorStyles.boldLabel);
            testComment = EditorGUILayout.TextField("コメント", testComment);
            testUserName = EditorGUILayout.TextField("ユーザー名", testUserName);
            
            EditorGUILayout.Space();
            
            // 送信ボタン
            if (GUILayout.Button("テストコメント送信", GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(testComment))
                {
                    oneCommeClient.InjectTestComment(testComment, testUserName);
                    Debug.Log($"[OneCommeTest] 送信完了: [{testUserName}] {testComment}");
                }
                else
                {
                    Debug.LogWarning("[OneCommeTest] コメントが空です");
                }
            }
            
            EditorGUILayout.Space();
            
            // プリセットボタン
            EditorGUILayout.LabelField("プリセットコメント", EditorStyles.boldLabel);
            
            if (GUILayout.Button("質問サンプル1"))
            {
                oneCommeClient.InjectTestComment("AIの未来について教えてください", "質問者A");
            }
            
            if (GUILayout.Button("質問サンプル2"))
            {
                oneCommeClient.InjectTestComment("今日の天気はどうですか？", "質問者B");
            }
            
            if (GUILayout.Button("長文テスト"))
            {
                oneCommeClient.InjectTestComment("これは長いテストコメントです。チャンクシステムが正常に動作するかどうかを確認するために、意図的に長めの文章を作成しています。", "長文ユーザー");
            }
            
            EditorGUILayout.Space();
            
            // 操作ガイド
            EditorGUILayout.HelpBox(
                "使用方法:\n" +
                "1. Play状態でこのウィンドウを開く\n" +
                "2. コメントとユーザー名を入力\n" +
                "3. 「テストコメント送信」ボタンをクリック\n" +
                "4. 実際のOneCommeと同じ処理フローでコメントが処理される",
                MessageType.Info
            );
        }

        void OnInspectorUpdate()
        {
            // プレイ中のみ定期的にクライアントを再検索
            if (Application.isPlaying && oneCommeClient == null)
            {
                oneCommeClient = FindObjectOfType<OneCommeClient>();
            }
            
            Repaint(); // UI更新
        }
    }
}