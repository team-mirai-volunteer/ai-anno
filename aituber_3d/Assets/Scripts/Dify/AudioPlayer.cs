#nullable enable
using System;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// MP3音声ファイル再生クラス
    /// </summary>
    public class AudioPlayer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string logPrefix = "[AudioPlayer]";
        
        private bool debugLog;

        private AudioSource? audioSource;

        /// <summary>
        /// 音声再生完了イベント
        /// </summary>
        public event Action? OnPlaybackCompleted;

        /// <summary>
        /// 音声再生エラーイベント
        /// </summary>
        public event Action<string>? OnPlaybackError;


        /// <summary>
        /// MP3バイトデータから音声を再生
        /// </summary>
        /// <param name="mp3Data">MP3バイトデータ</param>
        /// <returns>再生タスク</returns>
        public async UniTask PlayAudioFromDataAsync(byte[] mp3Data)
        {
            if (mp3Data == null || mp3Data.Length == 0)
            {
                var error = "音声データが空です";
                if (debugLog) Debug.LogWarning($"{logPrefix} {error}");
                OnPlaybackError?.Invoke(error);
                return;
            }

            try
            {
                var audioClip = await CreateAudioClipFromMp3Async(mp3Data);
                
                if (audioClip != null)
                {
                    await PlayAudioClipAsync(audioClip);
                }
                else
                {
                    var error = "MP3ファイル解析に失敗しました";
                    Debug.LogError($"{logPrefix} {error}");
                    OnPlaybackError?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                var error = $"音声再生例外: {ex.Message}";
                Debug.LogError($"{logPrefix} {error}");
                OnPlaybackError?.Invoke(error);
            }
        }

        /// <summary>
        /// MP3データからAudioClipを作成
        /// </summary>
        /// <param name="mp3Data">MP3バイトデータ</param>
        /// <returns>作成されたAudioClip</returns>
        private async UniTask<AudioClip?> CreateAudioClipFromMp3Async(byte[] mp3Data)
        {
            try
            {
                var tempPath = System.IO.Path.GetTempFileName() + ".mp3";
                System.IO.File.WriteAllBytes(tempPath, mp3Data);

                try
                {
                    using (var request = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG))
                    {
                        await request.SendWebRequest();

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            var audioClip = DownloadHandlerAudioClip.GetContent(request);
                                        return audioClip;
                        }
                        else
                        {
                            Debug.LogError($"{logPrefix} MP3読み込みエラー: {request.error}");
                            return null;
                        }
                    }
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} MP3処理例外: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// AudioClipを再生して完了まで待機
        /// </summary>
        /// <param name="audioClip">再生するAudioClip</param>
        /// <returns>再生完了タスク</returns>
        private async UniTask PlayAudioClipAsync(AudioClip audioClip)
        {
            if (audioSource == null) return;

            try
            {
                if (audioSource.isPlaying) audioSource.Stop();

                audioSource.clip = audioClip;
                audioSource.Play();


                // シンプルに isPlaying が false になるまで待機
                while (audioSource.isPlaying)
                {
                    await UniTask.Delay(100);
                }

                Debug.Log($"{logPrefix} 🔊 音声再生完了 - OnPlaybackCompletedイベント発火");
                OnPlaybackCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                var error = $"音声再生例外: {ex.Message}";
                Debug.LogError($"{logPrefix} {error}");
                OnPlaybackError?.Invoke(error);
            }
            finally
            {
                if (audioClip != null) DestroyImmediate(audioClip);
            }
        }

        /// <summary>
        /// 音声停止
        /// </summary>
        public void StopAudio()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                if (debugLog) Debug.Log($"{logPrefix} 音声再生を停止しました");
            }
        }

        /// <summary>
        /// 再生中かどうか
        /// </summary>
        public bool IsPlaying => audioSource?.isPlaying ?? false;

        /// <summary>
        /// 音量設定
        /// </summary>
        public void SetVolume(float volume)
        {
            if (audioSource != null)
            {
                audioSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>
        /// 依存注入（インストーラーから一括設定）
        /// </summary>
        /// <param name="audioSourceComponent">AudioSourceコンポーネント</param>
        /// <param name="debugLogEnabled">DebugLog有効フラグ</param>
        public void Install(AudioSource audioSourceComponent, bool debugLogEnabled)
        {
            audioSource = audioSourceComponent;
            debugLog = debugLogEnabled;
            if (debugLog) Debug.Log($"{logPrefix} Install完了");
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        private void OnDestroy()
        {
            StopAudio();
        }
    }
}