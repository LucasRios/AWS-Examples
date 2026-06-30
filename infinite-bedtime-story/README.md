# 🌙 Infinite Bedtime Stories

An AI-powered interactive storytelling application that creates unique, continuous bedtime stories using AWS Nova models. Each story has a natural beginning, middle, and end, with optional voice narration and illustrations.

## ✨ Features

### Core Functionality
- **Intelligent Story Generation**: Uses Amazon Nova Micro/Pro for contextual, continuous storytelling
- **Story Continuity**: Each segment builds naturally on previous ones - no repetition
- **Structured Narrative**: Automatic story phases (opening, development, climax, conclusion)
- **Configurable Length**: Set story duration from 5 to 50 interactions
- **Progress Tracking**: Visual progress bar showing story completion
- **Auto-Conclusion**: Stories automatically wrap up at the configured length

### Multimedia Generation (Optional)
- **Voice Narration**: Amazon Polly neural voices for natural speech
- **Scene Illustrations**: Amazon Nova Canvas for storybook-style images
- **Unique Files**: Each segment generates new audio/image with timestamps

### Interactive Elements
- **Wizard Setup**: 3-question wizard to personalize the story
- **Real-time Generation**: Continuous story flow with configurable delays
- **Responsive UI**: Beautiful, animated interface with glass-morphism design

## 🎯 How It Works

### Story Generation Flow

1. **Initial Setup**: User provides hero name, age, and adventure theme
2. **Opening** (Interaction 1): AI introduces the hero and sets up the adventure
3. **Development** (Early interactions): Story develops with challenges and discoveries
4. **Middle** (30-60%): Adventure continues with new developments
5. **Climax** (Last 4 interactions): Tension builds to the decisive moment
6. **Conclusion** (Last 2 interactions): Story wraps up with a satisfying ending

### Context Management

The system maintains story continuity by:
- Storing all generated text segments
- Passing the last 3 segments as context to the AI
- Tracking current position in the story arc
- Adjusting prompts based on remaining interactions

## 🚀 Quick Start

### Prerequisites

- Python 3.8+
- Node.js 16+
- AWS Account with Bedrock access
- AWS credentials with permissions for:
  - Amazon Bedrock (Nova models)
  - Amazon Polly (optional, for voice)

### Installation

1. **Clone and setup backend**:
```bash
cd infinite-bedtime-story
pip install -r requirements_api.txt
```

2. **Configure AWS credentials**:
```bash
# Edit .env file
AWS_ACCESS_KEY_ID=your_access_key
AWS_SECRET_ACCESS_KEY=your_secret_key
AWS_REGION=us-east-1

# Model configuration
TEXT_MODEL=amazon.nova-micro-v1:0
IMAGE_MODEL=amazon.nova-canvas-v1:0

# Feature toggles
GENERATE_TEXT=true
GENERATE_VOICE=false
GENERATE_IMAGE=false
LOOPING=true
```

3. **Install frontend dependencies**:
```bash
cd frontend
npm install
```

### Running the Application

**Option 1: Manual start**
```bash
# Terminal 1 - Backend
python api_server.py

# Terminal 2 - Frontend
cd frontend
npm run dev
```

**Option 2: Using start script**
```bash
python start_app.py
```

Access the application:
- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:8000
- **API Docs**: http://localhost:8000/docs

## ⚙️ Configuration

### Settings Page

Configure the application through the web interface at `/settings`:

#### AWS Credentials
- Access Key ID
- Secret Access Key
- Region (us-east-1, us-west-2, etc.)

#### Model Selection
- **Text Model**: `amazon.nova-micro-v1:0` (fast) or `amazon.nova-pro-v1:0` (advanced)
- **Audio Model**: `amazon.nova-2-sonic-v1:0` (future use)
- **Image Model**: `amazon.nova-canvas-v1:0`

#### Features
- **Generate Text**: Enable AI text generation
- **Generate Voice**: Enable Polly voice narration
- **Generate Image**: Enable Canvas illustrations
- **Continuous Loop**: Auto-continue story segments

#### Story Configuration
- **Number of Interactions**: Set story length (5-50)
  - 5-8: Short story (5-10 minutes)
  - 10-15: Medium story (10-20 minutes) ⭐ Recommended
  - 20-30: Long story (30-45 minutes)
  - 30+: Epic story (1+ hour)

## 📖 Usage Guide

### Creating a Story

1. **Configure Settings**:
   - Set AWS credentials
   - Choose desired features
   - Set number of interactions (default: 10)
   - Save settings

2. **Start New Story**:
   - Click "Start New Story" on home page
   - Answer 3 wizard questions:
     - Hero/heroine name
     - Age
     - Adventure theme

3. **Enjoy the Story**:
   - Story begins automatically
   - Progress bar shows completion
   - If looping is enabled, story continues automatically
   - Story concludes naturally at the set length

### Controls

- **Play/Pause**: Control audio playback
- **Next**: Skip to next segment (if looping disabled)
- **Home**: Return to main menu
- **Microphone**: Voice interruption (placeholder)

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    React Frontend                        │
│  (Vite + React Router + Framer Motion + Tailwind)      │
└────────────────────┬────────────────────────────────────┘
                     │ HTTP/REST
┌────────────────────▼────────────────────────────────────┐
│                  FastAPI Backend                         │
│              (Python + Uvicorn)                         │
└────────────────────┬────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        │            │            │
┌───────▼──────┐ ┌──▼─────┐ ┌───▼──────┐
│ Strands      │ │ Story  │ │ AWS      │
│ Agents SDK   │ │ State  │ │ Clients  │
└──────┬───────┘ └────────┘ └────┬─────┘
       │                          │
       └──────────┬───────────────┘
                  │
    ┌─────────────┼─────────────┐
    │             │             │
┌───▼────────┐ ┌─▼──────┐ ┌───▼────────┐
│ Nova Micro │ │ Polly  │ │ Nova Canvas│
│ (Text)     │ │ (Voice)│ │ (Images)   │
└────────────┘ └────────┘ └────────────┘
```

## 📁 Project Structure

```
infinite-bedtime-story/
├── 🐍 Backend (Python)
│   ├── api_server.py           # FastAPI REST API
│   ├── agent_logic.py          # Strands agent with story generation
│   ├── story_state.py          # Story state management
│   ├── main.py                 # CLI interface (legacy)
│   ├── start_app.py           # Unified startup script
│   └── requirements_api.txt    # Python dependencies
│
├── 🎨 Frontend (React)
│   ├── src/
│   │   ├── components/         # Reusable UI components
│   │   │   ├── BackgroundCarousel.jsx
│   │   │   ├── MicrophoneIndicator.jsx
│   │   │   ├── TypewriterEffect.jsx
│   │   │   └── WizardOverlay.jsx
│   │   ├── contexts/           # React context providers
│   │   │   └── SettingsContext.jsx
│   │   ├── pages/              # Main application pages
│   │   │   ├── Home.jsx
│   │   │   ├── Settings.jsx
│   │   │   └── StoryView.jsx
│   │   ├── App.jsx
│   │   ├── main.jsx
│   │   └── index.css
│   ├── package.json
│   └── vite.config.js
│
├── 📁 Generated Content
│   └── static/                # Generated audio/images
│       ├── story_audio_*.mp3
│       └── scene_illustration_*.png
│
├── 📚 Documentation
│   ├── README.md              # This file
│   ├── ATUALIZACAO_FINAL.md   # Latest updates (PT)
│   ├── CORRECAO_AUDIO_IMAGEM.md # Audio/image fix details
│   ├── TESTE_RAPIDO.md        # Quick test guide
│   └── CHECKLIST.md           # Verification checklist
│
└── 🔧 Configuration
    ├── .env                   # Environment variables
    └── .env.template          # Template for .env
```

## 🔧 API Endpoints

### Story Management

#### `POST /api/story/start`
Initialize a new story session.

**Request Body**:
```json
{
  "heroName": "Luna",
  "heroAge": "7",
  "adventureTheme": "magical dragons",
  "maxInteractions": 10,
  "awsCredentials": {
    "awsAccessKey": "...",
    "awsSecretKey": "...",
    "awsRegion": "us-east-1"
  },
  "modelIds": {
    "text": "amazon.nova-micro-v1:0",
    "audio": "amazon.nova-2-sonic-v1:0",
    "image": "amazon.nova-canvas-v1:0"
  },
  "features": {
    "generateText": true,
    "generateVoice": false,
    "generateImage": false,
    "looping": true
  }
}
```

**Response**:
```json
{
  "text_chunk": "Luna, a brave 7-year-old...",
  "audio_url": "static/story_audio_1234567890.mp3",
  "image_url": "static/scene_illustration_1234567890.png",
  "story_state": {
    "current_interaction": 1,
    "max_interactions": 10,
    "is_complete": false,
    "hero_name": "Luna",
    "mood": "curious"
  },
  "success": true
}
```

#### `POST /api/story/continue`
Generate the next story segment.

**Request Body**:
```json
{
  "features": {
    "generateText": true,
    "generateVoice": false,
    "generateImage": false
  }
}
```

**Response**: Same structure as `/start`

#### `POST /api/story/reset`
Reset the current story session.

#### `GET /api/story/state`
Get current story state.

### System Endpoints

#### `GET /health`
Health check endpoint.

#### `GET /`
API information and version.

## 🎨 Key Features Explained

### Story Continuity System

The application ensures story continuity through:

1. **Context Accumulation**: Stores all generated segments
2. **Sliding Window**: Passes last 3 segments to AI for context
3. **Phase Detection**: Adjusts prompts based on story progress
4. **Explicit Instructions**: Tells AI "DO NOT repeat what already happened"

### Unique File Generation

To prevent caching issues:
- Each audio file: `story_audio_{timestamp}.mp3`
- Each image file: `scene_illustration_{timestamp}.png`
- Frontend adds cache busters: `?t={timestamp}`

### Automatic Cleanup

Background task removes files older than 1 hour every 30 minutes.

## 💰 AWS Costs

### Estimated Costs per Story (10 interactions)

**Text Generation** (Nova Micro):
- ~1,500 tokens total
- Cost: ~$0.0001

**Voice Generation** (Polly Neural):
- ~1,500 characters
- Cost: ~$0.024

**Image Generation** (Nova Canvas):
- 10 images
- Cost: ~$0.40

**Total per story**: ~$0.42 (with all features enabled)

### Cost Optimization

For testing, disable expensive features:
```env
GENERATE_TEXT=true
GENERATE_VOICE=false  # Save ~$0.024 per story
GENERATE_IMAGE=false  # Save ~$0.40 per story
```

## 🧪 Testing

### Quick Test (3 minutes)

```bash
# 1. Start servers
python api_server.py
cd frontend && npm run dev

# 2. Configure
Settings → Number of Interactions: 5
Settings → Generate Text: ✅
Settings → Continuous Loop: ✅

# 3. Test
Start New Story → Answer wizard → Observe!
```

**Expected Result**:
- 5 different text segments
- Story with natural continuity
- Progress bar: 1/5, 2/5, 3/5, 4/5, 5/5
- "✨ Story Complete!" message
- Loop stops automatically

### Detailed Testing

See `TESTE_RAPIDO.md` for comprehensive test guide.

## 🐛 Troubleshooting

### Story Repeats Itself
- Ensure `agent_logic.py` is updated with context system
- Check backend logs for "Story progress: X/Y"
- Restart backend and clear browser cache

### Audio/Image Not Changing
- Check backend logs for unique timestamps
- Verify files in `static/` folder have different timestamps
- Clear browser cache (Ctrl+Shift+Delete)
- Check console for new URLs being loaded

### Loop Doesn't Stop
- Verify `is_complete` is `true` in console
- Check `maxInteractions` setting
- Ensure `StoryView.jsx` checks `isComplete` before continuing

### API Errors
- Verify AWS credentials are correct
- Check AWS region supports Nova models
- Ensure Bedrock permissions are configured
- Check backend logs for detailed error messages

## 📚 Documentation

- **ATUALIZACAO_FINAL.md**: Complete technical details of latest updates
- **CORRECAO_AUDIO_IMAGEM.md**: Audio/image generation fix explanation
- **TESTE_RAPIDO.md**: Step-by-step testing guide
- **TESTE_AUDIO_IMAGEM.md**: Audio/image testing guide
- **CHECKLIST.md**: Verification checklist
- **RESUMO_EXECUTIVO.md**: Executive summary (Portuguese)

## 🔐 Security Notes

### AWS Credentials
- Never commit `.env` file with real credentials
- Use `.env.template` for examples
- Add `.env` to `.gitignore`
- Use IAM roles with minimal permissions
- Rotate credentials regularly

### Required AWS Permissions
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "bedrock:InvokeModel"
      ],
      "Resource": [
        "arn:aws:bedrock:*::foundation-model/amazon.nova-*"
      ]
    },
    {
      "Effect": "Allow",
      "Action": [
        "polly:SynthesizeSpeech"
      ],
      "Resource": "*"
    }
  ]
}
```

## 🚀 Deployment

### Backend Options
- AWS Lambda + API Gateway
- AWS ECS/Fargate
- Heroku
- DigitalOcean App Platform

### Frontend Options
- Vercel (Recommended)
- Netlify
- AWS S3 + CloudFront
- GitHub Pages

### Build Frontend
```bash
cd frontend
npm run build
# Deploy dist/ folder
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

This project is licensed under the Apache 2.0 License.

## 🌟 Acknowledgments

- **AWS Nova Models** for multimodal AI capabilities
- **Strands Agents SDK** for agent framework
- **React & FastAPI** for modern web architecture
- **Framer Motion** for beautiful animations
- **Tailwind CSS** for styling

## 📞 Support

For issues or questions:
1. Check the documentation files
2. Review backend and frontend logs
3. Verify AWS credentials and permissions
4. Check console for errors (F12)

---

**Create infinite magical stories with natural continuity! 🌙✨**

Version: 2.0.0 (Latest Update: Story Continuity & Progress Tracking)