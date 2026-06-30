"""
Story State Management for Infinite Bedtime Stories
Handles the current state of the interactive story.
"""

from pydantic import BaseModel
from typing import Optional, List

class StoryState(BaseModel):
    """Maintains the current state of the bedtime story."""
    
    current_plot_summary: str
    hero_name: str
    last_sentence: str
    mood: str  # curious, excited, scared, happy, etc.
    scene_description: Optional[str] = None
    story_history: List[str] = []  # All generated text segments
    current_interaction: int = 0  # Current interaction number
    max_interactions: int = 10  # Maximum interactions for this story
    
    def update_plot(self, new_summary: str) -> None:
        """Update the plot summary with new developments."""
        self.current_plot_summary = new_summary
    
    def add_sentence(self, sentence: str) -> None:
        """Add a new sentence to the story."""
        self.last_sentence = sentence
        self.story_history.append(sentence)
        self.current_interaction += 1
    
    def change_mood(self, new_mood: str) -> None:
        """Change the emotional tone of the story."""
        self.mood = new_mood
    
    def get_context_for_generation(self) -> dict:
        """Get context dictionary for story generation."""
        return {
            "plot": self.current_plot_summary,
            "hero": self.hero_name,
            "last_sentence": self.last_sentence,
            "mood": self.mood,
            "scene": self.scene_description,
            "history": "\n".join(self.story_history[-3:]) if self.story_history else "",  # Last 3 segments
            "current_interaction": self.current_interaction,
            "max_interactions": self.max_interactions,
            "remaining_interactions": self.max_interactions - self.current_interaction
        }
    
    def is_story_complete(self) -> bool:
        """Check if story has reached max interactions."""
        return self.current_interaction >= self.max_interactions