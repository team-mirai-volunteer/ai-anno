import logging
import re
from datetime import datetime
from typing import Dict, Any
from selenium.webdriver.common.by import By
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, NoSuchElementException
from .base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class InstagramScraper(BaseScraper):
    def extract_video_id(self, url: str) -> str:
        patterns = [
            r'/p/([A-Za-z0-9_-]+)',
            r'/reel/([A-Za-z0-9_-]+)',
            r'/tv/([A-Za-z0-9_-]+)'
        ]
        
        for pattern in patterns:
            match = re.search(pattern, url)
            if match:
                return match.group(1)
        
        return ""
    
    def scrape_video_data(self, url: str) -> Dict[str, Any]:
        try:
            logger.info(f"Scraping Instagram video: {url}")
            self.driver.get(url)
            self._wait_for_page_load()
            
            video_data = {
                "timestamp": datetime.now().isoformat(),
                "platform": "Instagram",
                "video_url": url,
                "video_id": self.extract_video_id(url),
                "title": "",
                "view_count": 0,
                "like_count": 0,
                "comment_count": 0,
                "share_count": 0,
                "author": "",
                "duration": "",
                "upload_date": "",
                "last_updated": datetime.now().isoformat()
            }
            
            try:
                author_element = self.wait.until(
                    EC.presence_of_element_located((By.CSS_SELECTOR, "header a"))
                )
                video_data["author"] = author_element.text.strip()
            except TimeoutException:
                logger.warning("Could not find author information")
            
            try:
                caption_element = self.driver.find_element(By.CSS_SELECTOR, "article div[data-testid='post-caption'] span")
                video_data["title"] = caption_element.text.strip()[:200]
            except NoSuchElementException:
                logger.warning("Could not find caption/title")
            
            try:
                like_selectors = [
                    "section button span",
                    "article section span",
                    "[data-testid='like-count']"
                ]
                
                for selector in like_selectors:
                    try:
                        like_elements = self.driver.find_elements(By.CSS_SELECTOR, selector)
                        for element in like_elements:
                            text = element.text.strip()
                            if any(keyword in text.lower() for keyword in ['like', 'いいね', '좋아요']):
                                video_data["like_count"] = self._extract_number_from_text(text)
                                break
                        if video_data["like_count"] > 0:
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract like count: {e}")
            
            try:
                view_selectors = [
                    "span[title*='view']",
                    "span[title*='再生']",
                    "div[data-testid='video-view-count']"
                ]
                
                for selector in view_selectors:
                    try:
                        view_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        view_text = view_element.get_attribute('title') or view_element.text
                        if view_text:
                            video_data["view_count"] = self._extract_number_from_text(view_text)
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract view count: {e}")
            
            try:
                comment_selectors = [
                    "section button span",
                    "[data-testid='comment-count']"
                ]
                
                for selector in comment_selectors:
                    try:
                        comment_elements = self.driver.find_elements(By.CSS_SELECTOR, selector)
                        for element in comment_elements:
                            text = element.text.strip()
                            if any(keyword in text.lower() for keyword in ['comment', 'コメント', '댓글']):
                                video_data["comment_count"] = self._extract_number_from_text(text)
                                break
                        if video_data["comment_count"] > 0:
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract comment count: {e}")
            
            try:
                time_element = self.driver.find_element(By.CSS_SELECTOR, "time")
                video_data["upload_date"] = time_element.get_attribute('datetime') or time_element.text
            except NoSuchElementException:
                logger.warning("Could not find upload date")
            
            logger.info(f"Successfully scraped Instagram video data: {video_data['video_id']}")
            return video_data
            
        except Exception as e:
            logger.error(f"Failed to scrape Instagram video {url}: {e}")
            raise
