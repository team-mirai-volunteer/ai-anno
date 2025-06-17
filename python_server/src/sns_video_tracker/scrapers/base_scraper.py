import logging
import time
from abc import ABC, abstractmethod
from typing import Dict, Any, Optional
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, NoSuchElementException
from webdriver_manager.chrome import ChromeDriverManager
from ..config import Config

logger = logging.getLogger(__name__)

class BaseScraper(ABC):
    def __init__(self):
        self.driver: Optional[webdriver.Chrome] = None
        self.wait: Optional[WebDriverWait] = None
        
    def _setup_driver(self):
        chrome_options = Options()
        if Config.HEADLESS_MODE:
            chrome_options.add_argument("--headless")
        chrome_options.add_argument("--no-sandbox")
        chrome_options.add_argument("--disable-dev-shm-usage")
        chrome_options.add_argument("--disable-gpu")
        chrome_options.add_argument("--window-size=1920,1080")
        chrome_options.add_argument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36")
        
        try:
            if Config.CHROME_DRIVER_PATH:
                service = Service(Config.CHROME_DRIVER_PATH)
            else:
                service = Service(ChromeDriverManager().install())
                
            self.driver = webdriver.Chrome(service=service, options=chrome_options)
            self.wait = WebDriverWait(self.driver, 10)
            logger.info("Chrome driver initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize Chrome driver: {e}")
            raise
    
    def _cleanup_driver(self):
        if self.driver:
            try:
                self.driver.quit()
                logger.info("Chrome driver cleaned up")
            except Exception as e:
                logger.error(f"Error cleaning up driver: {e}")
            finally:
                self.driver = None
                self.wait = None
    
    def _wait_for_page_load(self, timeout: int = 10):
        try:
            self.wait.until(lambda driver: driver.execute_script("return document.readyState") == "complete")
            time.sleep(Config.REQUEST_DELAY)
        except TimeoutException:
            logger.warning("Page load timeout, continuing anyway")
    
    def _extract_number_from_text(self, text: str) -> int:
        if not text:
            return 0
            
        text = text.lower().replace(',', '').replace(' ', '')
        
        multipliers = {
            'k': 1000,
            'm': 1000000,
            'b': 1000000000,
            '万': 10000,
            '億': 100000000
        }
        
        for suffix, multiplier in multipliers.items():
            if suffix in text:
                try:
                    number_part = text.replace(suffix, '')
                    return int(float(number_part) * multiplier)
                except ValueError:
                    continue
        
        try:
            return int(''.join(filter(str.isdigit, text)))
        except ValueError:
            return 0
    
    @abstractmethod
    def extract_video_id(self, url: str) -> str:
        pass
    
    @abstractmethod
    def scrape_video_data(self, url: str) -> Dict[str, Any]:
        pass
    
    def scrape_with_retry(self, url: str) -> Dict[str, Any]:
        last_exception = None
        
        for attempt in range(Config.MAX_RETRIES):
            try:
                if not self.driver:
                    self._setup_driver()
                
                result = self.scrape_video_data(url)
                if result:
                    return result
                    
            except Exception as e:
                last_exception = e
                logger.warning(f"Scraping attempt {attempt + 1} failed for {url}: {e}")
                
                self._cleanup_driver()
                
                if attempt < Config.MAX_RETRIES - 1:
                    time.sleep(2 ** attempt)
        
        logger.error(f"All scraping attempts failed for {url}")
        if last_exception:
            raise last_exception
        else:
            raise Exception(f"Failed to scrape data from {url}")
    
    def __enter__(self):
        self._setup_driver()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self._cleanup_driver()
