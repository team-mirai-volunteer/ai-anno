import logging
import re
from datetime import datetime
from typing import Dict, Any
from selenium.webdriver.common.by import By
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, NoSuchElementException
from .base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class TikTokScraper(BaseScraper):
    def extract_video_id(self, url: str) -> str:
        patterns = [
            r'/video/(\d+)',
            r'@[^/]+/video/(\d+)',
            r'/v/(\d+)'
        ]
        
        for pattern in patterns:
            match = re.search(pattern, url)
            if match:
                return match.group(1)
        
        return ""
    
    def scrape_video_data(self, url: str) -> Dict[str, Any]:
        try:
            logger.info(f"Scraping TikTok video: {url}")
            self.driver.get(url)
            self._wait_for_page_load()
            
            video_data = {
                "timestamp": datetime.now().isoformat(),
                "platform": "TikTok",
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
                author_selectors = [
                    "[data-e2e='browse-username']",
                    "h2[data-e2e='browse-username']",
                    ".author-uniqueId"
                ]
                
                for selector in author_selectors:
                    try:
                        author_element = self.wait.until(
                            EC.presence_of_element_located((By.CSS_SELECTOR, selector))
                        )
                        video_data["author"] = author_element.text.strip().replace('@', '')
                        break
                    except TimeoutException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not find author information: {e}")
            
            try:
                title_selectors = [
                    "[data-e2e='browse-video-desc']",
                    ".video-meta-caption",
                    "div[data-e2e='video-desc']"
                ]
                
                for selector in title_selectors:
                    try:
                        title_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        video_data["title"] = title_element.text.strip()[:200]
                        break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not find title/description: {e}")
            
            try:
                like_selectors = [
                    "[data-e2e='like-count']",
                    "[data-e2e='browse-like-count']",
                    "strong[data-e2e='like-count']"
                ]
                
                for selector in like_selectors:
                    try:
                        like_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        like_text = like_element.text.strip()
                        if like_text:
                            video_data["like_count"] = self._extract_number_from_text(like_text)
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract like count: {e}")
            
            try:
                comment_selectors = [
                    "[data-e2e='comment-count']",
                    "[data-e2e='browse-comment-count']",
                    "strong[data-e2e='comment-count']"
                ]
                
                for selector in comment_selectors:
                    try:
                        comment_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        comment_text = comment_element.text.strip()
                        if comment_text:
                            video_data["comment_count"] = self._extract_number_from_text(comment_text)
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract comment count: {e}")
            
            try:
                share_selectors = [
                    "[data-e2e='share-count']",
                    "[data-e2e='browse-share-count']",
                    "strong[data-e2e='share-count']"
                ]
                
                for selector in share_selectors:
                    try:
                        share_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        share_text = share_element.text.strip()
                        if share_text:
                            video_data["share_count"] = self._extract_number_from_text(share_text)
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract share count: {e}")
            
            try:
                view_selectors = [
                    "[data-e2e='video-views']",
                    "strong[data-e2e='video-views']",
                    ".video-count"
                ]
                
                for selector in view_selectors:
                    try:
                        view_element = self.driver.find_element(By.CSS_SELECTOR, selector)
                        view_text = view_element.text.strip()
                        if view_text and any(keyword in view_text.lower() for keyword in ['view', '回視聴', '조회']):
                            video_data["view_count"] = self._extract_number_from_text(view_text)
                            break
                    except NoSuchElementException:
                        continue
                        
            except Exception as e:
                logger.warning(f"Could not extract view count: {e}")
            
            logger.info(f"Successfully scraped TikTok video data: {video_data['video_id']}")
            return video_data
            
        except Exception as e:
            logger.error(f"Failed to scrape TikTok video {url}: {e}")
            raise
