import logging
from collections.abc import Mapping

from cartesia import Cartesia
from dify_plugin import ModelProvider
from dify_plugin.entities.model import ModelType
from dify_plugin.errors.model import CredentialsValidateFailedError

logger = logging.getLogger(__name__)


class CartesiaTtsModelProvider(ModelProvider):
    def validate_provider_credentials(self, credentials: Mapping) -> None:
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
        except CredentialsValidateFailedError as ex:
            raise ex
        except Exception as ex:
            logger.exception(
                f"{self.get_provider_schema().provider} credentials validate failed"
            )
            raise ex
