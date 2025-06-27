using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.TestTools;
using AiTuber.Services.Legacy.Dify;
using AiTuber.Services.Legacy.Dify.Data;

namespace AiTuber.Tests.Legacy.Editor
{
    /// <summary>
    /// DifyServiceのイベント機能テスト
    /// Infrastructure LayerからApplication Layerへのイベント配信テスト
    /// </summary>
    [TestFixture]
    public class DifyServiceEventsTests
    {
        private DifyServiceWithEvents _difyService;
        private bool _audioEventReceived;
        private bool _textEventReceived;
        private bool _workflowEventReceived;
        private DifyStreamEvent _receivedAudioEvent;
        private DifyStreamEvent _receivedTextEvent;
        private DifyStreamEvent _receivedWorkflowEvent;
        
        [SetUp]
        public void Setup()
        {
            _difyService = new DifyServiceWithEvents();
            _audioEventReceived = false;
            _textEventReceived = false;
            _workflowEventReceived = false;
            _receivedAudioEvent = null;
            _receivedTextEvent = null;
            _receivedWorkflowEvent = null;
        }
        
        [TearDown]
        public void TearDown()
        {
            _difyService?.Dispose();
        }
        
        [Test]
        public void OnAudioMessage_TTSメッセージイベント_正しくイベント発火される()
        {
            // Arrange
            _difyService.OnAudioMessage += (audioEvent) =>
            {
                _audioEventReceived = true;
                _receivedAudioEvent = audioEvent;
            };
            
            var ttsEvent = new DifyStreamEvent
            {
                @event = "tts_message",
                audio = Convert.ToBase64String(new byte[] { 0xFF, 0xF3, 0x01 })
            };
            
            // Act
            _difyService.ProcessStreamEvent(ttsEvent);
            
            // Assert
            Assert.IsTrue(_audioEventReceived, "OnAudioMessageイベントが発火されること");
            Assert.IsNotNull(_receivedAudioEvent, "イベントデータが正しく渡されること");
            Assert.AreEqual("tts_message", _receivedAudioEvent.@event, "イベントタイプが正しく渡されること");
        }
        
        [Test]
        public void OnTextMessage_メッセージイベント_正しくイベント発火される()
        {
            // Arrange
            _difyService.OnTextMessage += (textEvent) =>
            {
                _textEventReceived = true;
                _receivedTextEvent = textEvent;
            };
            
            var messageEvent = new DifyStreamEvent
            {
                @event = "message",
                answer = "こんにちは、これはテストメッセージです。"
            };
            
            // Act
            _difyService.ProcessStreamEvent(messageEvent);
            
            // Assert
            Assert.IsTrue(_textEventReceived, "OnTextMessageイベントが発火されること");
            Assert.IsNotNull(_receivedTextEvent, "イベントデータが正しく渡されること");
            Assert.AreEqual("message", _receivedTextEvent.@event, "イベントタイプが正しく渡されること");
            Assert.AreEqual("こんにちは、これはテストメッセージです。", _receivedTextEvent.answer, "メッセージ内容が正しく渡されること");
        }
        
        [Test]
        public void OnWorkflowFinished_ワークフロー完了イベント_正しくイベント発火される()
        {
            // Arrange
            _difyService.OnWorkflowFinished += (workflowEvent) =>
            {
                _workflowEventReceived = true;
                _receivedWorkflowEvent = workflowEvent;
            };
            
            var finishedEvent = new DifyStreamEvent
            {
                @event = "workflow_finished",
                workflow_run_id = "test-workflow-123"
            };
            
            // Act
            _difyService.ProcessStreamEvent(finishedEvent);
            
            // Assert
            Assert.IsTrue(_workflowEventReceived, "OnWorkflowFinishedイベントが発火されること");
            Assert.IsNotNull(_receivedWorkflowEvent, "イベントデータが正しく渡されること");
            Assert.AreEqual("workflow_finished", _receivedWorkflowEvent.@event, "イベントタイプが正しく渡されること");
        }
        
        [Test]
        public void ProcessStreamEvent_未対応イベントタイプ_イベント発火されず警告ログ出力()
        {
            // Arrange
            _difyService.OnAudioMessage += (audioEvent) => _audioEventReceived = true;
            _difyService.OnTextMessage += (textEvent) => _textEventReceived = true;
            _difyService.OnWorkflowFinished += (workflowEvent) => _workflowEventReceived = true;
            
            LogAssert.Expect(LogType.Warning, "[DifyServiceWithEvents] Unhandled event type: unknown_event_type");
            
            var unknownEvent = new DifyStreamEvent
            {
                @event = "unknown_event_type",
                answer = "未知のイベント"
            };
            
            // Act
            _difyService.ProcessStreamEvent(unknownEvent);
            
            // Assert
            Assert.IsFalse(_audioEventReceived, "OnAudioMessageイベントが発火されないこと");
            Assert.IsFalse(_textEventReceived, "OnTextMessageイベントが発火されないこと");
            Assert.IsFalse(_workflowEventReceived, "OnWorkflowFinishedイベントが発火されないこと");
        }
        
        [Test]
        public void ProcessStreamEvent_複数イベント順次処理_すべて正しく配信される()
        {
            // Arrange
            int audioCount = 0, textCount = 0, workflowCount = 0;
            
            _difyService.OnAudioMessage += (audioEvent) => audioCount++;
            _difyService.OnTextMessage += (textEvent) => textCount++;
            _difyService.OnWorkflowFinished += (workflowEvent) => workflowCount++;
            
            var events = new[]
            {
                new DifyStreamEvent { @event = "message", answer = "メッセージ1" },
                new DifyStreamEvent { @event = "tts_message", audio = "YXVkaW8x" }, // "audio1" in base64
                new DifyStreamEvent { @event = "message", answer = "メッセージ2" },
                new DifyStreamEvent { @event = "workflow_finished", workflow_run_id = "wf-123" },
                new DifyStreamEvent { @event = "tts_message", audio = "YXVkaW8y" } // "audio2" in base64
            };
            
            // Act
            foreach (var streamEvent in events)
            {
                _difyService.ProcessStreamEvent(streamEvent);
            }
            
            // Assert
            Assert.AreEqual(2, textCount, "テキストメッセージが2回処理されること");
            Assert.AreEqual(2, audioCount, "音声メッセージが2回処理されること");
            Assert.AreEqual(1, workflowCount, "ワークフロー完了が1回処理されること");
        }
        
        [Test]
        public void EventSubscription_複数リスナー_すべてに配信される()
        {
            // Arrange
            bool listener1Called = false, listener2Called = false;
            
            _difyService.OnTextMessage += (textEvent) => listener1Called = true;
            _difyService.OnTextMessage += (textEvent) => listener2Called = true;
            
            var messageEvent = new DifyStreamEvent { @event = "message", answer = "テスト" };
            
            // Act
            _difyService.ProcessStreamEvent(messageEvent);
            
            // Assert
            Assert.IsTrue(listener1Called, "リスナー1が呼び出されること");
            Assert.IsTrue(listener2Called, "リスナー2が呼び出されること");
        }
    }
    
    /// <summary>
    /// テスト用のDifyService拡張クラス
    /// イベント配信機能を含む
    /// </summary>
    public class DifyServiceWithEvents : IDisposable
    {
        public event Action<DifyStreamEvent> OnAudioMessage;
        public event Action<DifyStreamEvent> OnTextMessage;
        public event Action<DifyStreamEvent> OnWorkflowFinished;
        
        /// <summary>
        /// ストリームイベントを処理してタイプ別にイベント発火
        /// </summary>
        /// <param name="streamEvent">受信したストリームイベント</param>
        public void ProcessStreamEvent(DifyStreamEvent streamEvent)
        {
            if (streamEvent == null) return;
            
            switch (streamEvent.@event)
            {
                case "tts_message":
                    OnAudioMessage?.Invoke(streamEvent);
                    break;
                    
                case "message":
                    OnTextMessage?.Invoke(streamEvent);
                    break;
                    
                case "workflow_finished":
                    OnWorkflowFinished?.Invoke(streamEvent);
                    break;
                    
                default:
                    // 未対応のイベントタイプは警告
                    Debug.LogWarning($"[DifyServiceWithEvents] Unhandled event type: {streamEvent.@event}");
                    break;
            }
        }
        
        public void Dispose()
        {
            // イベントリスナーをクリア
            OnAudioMessage = null;
            OnTextMessage = null;
            OnWorkflowFinished = null;
        }
    }
}