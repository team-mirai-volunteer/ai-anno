import logging
from typing import List, Dict, Any, Optional
import gspread
from google.oauth2.service_account import Credentials
from .config import Config

logger = logging.getLogger(__name__)

class SpreadsheetClient:
    def __init__(self, credentials_path: str = None, spreadsheet_url: str = None):
        self.credentials_path = credentials_path or Config.GOOGLE_CREDENTIALS_PATH
        self.spreadsheet_url = spreadsheet_url or Config.SPREADSHEET_URL
        self._client: Optional[gspread.Client] = None
        self._worksheet: Optional[gspread.Worksheet] = None
        
    def _get_client(self) -> gspread.Client:
        if self._client is None:
            credentials = Credentials.from_service_account_file(
                self.credentials_path, 
                scopes=[
                    "https://www.googleapis.com/auth/spreadsheets",
                    "https://www.googleapis.com/auth/drive"
                ]
            )
            self._client = gspread.authorize(credentials)
        return self._client
    
    def _get_worksheet(self, sheet_name: str = None) -> gspread.Worksheet:
        if self._worksheet is None:
            client = self._get_client()
            spreadsheet = client.open_by_url(self.spreadsheet_url)
            if sheet_name:
                try:
                    self._worksheet = spreadsheet.worksheet(sheet_name)
                except gspread.WorksheetNotFound:
                    self._worksheet = spreadsheet.add_worksheet(title=sheet_name, rows=1000, cols=20)
            else:
                self._worksheet = spreadsheet.sheet1
        return self._worksheet
    
    def initialize_headers(self, sheet_name: str = "SNS_Video_Data"):
        worksheet = self._get_worksheet(sheet_name)
        headers = [
            "Timestamp",
            "Platform", 
            "Video_URL",
            "Video_ID",
            "Title",
            "View_Count",
            "Like_Count", 
            "Comment_Count",
            "Share_Count",
            "Author",
            "Duration",
            "Upload_Date",
            "Last_Updated"
        ]
        
        try:
            existing_headers = worksheet.row_values(1)
            if not existing_headers or existing_headers != headers:
                worksheet.clear()
                worksheet.append_row(headers)
                logger.info(f"Initialized headers in worksheet: {sheet_name}")
        except Exception as e:
            logger.error(f"Failed to initialize headers: {e}")
            raise
    
    def append_video_data(self, video_data: Dict[str, Any], sheet_name: str = "SNS_Video_Data"):
        worksheet = self._get_worksheet(sheet_name)
        
        row_data = [
            video_data.get("timestamp", ""),
            video_data.get("platform", ""),
            video_data.get("video_url", ""),
            video_data.get("video_id", ""),
            video_data.get("title", ""),
            video_data.get("view_count", 0),
            video_data.get("like_count", 0),
            video_data.get("comment_count", 0),
            video_data.get("share_count", 0),
            video_data.get("author", ""),
            video_data.get("duration", ""),
            video_data.get("upload_date", ""),
            video_data.get("last_updated", "")
        ]
        
        try:
            worksheet.append_row(row_data)
            logger.info(f"Appended data for video: {video_data.get('video_url', 'Unknown')}")
        except Exception as e:
            logger.error(f"Failed to append video data: {e}")
            raise
    
    def batch_append_video_data(self, video_data_list: List[Dict[str, Any]], sheet_name: str = "SNS_Video_Data"):
        if not video_data_list:
            return
            
        worksheet = self._get_worksheet(sheet_name)
        
        rows_data = []
        for video_data in video_data_list:
            row_data = [
                video_data.get("timestamp", ""),
                video_data.get("platform", ""),
                video_data.get("video_url", ""),
                video_data.get("video_id", ""),
                video_data.get("title", ""),
                video_data.get("view_count", 0),
                video_data.get("like_count", 0),
                video_data.get("comment_count", 0),
                video_data.get("share_count", 0),
                video_data.get("author", ""),
                video_data.get("duration", ""),
                video_data.get("upload_date", ""),
                video_data.get("last_updated", "")
            ]
            rows_data.append(row_data)
        
        try:
            worksheet.append_rows(rows_data)
            logger.info(f"Batch appended {len(rows_data)} video records")
        except Exception as e:
            logger.error(f"Failed to batch append video data: {e}")
            raise
    
    def update_existing_video(self, video_url: str, updated_data: Dict[str, Any], sheet_name: str = "SNS_Video_Data"):
        worksheet = self._get_worksheet(sheet_name)
        
        try:
            all_records = worksheet.get_all_records()
            for i, record in enumerate(all_records, start=2):
                if record.get("Video_URL") == video_url:
                    for key, value in updated_data.items():
                        col_name = key.replace("_", "_").title()
                        if col_name in record:
                            col_index = list(record.keys()).index(col_name) + 1
                            worksheet.update_cell(i, col_index, value)
                    logger.info(f"Updated existing video: {video_url}")
                    return True
            
            logger.warning(f"Video not found for update: {video_url}")
            return False
            
        except Exception as e:
            logger.error(f"Failed to update existing video: {e}")
            raise
