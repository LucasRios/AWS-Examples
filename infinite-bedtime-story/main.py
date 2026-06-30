#!/usr/bin/env python3
"""
Infinite Bedtime Story - Real-time Interactive Storytelling Agent
Entry point for the AWS Strands-based storytelling application.
"""

import asyncio
import os
from dotenv import load_dotenv
from agent_logic import StorytellingAgent
from story_state import StoryState

# Load environment variables
load_dotenv()

async def main():
    """Main entry point for the storytelling agent."""
     
    # Initialize story state
    # hero_name = input("What's your hero's name? ").strip() or "Alex"
    hero_name = "Alex"
    initial_state = StoryState(
        hero_name=hero_name,
        current_plot_summary=f"A brave child named {hero_name} begins an adventure",
        last_sentence="",
        mood="curious"
    )
    
    # Create and start the agent
    agent = StorytellingAgent(initial_state)
    
    #print(f"\n✨ Starting {hero_name}'s adventure...")
    #print("💡 Say 'Stop!', 'Wait!', or add characters like 'Dragon!' to change the story!")
    #print("📖 Type 'quit' to end the story.\n")
    
    try:
        await agent.run()
    except KeyboardInterrupt:
        print("\n🌟 Sweet dreams! The story continues in your imagination...")
    except Exception as e:
        print(f"❌ Story error: {e}")

if __name__ == "__main__":
    asyncio.run(main())