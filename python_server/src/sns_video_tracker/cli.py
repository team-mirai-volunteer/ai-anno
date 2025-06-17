#!/usr/bin/env python3
import argparse
import logging
import sys
from pathlib import Path
from typing import List
from .config import Config
from .video_tracker import VideoTracker

def setup_logging(level: str = "INFO"):
    logging.basicConfig(
        level=getattr(logging, level.upper()),
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(sys.stdout),
            logging.FileHandler('sns_video_tracker.log')
        ]
    )

def read_urls_from_file(file_path: str) -> List[str]:
    urls = []
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#'):
                    urls.append(line)
        return urls
    except FileNotFoundError:
        print(f"Error: File {file_path} not found")
        sys.exit(1)
    except Exception as e:
        print(f"Error reading file {file_path}: {e}")
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='SNS Video Tracker - Track Instagram/TikTok video metrics')
    
    parser.add_argument('--credentials', '-c', 
                       help='Path to Google credentials JSON file',
                       default=Config.GOOGLE_CREDENTIALS_PATH)
    
    parser.add_argument('--spreadsheet', '-s',
                       help='Google Spreadsheet URL',
                       default=Config.SPREADSHEET_URL)
    
    parser.add_argument('--sheet-name', 
                       help='Worksheet name in the spreadsheet',
                       default='SNS_Video_Data')
    
    parser.add_argument('--log-level', 
                       choices=['DEBUG', 'INFO', 'WARNING', 'ERROR'],
                       default=Config.LOG_LEVEL,
                       help='Logging level')
    
    subparsers = parser.add_subparsers(dest='command', help='Available commands')
    
    init_parser = subparsers.add_parser('init', help='Initialize spreadsheet with headers')
    
    track_parser = subparsers.add_parser('track', help='Track video(s)')
    track_group = track_parser.add_mutually_exclusive_group(required=True)
    track_group.add_argument('--url', help='Single video URL to track')
    track_group.add_argument('--file', help='File containing video URLs (one per line)')
    track_group.add_argument('--urls', nargs='+', help='Multiple video URLs')
    
    update_parser = subparsers.add_parser('update', help='Update existing video data')
    update_group = update_parser.add_mutually_exclusive_group(required=True)
    update_group.add_argument('--url', help='Single video URL to update')
    update_group.add_argument('--file', help='File containing video URLs (one per line)')
    update_group.add_argument('--urls', nargs='+', help='Multiple video URLs')
    
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        sys.exit(1)
    
    setup_logging(args.log_level)
    
    if not Config.validate():
        print("Configuration validation failed. Please check your environment variables and credentials.")
        sys.exit(1)
    
    try:
        tracker = VideoTracker(args.credentials, args.spreadsheet)
        
        if args.command == 'init':
            tracker.initialize_spreadsheet(args.sheet_name)
            print(f"Spreadsheet initialized successfully with sheet: {args.sheet_name}")
            
        elif args.command == 'track':
            urls = []
            if args.url:
                urls = [args.url]
            elif args.file:
                urls = read_urls_from_file(args.file)
            elif args.urls:
                urls = args.urls
            
            print(f"Tracking {len(urls)} video(s)...")
            
            if len(urls) == 1:
                result = tracker.track_single_video(urls[0])
                print("Video tracked successfully!")
                print(tracker.get_video_summary(result))
            else:
                results = tracker.track_multiple_videos(urls)
                print(f"Successfully tracked {len(results)} out of {len(urls)} videos")
                for result in results:
                    print(tracker.get_video_summary(result))
                    
        elif args.command == 'update':
            urls = []
            if args.url:
                urls = [args.url]
            elif args.file:
                urls = read_urls_from_file(args.file)
            elif args.urls:
                urls = args.urls
            
            print(f"Updating {len(urls)} video(s)...")
            results = tracker.update_existing_videos(urls)
            print(f"Successfully updated {len(results)} videos")
            for result in results:
                print(tracker.get_video_summary(result))
                
    except KeyboardInterrupt:
        print("\nOperation cancelled by user")
        sys.exit(1)
    except Exception as e:
        print(f"Error: {e}")
        logging.error(f"Application error: {e}", exc_info=True)
        sys.exit(1)

if __name__ == '__main__':
    main()
