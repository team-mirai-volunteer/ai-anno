import os
from pathlib import Path
from typing import Optional

class Config:
    GOOGLE_CREDENTIALS_PATH: str = os.getenv("GOOGLE_CREDENTIALS_PATH", "gcp_credentials.json")
    SPREADSHEET_URL: Optional[str] = os.getenv("SPREADSHEET_URL")
    
    CHROME_DRIVER_PATH: Optional[str] = os.getenv("CHROME_DRIVER_PATH")
    HEADLESS_MODE: bool = os.getenv("HEADLESS_MODE", "true").lower() == "true"
    
    REQUEST_DELAY: float = float(os.getenv("REQUEST_DELAY", "2.0"))
    MAX_RETRIES: int = int(os.getenv("MAX_RETRIES", "3"))
    
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    
    @classmethod
    def validate(cls) -> bool:
        if not cls.SPREADSHEET_URL:
            print("Warning: SPREADSHEET_URL not set in environment variables")
            return False
        
        credentials_path = Path(cls.GOOGLE_CREDENTIALS_PATH)
        if not credentials_path.exists():
            print(f"Warning: Google credentials file not found at {cls.GOOGLE_CREDENTIALS_PATH}")
            return False
            
        return True
