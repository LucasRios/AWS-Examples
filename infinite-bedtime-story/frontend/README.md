# 🌙 Infinite Bedtime Stories - Frontend

A magical, immersive React frontend for the children's storytelling agent.

## ✨ Features

- **Wizard Onboarding**: 3-step interactive setup with voice input support
- **Immersive Story Mode**: Full-screen experience with Ken Burns effect
- **Real-time Interactions**: Always-listening microphone for story interruptions
- **Typewriter Effect**: Smooth text reveal animation
- **Settings Management**: AWS credentials and model configuration
- **Responsive Design**: Works on desktop and mobile devices

## 🚀 Quick Start

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Start development server
npm run dev
```

The app will be available at `http://localhost:3000`

## 🛠️ Tech Stack

- **React 18** with Vite
- **Tailwind CSS** for styling
- **Framer Motion** for animations
- **Lucide React** for icons
- **React Router DOM** for navigation
- **React Speech Recognition** for voice input

## 📁 Project Structure

```
frontend/
├── src/
│   ├── components/
│   │   ├── BackgroundCarousel.jsx    # Fantasy image carousel
│   │   ├── MicrophoneIndicator.jsx   # Always-listening UI
│   │   ├── TypewriterEffect.jsx      # Text animation
│   │   └── WizardOverlay.jsx         # Onboarding wizard
│   ├── contexts/
│   │   └── SettingsContext.jsx       # Global state management
│   ├── pages/
│   │   ├── Home.jsx                  # Landing page with wizard
│   │   ├── Settings.jsx              # Configuration page
│   │   └── StoryView.jsx             # Immersive story experience
│   ├── App.jsx                       # Main app component
│   ├── main.jsx                      # Entry point
│   └── index.css                     # Global styles
├── package.json
├── vite.config.js
└── tailwind.config.js
```

## 🎨 Key Components

### WizardOverlay
Interactive 3-step onboarding:
1. Hero name input
2. Hero age input  
3. Adventure theme selection

Features voice input with mock speech recognition.

### StoryView
Immersive storytelling experience:
- Full-screen background images with Ken Burns effect
- Typewriter text animation at bottom
- Always-listening microphone indicator
- Story controls (play/pause/next)

### MicrophoneIndicator
Floating microphone with:
- Pulsing animation when listening
- Sparkle effects for magic feel
- Visual feedback for user interactions

### SettingsContext
Global state management for:
- AWS credentials (Access Key, Secret, Region)
- Model IDs (Nova Micro, Nova Sonic, Nova Canvas)
- Feature toggles (Text, Voice, Image, Looping)
- Story configuration (Hero name, age, theme)

## 🎯 API Integration

The frontend is prepared for backend integration with mock API calls in `StoryView.jsx`:

```javascript
const response = await fetch('/api/generate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    heroName: storyConfig.heroName,
    heroAge: storyConfig.heroAge,
    adventureTheme: storyConfig.adventureTheme,
    settings: settings
  })
})

const data = await response.json()
// Expected: { text_chunk, audio_url, image_url }
```

## 🎨 Styling

Custom Tailwind configuration with:
- **Magic colors**: Purple/pink gradients
- **Dream colors**: Blue gradients  
- **Custom fonts**: Comfortaa (UI), Merriweather (story text)
- **Animations**: Float, pulse-soft, ken-burns, sparkle

## 🔧 Configuration

### Environment Setup
The app expects a backend API at `http://localhost:8000` (configurable in `vite.config.js`).

### Settings Storage
All settings are persisted in `localStorage` with the key `bedtime-story-settings`.

## 🎮 User Flow

1. **Home Page**: Welcome screen with background carousel
2. **Configuration Check**: Redirects to settings if AWS not configured
3. **Wizard**: 3-step story setup with voice input option
4. **Story Experience**: Immersive full-screen storytelling
5. **Interactions**: Real-time story modifications via microphone

## 🚀 Production Build

```bash
npm run build
npm run preview
```

## 🎯 Next Steps

1. **Backend Integration**: Connect to Python storytelling agent
2. **Real Speech Recognition**: Implement actual voice input
3. **Audio Streaming**: Add Nova Sonic audio playback
4. **Offline Mode**: Cache stories for offline use
5. **User Accounts**: Save story history and preferences

## 🎨 Customization

### Adding New Themes
Update `BackgroundCarousel.jsx` with new fantasy images:

```javascript
const fantasyImages = [
  'your-new-image-url.jpg',
  // ... existing images
]
```

### Custom Animations
Extend `tailwind.config.js` with new animations:

```javascript
animation: {
  'your-animation': 'your-keyframes 2s ease-in-out infinite'
}
```

## 📱 Mobile Support

The app is fully responsive with:
- Touch-friendly controls
- Mobile-optimized layouts
- Gesture support for story navigation

Perfect for bedtime stories on tablets and phones! 🌙✨