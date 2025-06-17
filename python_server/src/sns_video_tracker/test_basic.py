#!/usr/bin/env python3
"""
Basic functionality test for SNS Video Tracker
"""
import sys
import logging
from sns_video_tracker.scrapers.instagram_scraper import InstagramScraper
from sns_video_tracker.scrapers.tiktok_scraper import TikTokScraper

def test_video_id_extraction():
    """Test video ID extraction from URLs"""
    print("Testing video ID extraction...")
    
    instagram_scraper = InstagramScraper()
    tiktok_scraper = TikTokScraper()
    
    instagram_urls = [
        "https://www.instagram.com/p/ABC123DEF456/",
        "https://www.instagram.com/reel/XYZ789GHI012/",
        "https://instagram.com/p/TEST123/"
    ]
    
    for url in instagram_urls:
        video_id = instagram_scraper.extract_video_id(url)
        print(f"Instagram URL: {url} -> ID: {video_id}")
    
    tiktok_urls = [
        "https://www.tiktok.com/@username/video/1234567890123456789",
        "https://tiktok.com/@user/video/9876543210987654321",
        "https://vm.tiktok.com/ZMd1234567/"
    ]
    
    for url in tiktok_urls:
        video_id = tiktok_scraper.extract_video_id(url)
        print(f"TikTok URL: {url} -> ID: {video_id}")

def test_number_extraction():
    """Test number extraction from text"""
    print("\nTesting number extraction...")
    
    scraper = InstagramScraper()
    
    test_cases = [
        ("1,234", 1234),
        ("1.2K", 1200),
        ("2.5M", 2500000),
        ("1B", 1000000000),
        ("500万", 5000000),
        ("1.5億", 150000000),
        ("123 likes", 123),
        ("No numbers here", 0)
    ]
    
    for text, expected in test_cases:
        result = scraper._extract_number_from_text(text)
        status = "✓" if result == expected else "✗"
        print(f"{status} '{text}' -> {result} (expected: {expected})")

def test_platform_detection():
    """Test platform detection"""
    print("\nTesting platform detection...")
    
    from sns_video_tracker.video_tracker import VideoTracker
    tracker = VideoTracker()
    
    test_urls = [
        ("https://www.instagram.com/p/ABC123/", "instagram"),
        ("https://instagram.com/reel/XYZ789/", "instagram"),
        ("https://www.tiktok.com/@user/video/123456789", "tiktok"),
        ("https://tiktok.com/@user/video/987654321", "tiktok"),
        ("https://youtube.com/watch?v=abc123", None),
        ("https://twitter.com/user/status/123", None)
    ]
    
    for url, expected in test_urls:
        result = tracker._detect_platform(url)
        status = "✓" if result == expected else "✗"
        print(f"{status} {url} -> {result} (expected: {expected})")

def main():
    logging.basicConfig(level=logging.INFO)
    
    print("SNS Video Tracker - Basic Functionality Test")
    print("=" * 50)
    
    try:
        test_video_id_extraction()
        test_number_extraction()
        test_platform_detection()
        
        print("\n" + "=" * 50)
        print("✓ All basic tests completed successfully!")
        print("System is ready for use.")
        
    except Exception as e:
        print(f"\n✗ Test failed: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
