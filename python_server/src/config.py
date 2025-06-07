import pathlib
from typing import Any, Optional
from zoneinfo import ZoneInfo

from dotenv import load_dotenv
from pydantic import PostgresDsn, ValidationInfo, field_validator
from pydantic_settings import BaseSettings

load_dotenv(override=True)


class Settings(BaseSettings):
    """Settings for python_server"""

    YOUTUBE_API_KEY: Optional[str] = None
    GOOGLE_APPLICATION_CREDENTIALS: Optional[pathlib.Path] = None
    YT_ID: Optional[str] = None
    ELEVENLABS_API_KEY: Optional[str] = None
    AZURE_SPEECH_KEY: Optional[str] = None

    PROJECT_ROOT: pathlib.Path = pathlib.Path(__file__).resolve().parent.parent.parent
    AITUBER_3D_ROOT: pathlib.Path = PROJECT_ROOT / "aituber_3d"
    PYTHON_SERVER_ROOT: pathlib.Path = PROJECT_ROOT / "python_server"
    FAISS_QA_DB_DIR: pathlib.Path = PYTHON_SERVER_ROOT / "faiss_qa_db"
    FAISS_KNOWLEDGE_DB_DIR: pathlib.Path = PYTHON_SERVER_ROOT / "faiss_knowledge_manifest_demo_csv_db"
    BM25_KNOWLEDGE_DB_DIR: pathlib.Path = PYTHON_SERVER_ROOT / "bm25_knowledge_manifest_demo_csv_db"

    GOOGLE_DRIVE_FOLDER_ID: Optional[str] = None
    GOOGLE_API_KEY: Optional[str] = None

    # Postgres
    PG_HOST: str
    PG_PORT: int
    PG_USER: str
    PG_PASSWORD: str
    PG_DATABASE: str
    SQLALCHEMY_DATABASE_URI: PostgresDsn | None = None

    @field_validator("SQLALCHEMY_DATABASE_URI", mode="before")
    def assemble_db_connection(cls, v: str | None, info: ValidationInfo) -> Any:
        """Build PostgresDsn object."""
        if isinstance(v, str):
            return v
        return PostgresDsn.build(
            scheme="postgresql+psycopg",
            host=info.data.get("PG_HOST"),
            port=info.data.get("PG_PORT"),
            username=info.data.get("PG_USER"),
            password=info.data.get("PG_PASSWORD"),
            path=f"{info.data.get('PG_DATABASE') or ''}",
        )

    LOCAL_TZ: ZoneInfo = ZoneInfo("Asia/Tokyo")


settings = Settings()
