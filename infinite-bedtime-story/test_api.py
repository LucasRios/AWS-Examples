#!/usr/bin/env python3
"""
Test script for the Infinite Bedtime Stories API
Tests all endpoints to ensure proper integration
"""

import asyncio
import json
import os
from pathlib import Path
import httpx
import pytest

# Test configuration
API_BASE_URL = "http://localhost:8000"
TEST_TIMEOUT = 30

# Test data
TEST_STORY_REQUEST = {
    "heroName": "Luna",
    "heroAge": "7",
    "adventureTheme": "magical forest adventure",
    "awsCredentials": {
        "awsAccessKey": os.getenv("AWS_ACCESS_KEY_ID", "test_key"),
        "awsSecretKey": os.getenv("AWS_SECRET_ACCESS_KEY", "test_secret"),
        "awsRegion": os.getenv("AWS_REGION", "us-east-1")
    },
    "modelIds": {
        "text": "amazon.nova-micro-v1:0",
        "audio": "amazon.nova-2-sonic-v1:0",
        "image": "amazon.nova-canvas-v1:0"
    },
    "features": {
        "generateText": True,
        "generateVoice": False,  # Disabled for testing
        "generateImage": False,  # Disabled for testing
        "looping": False
    }
}

class APITester:
    """Test class for API endpoints."""
    
    def __init__(self):
        self.client = httpx.AsyncClient(timeout=TEST_TIMEOUT)
        self.base_url = API_BASE_URL
    
    async def test_health_check(self):
        """Test the health check endpoint."""
        print("🔍 Testing health check...")
        
        response = await self.client.get(f"{self.base_url}/health")
        assert response.status_code == 200
        
        data = response.json()
        assert data["status"] == "healthy"
        
        print("✅ Health check passed")
        return data
    
    async def test_root_endpoint(self):
        """Test the root endpoint."""
        print("🔍 Testing root endpoint...")
        
        response = await self.client.get(f"{self.base_url}/")
        assert response.status_code == 200
        
        data = response.json()
        assert "message" in data
        assert "Infinite Bedtime Stories" in data["message"]
        
        print("✅ Root endpoint passed")
        return data
    
    async def test_start_story(self):
        """Test starting a new story."""
        print("🔍 Testing story start...")
        
        response = await self.client.post(
            f"{self.base_url}/api/story/start",
            json=TEST_STORY_REQUEST
        )
        
        print(f"Response status: {response.status_code}")
        if response.status_code != 200:
            print(f"Response content: {response.text}")
        
        assert response.status_code == 200
        
        data = response.json()
        assert data["success"] is True
        assert "text_chunk" in data
        assert len(data["text_chunk"]) > 0
        assert "story_state" in data
        
        print("✅ Story start passed")
        print(f"📖 Generated text: {data['text_chunk'][:100]}...")
        return data
    
    async def test_continue_story(self):
        """Test continuing the story."""
        print("🔍 Testing story continuation...")
        
        # First start a story
        await self.test_start_story()
        
        # Then continue it
        response = await self.client.post(f"{self.base_url}/api/story/continue")
        assert response.status_code == 200
        
        data = response.json()
        assert data["success"] is True
        assert "text_chunk" in data
        assert len(data["text_chunk"]) > 0
        
        print("✅ Story continuation passed")
        print(f"📖 Continued text: {data['text_chunk'][:100]}...")
        return data
    
    async def test_story_interruption(self):
        """Test story interruption handling."""
        print("🔍 Testing story interruption...")
        
        # First start a story
        await self.test_start_story()
        
        # Then interrupt it
        interruption_data = {
            "keyword": "dragon!",
            "userInput": "A dragon appears!"
        }
        
        response = await self.client.post(
            f"{self.base_url}/api/story/interrupt",
            json=interruption_data
        )
        assert response.status_code == 200
        
        data = response.json()
        assert data["success"] is True
        assert "text_chunk" in data
        
        print("✅ Story interruption passed")
        print(f"🐉 Interrupted text: {data['text_chunk'][:100]}...")
        return data
    
    async def test_story_state(self):
        """Test getting story state."""
        print("🔍 Testing story state...")
        
        # First start a story
        await self.test_start_story()
        
        # Then get state
        response = await self.client.get(f"{self.base_url}/api/story/state")
        assert response.status_code == 200
        
        data = response.json()
        assert "story_state" in data
        assert "agent_active" in data
        assert data["agent_active"] is True
        
        print("✅ Story state passed")
        return data
    
    async def test_story_reset(self):
        """Test resetting the story."""
        print("🔍 Testing story reset...")
        
        # First start a story
        await self.test_start_story()
        
        # Then reset it
        response = await self.client.post(f"{self.base_url}/api/story/reset")
        assert response.status_code == 200
        
        data = response.json()
        assert "message" in data
        
        print("✅ Story reset passed")
        return data
    
    async def run_all_tests(self):
        """Run all API tests."""
        print("🧪 Starting API tests...")
        print("=" * 50)
        
        try:
            await self.test_health_check()
            await self.test_root_endpoint()
            await self.test_start_story()
            await self.test_continue_story()
            await self.test_story_interruption()
            await self.test_story_state()
            await self.test_story_reset()
            
            print("\n" + "=" * 50)
            print("🎉 All tests passed!")
            print("✅ API is working correctly")
            
        except Exception as e:
            print(f"\n❌ Test failed: {e}")
            raise
        
        finally:
            await self.client.aclose()

async def main():
    """Main test function."""
    print("🌙 Testing Infinite Bedtime Stories API")
    
    # Check if API server is running
    try:
        async with httpx.AsyncClient() as client:
            response = await client.get(f"{API_BASE_URL}/health", timeout=5)
            if response.status_code != 200:
                raise Exception("API not responding")
    except Exception:
        print("❌ API server not running!")
        print("💡 Start the server first: python start_app.py")
        return
    
    # Run tests
    tester = APITester()
    await tester.run_all_tests()

if __name__ == "__main__":
    asyncio.run(main())