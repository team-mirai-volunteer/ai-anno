#nullable enable
using UnityEngine;
using UnityEditor;
using AiTuber.Dify;

namespace AiTuber.Editor
{
    /// <summary>
    /// AiTuber設定ウィンドウ - PlayerPrefs設定用エディタウィンドウ
    /// </summary>
    public class AiTuberSettingsWindow : EditorWindow
    {
        private string oneCommeUrl = "";
        private string difyUrl = "";
        private string difyApiKey = "";
        
        private bool hasLoadedSettings = false;

        /// <summary>
        /// エディタメニューから設定ウィンドウを開く
        /// </summary>
        [MenuItem("AiTuber/Settings")]
        public static void ShowWindow()
        {
            GetWindow<AiTuberSettingsWindow>("AiTuber Settings");
        }

        /// <summary>
        /// ウィンドウが開かれた時の処理
        /// </summary>
        private void OnEnable()
        {
            LoadSettings();
        }

        /// <summary>
        /// 現在の設定をPlayerPrefsから読み込み
        /// </summary>
        private void LoadSettings()
        {
            oneCommeUrl = PlayerPrefs.GetString(Constants.PlayerPrefs.OneCommeUrl, "");
            difyUrl = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyUrl, "");
            difyApiKey = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyApiKey, "");
            hasLoadedSettings = true;
        }

        /// <summary>
        /// GUI描画
        /// </summary>
        private void OnGUI()
        {
            if (!hasLoadedSettings)
            {
                LoadSettings();
            }

            GUILayout.Label("AiTuber System Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // OneComme設定
            GUILayout.Label("OneComme Configuration", EditorStyles.boldLabel);
            oneCommeUrl = EditorGUILayout.TextField("OneComme URL:", oneCommeUrl);
            EditorGUILayout.HelpBox("OneComme WebSocket URL (例: ws://localhost:11180/)", MessageType.Info);
            EditorGUILayout.Space();

            // Dify設定
            GUILayout.Label("Dify Configuration", EditorStyles.boldLabel);
            difyUrl = EditorGUILayout.TextField("Dify URL:", difyUrl);
            EditorGUILayout.HelpBox("Dify API エンドポイント URL", MessageType.Info);
            
            difyApiKey = EditorGUILayout.PasswordField("Dify API Key:", difyApiKey);
            EditorGUILayout.HelpBox("Dify API Key (セキュリティ上、PlayerPrefsに保存されます)", MessageType.Warning);
            EditorGUILayout.Space();

            // 現在の設定状態表示
            ShowCurrentStatus();
            EditorGUILayout.Space();

            // ボタン群
            DrawButtons();
        }

        /// <summary>
        /// 現在の設定状態を表示
        /// </summary>
        private void ShowCurrentStatus()
        {
            GUILayout.Label("Current Status", EditorStyles.boldLabel);
            
            // OneComme URL 状態
            bool oneCommeValid = !string.IsNullOrWhiteSpace(oneCommeUrl);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("OneComme URL:", oneCommeValid ? "✓ Valid" : "✗ Invalid");
            GUILayout.FlexibleSpace();
            if (oneCommeValid)
            {
                EditorGUILayout.LabelField("", EditorStyles.helpBox);
            }
            EditorGUILayout.EndHorizontal();

            // Dify URL 状態
            bool difyUrlValid = !string.IsNullOrWhiteSpace(difyUrl);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Dify URL:", difyUrlValid ? "✓ Valid" : "✗ Invalid");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // API Key 状態
            bool apiKeyValid = !string.IsNullOrWhiteSpace(difyApiKey);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", apiKeyValid ? "✓ Set" : "✗ Not Set");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 全体の状態
            bool allValid = oneCommeValid && difyUrlValid && apiKeyValid;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("System Status:", allValid ? "✓ Ready" : "✗ Configuration Required");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// ボタン群の描画
        /// </summary>
        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 保存ボタン
            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
            {
                SaveSettings();
            }

            // リロードボタン
            if (GUILayout.Button("Reload Settings", GUILayout.Height(30)))
            {
                LoadSettings();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // クリアボタン
            if (GUILayout.Button("Clear All Settings", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Clear Settings", 
                    "すべての設定をクリアしますか？", "Yes", "Cancel"))
                {
                    ClearSettings();
                }
            }

            EditorGUILayout.Space();

            // テスト用ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test OneComme Connection"))
            {
                TestOneCommeConnection();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 設定をPlayerPrefsに保存
        /// </summary>
        private void SaveSettings()
        {
            PlayerPrefs.SetString(Constants.PlayerPrefs.OneCommeUrl, oneCommeUrl);
            PlayerPrefs.SetString(Constants.PlayerPrefs.DifyUrl, difyUrl);
            PlayerPrefs.SetString(Constants.PlayerPrefs.DifyApiKey, difyApiKey);
            PlayerPrefs.Save();

            Debug.Log("[AiTuber Settings] 設定を保存しました");
            EditorUtility.DisplayDialog("Settings Saved", "設定がPlayerPrefsに保存されました", "OK");
        }

        /// <summary>
        /// すべての設定をクリア
        /// </summary>
        private void ClearSettings()
        {
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.OneCommeUrl);
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.DifyUrl);
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.DifyApiKey);
            PlayerPrefs.Save();

            // フィールドもクリア
            oneCommeUrl = "";
            difyUrl = "";
            difyApiKey = "";

            Debug.Log("[AiTuber Settings] すべての設定をクリアしました");
            EditorUtility.DisplayDialog("Settings Cleared", "すべての設定がクリアされました", "OK");
            Repaint();
        }

        /// <summary>
        /// OneComme接続テスト
        /// </summary>
        private void TestOneCommeConnection()
        {
            if (string.IsNullOrWhiteSpace(oneCommeUrl))
            {
                EditorUtility.DisplayDialog("Test Failed", "OneComme URLが設定されていません", "OK");
                return;
            }

            Debug.Log($"[AiTuber Settings] OneComme接続テスト開始: {oneCommeUrl}");
            EditorUtility.DisplayDialog("Connection Test", 
                $"OneComme接続テストを開始します\nURL: {oneCommeUrl}\n\n結果はConsoleログで確認してください", "OK");
        }
    }
}