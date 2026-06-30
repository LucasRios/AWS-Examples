"""
Interactive Storytelling Agent using Strands Agents SDK
Real-time bedtime story generation with interruption handling
"""

import asyncio
import os
from dotenv import load_dotenv
import json
from typing import Dict, Any, Optional
import boto3
from strands import Agent, tool
from story_state import StoryState

# Carrega variáveis de ambiente do arquivo .env (AWS credentials, model IDs, feature flags)
load_dotenv()

class StorytellingAgent:
    """
    Agente de narração interativa que usa AWS Bedrock para gerar texto, áudio e imagens.

    Fluxo principal:
    1. Gera um segmento de história via Amazon Nova (texto)
    2. Converte o texto em narração via Amazon Polly (áudio)
    3. Gera uma ilustração via Amazon Nova Canvas (imagem)
    4. Aguarda input do usuário para inserir plot twists em tempo real

    Cada funcionalidade pode ser habilitada/desabilitada via variáveis de ambiente
    (GENERATE_TEXT, GENERATE_VOICE, GENERATE_IMAGE, LOOPING).
    """

    def __init__(self, initial_state: StoryState):
        self.state = initial_state

        # Cria o cliente AWS Bedrock Runtime usando as credenciais do .env
        # As credenciais NUNCA devem ser hardcoded — são lidas das variáveis de ambiente
        try:
            self.bedrock_client = boto3.client(
                'bedrock-runtime',
                aws_access_key_id=os.getenv('AWS_ACCESS_KEY_ID'),
                aws_secret_access_key=os.getenv('AWS_SECRET_ACCESS_KEY'),
                region_name=os.getenv('AWS_REGION', 'us-east-1')
            )
        except Exception as e:
            print(f"❌ DETAILED ERROR: {type(e).__name__}: {e}")
            import traceback
            traceback.print_exc()

        self.running = True
        
        """
        try:
            # Lista os modelos foundation (base)
            response = self.bedrock_client.list_foundation_models()
    
            # Cabeçalho da tabela com as novas colunas
            print(f"{'ID do Modelo':<45} | {'Input':<20} | {'Output':<20}")
            print("-" * 90)
            
            for model in response['modelSummaries']:
                # FILTRO: Verifica se o provedor é a Amazon
                if model['providerName'] == 'Amazon':
                    model_id = model['modelId']
                    
                    # Formata as modalidades para exibição
                    input_m = ", ".join(model['inputModalities'])
                    output_m = ", ".join(model['outputModalities'])
                    
                    print(f"{model_id:<45} | {input_m:<20} | {output_m:<20}")

        except Exception as e:
            print(f"Erro ao listar modelos: {e}")
        """                
        # Create Strands agent with tools
        self.agent = Agent(tools=[
            self.generate_story_segment,
            self.generate_audio, 
            self.generate_scene
        ])
        
        # Mapa de palavras-chave que o usuário pode digitar para inserir plot twists imediatos.
        # Quando detectadas no input, a história muda de direção instantaneamente.
        self.interrupt_keywords = {
            'dragon!': "A magnificent dragon swoops down from the clouds!",
            'princess!': "A brave princess appears to join the adventure!",
            'monster!': "A friendly monster emerges from behind the trees!",
            'castle!': "A magical castle materializes in the distance!",
            'treasure!': "A glimmering treasure chest catches the light!",
            'stop!': "Everything becomes very quiet and still.",
            'wait!': "Time seems to pause as something important happens."
        }
    
    @tool
    def generate_story_segment(self, plot: str, hero: str, mood: str) -> str:
        """
        Gera o próximo segmento da história usando Amazon Nova (texto).

        O prompt é adaptado dinamicamente conforme o progresso da história:
        - Primeira interação → introdução do herói e cenário
        - Progresso 0-30% → desenvolvimento inicial
        - Progresso 30-70% → meio da história
        - Últimas 4 interações → clímax
        - Últimas 2 interações → conclusão

        O modelo é configurado via variável de ambiente TEXT_MODEL.
        """
        # Obtém o contexto completo do estado atual (histórico, interação atual, máximo)
        context = self.state.get_context_for_generation()

        # Define se esta é a primeira interação (sem histórico anterior)
        is_first = context['current_interaction'] == 0
        remaining = context['remaining_interactions']
        
        if is_first:
            # First interaction: Set the scene and introduce the hero
            prompt = f"""
You are a magical storyteller creating a bedtime story for a child. This is the BEGINNING of the story.

Hero: {hero}
Adventure theme: {plot}
Mood: {mood}
Total story length: {context['max_interactions']} segments

Create an engaging opening that:
1. Introduces {hero} and their world
2. Sets up the adventure theme: {plot}
3. Creates excitement and curiosity
4. Keeps it age-appropriate and {mood}

Write 2-3 short sentences. Make it magical and captivating!
"""
        else:
            # Continuation: Build on previous segments
            progress_percentage = (context['current_interaction'] / context['max_interactions']) * 100
            
            if remaining <= 2:
                story_phase = "CONCLUSION"
                instruction = f"This is the END of the story (segment {context['current_interaction'] + 1} of {context['max_interactions']}). Bring the adventure to a satisfying, happy conclusion. Wrap up the story beautifully."
            elif remaining <= 4:
                story_phase = "CLIMAX"
                instruction = f"This is the CLIMAX (segment {context['current_interaction'] + 1} of {context['max_interactions']}). Build tension and excitement. The big moment is happening!"
            elif progress_percentage < 30:
                story_phase = "DEVELOPMENT"
                instruction = f"This is the EARLY story (segment {context['current_interaction'] + 1} of {context['max_interactions']}). Develop the adventure, introduce challenges or discoveries."
            else:
                story_phase = "MIDDLE"
                instruction = f"This is the MIDDLE of the story (segment {context['current_interaction'] + 1} of {context['max_interactions']}). Continue the adventure with new developments."
            
            prompt = f"""
You are continuing a bedtime story for a child. {instruction}

Hero: {hero}
Current mood: {mood}
Story so far:
{context['history']}

Current situation: {plot}

Continue the story naturally from where it left off. Write 2-3 short sentences that flow smoothly from the previous segment. Keep the {mood} mood and make it age-appropriate.

DO NOT repeat what already happened. Move the story forward!
"""
        
        try:  
            native_request = {
                    "messages": [
                        {
                            "role": "user",
                            "content": [{"text": prompt}]
                        }
                    ],
                    "inferenceConfig": {
                        "maxTokens": 150,
                        "temperature": 0.8,
                        "topP": 0.9
                    }
                }

            credentials = self.bedrock_client._request_signer._credentials
            response = self.bedrock_client.invoke_model(
                modelId=os.getenv('TEXT_MODEL'),
                body=json.dumps(native_request)
            )
            
            response_body = json.loads(response['body'].read())
            
            try:
                story_text = response_body['output']['message']['content'][0]['text'].strip()
            except KeyError: 
                print(f"Estrutura recebida: {response_body}")
                raise
            
            # Update state
            self.state.add_sentence(story_text)
            self.state.scene_description = story_text
            
            print(f"📖 Story progress: {context['current_interaction'] + 1}/{context['max_interactions']}")
            
            return story_text
            
        except Exception as e:
            fallback = f"{hero} took a deep breath and looked around thoughtfully. The adventure was just beginning!"
            print(f"⚠️ Using fallback story: {e}")
            return fallback
    
    @tool
    def generate_audio(self, text: str, emotion: str) -> str:
        """
        Converte o texto da história em narração de áudio via Amazon Polly.

        Usa a engine neural para vozes mais naturais. A voz 'Ruth' foi escolhida
        por ter tom suave adequado para histórias infantis.
        O arquivo MP3 gerado é salvo em static/ com timestamp único para evitar colisões.
        """
        try:
            # Cria o cliente Polly usando as credenciais já carregadas no ambiente
            polly = boto3.client(
                'polly',
                region_name=os.getenv('AWS_REGION', 'us-east-1')
            )

            print(f"🔊 Generating audio with Polly (Voice: Neural)...")
            print(f"🔊 Text: {text[:50]}...")
            
            # Call Polly
            response = polly.synthesize_speech(
                Engine='neural', # Neural voices are more natural
                LanguageCode='en-US', # Change to 'pt-BR' if story is in Portuguese
                OutputFormat='mp3',
                Text=text,
                VoiceId='Ruth' # 'Ruth' or 'Danielle' are great for stories
            )

            # Use timestamp to create unique filename
            import time
            timestamp = int(time.time() * 1000)
            filename = os.path.join("static", f"story_audio_{timestamp}.mp3")
            
            if "AudioStream" in response:
                with open(filename, "wb") as f:
                    f.write(response["AudioStream"].read())
                print(f"✅ Audio generated: {filename}")
                return f"story_audio_{timestamp}.mp3"
                
        except Exception as e:
            print(f"⚠️ Polly error: {e}")
            return "fallback.mp3"

    @tool
    def generate_scene(self, description: str) -> str:
        """
        Gera uma ilustração da cena atual via Amazon Nova Canvas (text-to-image).

        O prompt é construído com diretrizes de estilo fixas (aquarela, cores quentes,
        adequado para crianças 3-10 anos) para manter consistência visual ao longo da história.
        A imagem PNG é salva em static/ com timestamp único.
        O modelo é configurado via variável de ambiente IMAGE_MODEL.
        """
        try:
            # Timestamp em milissegundos garante nome de arquivo único por geração
            import time
            timestamp = int(time.time() * 1000)
            filename = os.path.join("static", f"scene_illustration_{timestamp}.png")
            
            prompt = f"""
            Child-friendly storybook illustration: {description}
            Style: Soft watercolor, whimsical, magical
            Colors: Warm and inviting
            Safe for children ages 3-10
            """
            
            print(f"🎨 Generating scene: '{description[:40]}...'")
            
            native_request = {
                        "taskType": "TEXT_IMAGE",
                        "textToImageParams": {
                            "text": prompt
                        },
                        "imageGenerationConfig": {
                            "numberOfImages": 1,
                            "height": 512,
                            "width": 512,
                            "cfgScale": 8.0
                        }
                    }
            
            response = self.bedrock_client.invoke_model(
                        modelId=os.getenv('IMAGE_MODEL'), 
                        body=json.dumps(native_request)
                    )
                    
            response_body = json.loads(response['body'].read())
            base64_image = response_body['images'][0]
                    
            import base64
            with open(filename, "wb") as f: 
                f.write(base64.b64decode(base64_image))
                        
            print(f"✅ Scene image generated: {filename}")
            return f"scene_illustration_{timestamp}.png"
            
        except Exception as e:
            print(f"⚠️ Scene generation failed: {e}")
            return "scene_placeholder.jpg"
    
    def detect_interruption(self, user_input: str) -> Optional[str]:
        """Check if user input contains interruption keywords."""
        user_input_lower = user_input.lower().strip()
        
        for keyword in self.interrupt_keywords:
            if keyword in user_input_lower:
                return keyword
        return None
    
    def handle_interruption(self, keyword: str) -> None:
        """Process story interruption and update plot."""
        twist = self.interrupt_keywords[keyword]
        print(f"\n🎭 Plot twist: {keyword.upper()}")
        
        # Update story state
        new_plot = f"{self.state.current_plot_summary} {twist}"
        self.state.update_plot(new_plot)
        self.state.change_mood("surprised")
        
        print(f"📝 Story updated: {twist}")
    
    async def story_loop(self):
        """Main interactive story loop."""
        print(f"✨ Beginning {self.state.hero_name}'s adventure...\n")
        
        while self.running:
            try:
                # Generate story segment
                story_segment = "teste";

                if (os.getenv('GENERATE_TEXT').lower() == 'true'):                
                    story_segment = self.generate_story_segment(
                    self.state.current_plot_summary,
                    self.state.hero_name, 
                    self.state.mood
                    )
                
                print(f"📖 {story_segment}\n")
                
                # Generate multimedia content
                audio_file = "story_audio.mp3"; 
                if (os.getenv('GENERATE_VOICE').lower() == 'true'):
                    audio_file = self.generate_audio(story_segment, self.state.mood)
                
                scene_file = "scene_illustration.png"; 
                if (os.getenv('GENERATE_IMAGE').lower() == 'true'):
                    scene_file =self.generate_scene(story_segment)                

                
                # Wait for user input with timeout
                try:
                    user_input = await asyncio.wait_for(
                        asyncio.to_thread(
                            input, 
                            "💭 Say something to change the story (or Enter to continue): "
                        ),
                        timeout=8.0
                    )
                    
                    if user_input.lower() in ['quit', 'exit', 'end']:
                        print("\n🌙 Sweet dreams! The story continues in your imagination...")
                        self.running = False
                        break
                    
                    # Handle interruptions
                    if user_input.strip():
                        keyword = self.detect_interruption(user_input)
                        if keyword:
                            self.handle_interruption(keyword)
                        else:
                            # Incorporate user input into story
                            user_addition = f"Then {user_input.strip()}"
                            self.state.update_plot(f"{self.state.current_plot_summary} {user_addition}")
                            print(f"📝 Added to story: {user_addition}")
                
                except asyncio.TimeoutError:
                    # Continue automatically if no input
                    print("⏰ Continuing the story...\n")
                
                await asyncio.sleep(1)
                
                if (os.getenv('LOOPING').lower() == 'false'):
                     self.running = False;            

            except KeyboardInterrupt:
                print("\n🌟 Story paused. Sweet dreams!")
                self.running = False
            except Exception as e:
                print(f"❌ Story error: {e}")
                await asyncio.sleep(2)
    
    async def run(self):
        """Start the storytelling agent."""
        await self.story_loop()


# Optional: Bedrock AgentCore deployment wrapper
try:
    from bedrock_agentcore import BedrockAgentCoreApp
    
    app = BedrockAgentCoreApp()
    
    @app.entrypoint
    def production_handler(request):
        """Production API endpoint for Bedrock AgentCore."""
        hero_name = request.get("hero_name", "Alex")
        
        initial_state = StoryState(
            hero_name=hero_name,
            current_plot_summary=f"A brave child named {hero_name} begins a magical adventure",
            last_sentence="",
            mood="curious"
        )
        
        agent = StorytellingAgent(initial_state)
        
        # Generate single story response
        story_segment = agent.generate_story_segment(
            initial_state.current_plot_summary,
            initial_state.hero_name,
            initial_state.mood
        )
        
        return {
            "story_segment": story_segment,
            "hero_name": hero_name,
            "mood": initial_state.mood,
            "audio_url": agent.generate_audio(story_segment, initial_state.mood),
            "scene_url": agent.generate_scene(story_segment)
        }

except ImportError:
    print("💡 bedrock-agentcore not installed - local mode only")
    app = None