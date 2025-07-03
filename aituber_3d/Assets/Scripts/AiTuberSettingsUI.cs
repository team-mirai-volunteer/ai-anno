using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AiTuber.Dify;

namespace AiTuber
{
    public class AiTuberSettingsUI : MonoBehaviour
    {
        [Header("Input Fields")]
        public TMP_InputField oneCommeUrlInput;
        public TMP_InputField difyUrlInput;
        public TMP_InputField difyApiKeyInput;

        [Header("Status Texts")]
        public TMP_Text oneCommeStatusText;
        public TMP_Text difyUrlStatusText;
        public TMP_Text apiKeyStatusText;
        public TMP_Text systemStatusText;
        public TMP_Text messageText;

        [Header("Buttons")]
        public Button saveButton;
        public Button reloadButton;
        public Button clearButton;
        public Button resetAnswerCountButton;
        public Button closeButton;

        private void Start()
        {
            LoadSettings();
            UpdateStatus();
            saveButton.onClick.AddListener(SaveSettings);
            reloadButton.onClick.AddListener(() => { LoadSettings(); UpdateStatus(); ShowMessage("設定を再読込しました"); });
            clearButton.onClick.AddListener(ClearSettings);
            resetAnswerCountButton.onClick.AddListener(ResetAnswerCount);
            closeButton.onClick.AddListener(Close);
        }

        private void LoadSettings()
        {
            oneCommeUrlInput.text = PlayerPrefs.GetString(Constants.PlayerPrefs.OneCommeUrl, "");
            difyUrlInput.text = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyUrl, "");
            difyApiKeyInput.text = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyApiKey, "");
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString(Constants.PlayerPrefs.OneCommeUrl, oneCommeUrlInput.text);
            PlayerPrefs.SetString(Constants.PlayerPrefs.DifyUrl, difyUrlInput.text);
            PlayerPrefs.SetString(Constants.PlayerPrefs.DifyApiKey, difyApiKeyInput.text);
            PlayerPrefs.Save();
            UpdateStatus();
            ShowMessage("設定を保存しました");
        }

        private void ClearSettings()
        {
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.OneCommeUrl);
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.DifyUrl);
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.DifyApiKey);
            PlayerPrefs.Save();
            oneCommeUrlInput.text = "";
            difyUrlInput.text = "";
            difyApiKeyInput.text = "";
            UpdateStatus();
            ShowMessage("すべての設定をクリアしました");
        }

        private void Close()
        {
            this.gameObject.SetActive(false);
        }

        private void UpdateStatus()
        {
            bool oneCommeValid = !string.IsNullOrWhiteSpace(oneCommeUrlInput.text);
            bool difyUrlValid = !string.IsNullOrWhiteSpace(difyUrlInput.text);
            bool apiKeyValid = !string.IsNullOrWhiteSpace(difyApiKeyInput.text);
            oneCommeStatusText.text = oneCommeValid ? "o Valid" : "x Invalid";
            difyUrlStatusText.text = difyUrlValid ? "o Valid" : "x Invalid";
            apiKeyStatusText.text = apiKeyValid ? "o Set" : "x Not Set";
            systemStatusText.text = (oneCommeValid && difyUrlValid && apiKeyValid) ? "o Ready" : "x Configuration Required";
        }

        private void ResetAnswerCount()
        {
            PlayerPrefs.DeleteKey(Constants.PlayerPrefs.TotalAnswerCount);
            PlayerPrefs.Save();
            
            // MainUIControllerのカウンターもリセット
            var mainUIController = FindObjectOfType<MainUIController>();
            if (mainUIController != null)
            {
                mainUIController.ResetAnswerCount();
            }
            
            ShowMessage("回答累積数をリセットしました");
        }
        
        private void ShowMessage(string msg)
        {
            messageText.text = msg;
        }
    }
}
