"""
FastAPI Backend for Infinite Bedtime Stories
Connects React frontend with Strands storytelling agent
"""

import asyncio
import os
from typing import Dict, Any, Optional
from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
import uvicorn

from agent_logic import StorytellingAgent
from story_state import StoryState
 
os.makedirs("files", exist_ok=True) 

# Initialize FastAPI app
app = FastAPI(
    title="Infinite Bedtime Stories API",
    description="Backend API for magical storytelling with AWS Nova models",
    version="1.0.0"
)

# CORS middleware for React frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://127.0.0.1:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Serve static files (generated audio/images)
app.mount("/static", StaticFiles(directory="static"), name="static")

# Global agent instance
current_agent: Optional[StorytellingAgent] = None
current_story_state: Optional[StoryState] = None

# Request/Response Models
class StoryRequest(BaseModel):
    heroName: str
    heroAge: str
    adventureTheme: str
    awsCredentials: Dict[str, str]
    modelIds: Dict[str, str]
    features: Dict[str, bool]
    maxInteractions: int = 10  # Default to 10 interactions

class InterruptionRequest(BaseModel):
    keyword: str
    userInput: str

class StoryResponse(BaseModel):
    text_chunk: str
    audio_url: Optional[str] = None
    image_url: Optional[str] = None
    story_state: Dict[str, Any]
    success: bool = True
    error: Optional[str] = None

class HealthResponse(BaseModel):
    status: str
    message: str
    agent_active: bool

# API Endpoints

@app.get("/", response_model=Dict[str, str])
async def root():
    """Root endpoint with API information."""
    return {
        "message": "🌙 Infinite Bedtime Stories API",
        "version": "1.0.0",
        "docs": "/docs",
        "frontend": "http://localhost:3000"
    }

@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint."""
    return HealthResponse(
        status="healthy",
        message="API is running smoothly",
        agent_active=current_agent is not None
    )

@app.post("/api/story/start", response_model=StoryResponse)
async def start_story(request: StoryRequest):
    """Initialize a new story with the given configuration."""
    global current_agent, current_story_state
    
    try:
        # Set AWS credentials from request
        os.environ['AWS_ACCESS_KEY_ID'] = request.awsCredentials.get('awsAccessKey', '')
        os.environ['AWS_SECRET_ACCESS_KEY'] = request.awsCredentials.get('awsSecretKey', '')
        os.environ['AWS_DEFAULT_REGION'] = request.awsCredentials.get('awsRegion', 'us-east-1')
        
        # Create initial story state
        initial_plot = f"A brave {request.heroAge}-year-old named {request.heroName} begins a magical adventure about {request.adventureTheme}"
        
        current_story_state = StoryState(
            hero_name=request.heroName,
            current_plot_summary=initial_plot,
            last_sentence="",
            mood="curious",
            max_interactions=request.maxInteractions
        )
        
        # Initialize storytelling agent
        current_agent = StorytellingAgent(current_story_state)
        
        # Generate first story segment
        story_text = 'teste'

        if request.features.get('generateText', False): 
            story_text = current_agent.generate_story_segment(
                current_story_state.current_plot_summary,
                current_story_state.hero_name,
                current_story_state.mood
            )
        else:
            story_text = 'teste'

        # Generate multimedia content if enabled
        audio_url = None
        image_url = None
        
        if request.features.get('generateVoice', False):
            audio_file = current_agent.generate_audio(story_text, current_story_state.mood)
            audio_url = f"static/{audio_file}" if audio_file else None
        else:
            audio_url = f"static/story_audio.mp3"           
        
        if request.features.get('generateImage', False):
            image_file = current_agent.generate_scene(story_text)
            image_url = f"static/{image_file}" if image_file else None
        else:
            image_url = f"static/scene_illustration.png"           
        
        return StoryResponse(
            text_chunk=story_text,
            audio_url=audio_url,
            image_url=image_url,
            story_state={
                **current_story_state.dict(),
                "is_complete": current_story_state.is_story_complete()
            }
        )
        
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to start story: {str(e)}"
        )

class ContinueRequest(BaseModel):
    features: Dict[str, bool]

@app.post("/api/story/continue", response_model=StoryResponse)
async def continue_story(request: ContinueRequest):
    """Generate the next segment of the current story."""
    global current_agent, current_story_state
    
    if not current_agent or not current_story_state:
        raise HTTPException(
            status_code=400,
            detail="No active story session. Please start a new story first."
        )
    
    try:
        # Generate next story segment
        story_text = 'test'
        
        if request.features.get('generateText', False):
            story_text = current_agent.generate_story_segment(
                current_story_state.current_plot_summary,
                current_story_state.hero_name,
                current_story_state.mood
            )
            print(f"📝 Generated new story text: {story_text[:80]}...")
        
        # Generate multimedia content
        audio_url = None
        image_url = None
        
        if request.features.get('generateVoice', False):
            print(f"🎤 Generating audio for: {story_text[:50]}...")
            audio_file = current_agent.generate_audio(story_text, current_story_state.mood)
            audio_url = f"static/{audio_file}" if audio_file else None
            print(f"✅ Audio URL: {audio_url}")
        else:
            audio_url = f"static/story_audio.mp3"
        
        if request.features.get('generateImage', False):
            print(f"🖼️ Generating image for: {story_text[:50]}...")
            image_file = current_agent.generate_scene(story_text)
            image_url = f"static/{image_file}" if image_file else None
            print(f"✅ Image URL: {image_url}")
        else:
            image_url = f"static/scene_illustration.png"
        
        return StoryResponse(
            text_chunk=story_text,
            audio_url=audio_url,
            image_url=image_url,
            story_state={
                **current_story_state.dict(),
                "is_complete": current_story_state.is_story_complete()
            }
        )
        
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to continue story: {str(e)}"
        )

@app.post("/api/story/interrupt", response_model=StoryResponse)
async def handle_interruption(request: InterruptionRequest):
    """Handle user interruption and modify the story."""
    global current_agent, current_story_state
    
    if not current_agent or not current_story_state:
        raise HTTPException(
            status_code=400,
            detail="No active story session. Please start a new story first."
        )
    
    try:
        # Handle the interruption
        await current_agent.handle_interruption(request.keyword, request.userInput)
        
        # Generate new story segment based on interruption
        story_text = current_agent.generate_story_segment(
            current_story_state.current_plot_summary,
            current_story_state.hero_name,
            current_story_state.mood
        )
        
        # Generate multimedia content
        audio_file = current_agent.generate_audio(story_text, current_story_state.mood)
        image_file = current_agent.generate_scene(story_text)
        
        return StoryResponse(
            text_chunk=story_text,
            audio_url=f"/static/{audio_file}" if audio_file else None,
            image_url=f"/static/{image_file}" if image_file else None,
            story_state=current_story_state.dict()
        )
        
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to handle interruption: {str(e)}"
        )

@app.post("/api/story/reset")
async def reset_story():
    """Reset the current story session."""
    global current_agent, current_story_state
    
    current_agent = None
    current_story_state = None
    
    return {"message": "Story session reset successfully"}

@app.get("/api/story/state")
async def get_story_state():
    """Get the current story state."""
    if not current_story_state:
        raise HTTPException(
            status_code=400,
            detail="No active story session"
        )
    
    return {
        "story_state": current_story_state.dict(),
        "agent_active": current_agent is not None
    }

# Background task for cleanup
async def cleanup_old_files():
    """Clean up old generated files periodically."""
    import glob
    import time
    
    while True:
        try:
            # Clean files older than 1 hour
            current_time = time.time()
            for file_pattern in ["static/*.mp3", "static/*.png", "static/*.jpg"]:
                for file_path in glob.glob(file_pattern):
                    if os.path.getmtime(file_path) < current_time - 3600:  # 1 hour
                        os.remove(file_path)
                        print(f"Cleaned up old file: {file_path}")
        except Exception as e:
            print(f"Cleanup error: {e}")
        
        # Wait 30 minutes before next cleanup
        await asyncio.sleep(1800)

@app.on_event("startup")
async def startup_event():
    """Initialize the API server."""
    # Create static directory if it doesn't exist
    os.makedirs("static", exist_ok=True)
    
    # Start background cleanup task
    asyncio.create_task(cleanup_old_files())
    
    print("🌙 Infinite Bedtime Stories API started!")
    print("📖 Ready to create magical stories...")

@app.on_event("shutdown")
async def shutdown_event():
    """Cleanup on server shutdown."""
    global current_agent, current_story_state
    current_agent = None
    current_story_state = None
    print("👋 API server shutting down...")

if __name__ == "__main__":
    uvicorn.run(
        "api_server:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info"
    )