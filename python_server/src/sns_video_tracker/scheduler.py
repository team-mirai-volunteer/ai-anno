import logging
import schedule
import time
from datetime import datetime
from typing import List
from .video_tracker import VideoTracker
from .config import Config

logger = logging.getLogger(__name__)

class VideoTrackingScheduler:
    def __init__(self, credentials_path: str = None, spreadsheet_url: str = None):
        self.tracker = VideoTracker(credentials_path, spreadsheet_url)
        self.video_urls: List[str] = []
        
    def add_video_urls(self, urls: List[str]):
        self.video_urls.extend(urls)
        logger.info(f"Added {len(urls)} URLs to tracking list. Total: {len(self.video_urls)}")
    
    def load_urls_from_file(self, file_path: str):
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                urls = [line.strip() for line in f if line.strip() and not line.startswith('#')]
            self.add_video_urls(urls)
            logger.info(f"Loaded {len(urls)} URLs from {file_path}")
        except Exception as e:
            logger.error(f"Failed to load URLs from {file_path}: {e}")
            raise
    
    def update_all_videos(self):
        if not self.video_urls:
            logger.warning("No video URLs to update")
            return
        
        logger.info(f"Starting scheduled update of {len(self.video_urls)} videos")
        start_time = datetime.now()
        
        try:
            results = self.tracker.update_existing_videos(self.video_urls)
            end_time = datetime.now()
            duration = (end_time - start_time).total_seconds()
            
            logger.info(f"Scheduled update completed: {len(results)} videos updated in {duration:.2f} seconds")
            
        except Exception as e:
            logger.error(f"Scheduled update failed: {e}")
    
    def setup_daily_schedule(self, time_str: str = "09:00"):
        schedule.every().day.at(time_str).do(self.update_all_videos)
        logger.info(f"Daily update scheduled at {time_str}")
    
    def setup_hourly_schedule(self):
        schedule.every().hour.do(self.update_all_videos)
        logger.info("Hourly update scheduled")
    
    def setup_custom_schedule(self, interval_minutes: int):
        schedule.every(interval_minutes).minutes.do(self.update_all_videos)
        logger.info(f"Custom update scheduled every {interval_minutes} minutes")
    
    def run_scheduler(self):
        logger.info("Starting video tracking scheduler...")
        
        try:
            while True:
                schedule.run_pending()
                time.sleep(60)
                
        except KeyboardInterrupt:
            logger.info("Scheduler stopped by user")
        except Exception as e:
            logger.error(f"Scheduler error: {e}")
            raise

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='SNS Video Tracking Scheduler')
    parser.add_argument('--urls-file', required=True, help='File containing video URLs to track')
    parser.add_argument('--schedule-type', choices=['daily', 'hourly', 'custom'], 
                       default='daily', help='Schedule type')
    parser.add_argument('--time', default='09:00', help='Time for daily schedule (HH:MM)')
    parser.add_argument('--interval', type=int, default=60, 
                       help='Interval in minutes for custom schedule')
    parser.add_argument('--credentials', help='Path to Google credentials JSON file')
    parser.add_argument('--spreadsheet', help='Google Spreadsheet URL')
    
    args = parser.parse_args()
    
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )
    
    scheduler = VideoTrackingScheduler(args.credentials, args.spreadsheet)
    
    try:
        scheduler.load_urls_from_file(args.urls_file)
        
        if args.schedule_type == 'daily':
            scheduler.setup_daily_schedule(args.time)
        elif args.schedule_type == 'hourly':
            scheduler.setup_hourly_schedule()
        elif args.schedule_type == 'custom':
            scheduler.setup_custom_schedule(args.interval)
        
        scheduler.run_scheduler()
        
    except Exception as e:
        logger.error(f"Scheduler setup failed: {e}")
        return 1
    
    return 0

if __name__ == '__main__':
    exit(main())
