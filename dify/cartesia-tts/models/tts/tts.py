from collections.abc import Generator
from typing import Optional

import concurrent.futures
from dify_plugin import TTSModel
from dify_plugin.errors.model import (
    CredentialsValidateFailedError,
    InvokeBadRequestError,
    InvokeError,
    InvokeServerUnavailableError
)

from cartesia import Cartesia

class CartesiaTtsText2SpeechModel(TTSModel):
    """
    Model class for OpenAI Speech to text model.
    """

    def _invoke(
        self,
        model: str,
        tenant_id,
        credentials: dict,
        content_text: str,
        voice: str,
        user: Optional[str] = None,
    ) -> bytes | Generator[bytes, None, None]:
        api_key = credentials.get("cartesia_api_key")
        voice_id = credentials.get("voice_id")
        client = Cartesia(
            api_key=api_key,
        )

        return client.tts.bytes(
            model_id="sonic-2",
            transcript=content_text,
            voice={"id": voice_id},
            language="ja",
            output_format={
                "container": "mp3",
                "bit_rate": 192000,
                "sample_rate": 44100,
            },
        )

    def _tts_invoke(self, model: str, credentials: dict, content_text: str, voice: str) -> any:
        api_key = credentials.get("cartesia_api_key")
        voice_id = credentials.get("voice_id")
        max_workers = self._get_model_workers_limit(model, credentials)

        try:
            # Sentenceを句点で分割
            sentences = content_text.split("。")

            # Sentenceごとに非同期で処理
            with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
                futures = [executor.submit(self._process_sentence, sentence=sentence, api_key=api_key,
                                           voice_id=voice_id) for sentence in sentences]
                for future in futures:
                    yield future.result()

        except Exception as ex:
            raise InvokeBadRequestError(str(ex))
    
    def _process_sentence(self, sentence: str, api_key: str, voice_id: str):
        client = Cartesia(
            api_key=api_key,
        )

        return client.tts.bytes(
            model_id="sonic-2",
            transcript=sentence,
            voice={"id": voice_id},
            language="ja",
            output_format={
                "container": "mp3",
                "bit_rate": 192000,
                "sample_rate": 48000,
            },
        )
            
    def validate_credentials(
        self, model: str, credentials: dict, user: Optional[str] = None
    ) -> None:
        api_key = credentials.get("cartesia_api_key")
        voice_id = credentials.get("voice_id")

        if not api_key or not voice_id:
            raise CredentialsValidateFailedError(
                "Missing required credentials: 'cartesia_api_key' and 'voice_id'."
            )
        
        client = Cartesia(
            api_key=api_key,
        )

        voices = client.voices.list(
            limit=100
        )
        if not any(voice.id == voice_id for voice in voices):
            raise CredentialsValidateFailedError(
                f"Voice ID '{voice_id}' not found in the available voices."
            )

        try:
            pass
        except Exception as ex:
            raise CredentialsValidateFailedError(str(ex))
        
    @property
    def _invoke_error_mapping(self) -> dict[type[InvokeError], list[type[Exception]]]:
        # TODO: Break down the errors
        return {
            InvokeServerUnavailableError: [Exception],
        }