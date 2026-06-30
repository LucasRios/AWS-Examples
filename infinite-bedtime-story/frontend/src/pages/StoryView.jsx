import React, { useState, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useNavigate, useLocation } from 'react-router-dom'
import { ArrowLeft, Pause, Play, SkipForward, Home } from 'lucide-react'
import TypewriterEffect from '../components/TypewriterEffect'
import MicrophoneIndicator from '../components/MicrophoneIndicator'
import { useSettings } from '../contexts/SettingsContext'

const StoryView = () => {
  const navigate = useNavigate()
  const location = useLocation()
  const { settings } = useSettings()
  const audioRef = useRef(null)
  
  const [storyState, setStoryState] = useState({
    currentText: '',
    currentImage: '',
    currentAudio: '',
    isPlaying: false,
    isLoading: false,
    currentInteraction: 0,
    maxInteractions: 10,
    isComplete: false
  })
  
  const [isListening, setIsListening] = useState(false)
  const [isPaused, setIsPaused] = useState(false)
  
  // Get story config from navigation state
  const storyConfig = location.state?.storyConfig || settings.storyConfig

  const [isFirstLoad, setIsFirstLoad] = useState(true)

  useEffect(() => {
    // Start the first story segment
    if (isFirstLoad) {
      startNewStory()
      setIsFirstLoad(false)
    }
  }, [])

  const startNewStory = async () => {
    setStoryState(prev => ({ ...prev, isLoading: true }))
    
    try {
      const response = await callStoryAPI('start', {
        heroName: storyConfig.heroName || 'Alex',
        heroAge: storyConfig.heroAge || '7',
        adventureTheme: storyConfig.adventureTheme || 'magical adventure',
        maxInteractions: settings.storyConfig?.maxInteractions || 10,
        awsCredentials: {
          awsAccessKey: settings.awsAccessKey,
          awsSecretKey: settings.awsSecretKey,
          awsRegion: settings.awsRegion
        },
        modelIds: settings.modelIds,
        features: settings.features
      })
      
      setStoryState(prev => ({
        ...prev,
        currentText: response.text_chunk,
        currentImage: response.image_url ? `http://localhost:8000/${response.image_url}` : getRandomPlaceholderImage(),
        currentAudio: `http://localhost:8000/${response.audio_url}`,
        isLoading: false,
        isPlaying: true,
        currentInteraction: response.story_state?.current_interaction || 0,
        maxInteractions: response.story_state?.max_interactions || 10,
        isComplete: response.story_state?.is_complete || false
      }))
      
      // Auto-play audio if available
      if (response.audio_url) {
        playAudio(`http://localhost:8000/${response.audio_url}`)
      }
      
    } catch (error) {
      console.error('Failed to start story:', error)
      // Fallback to placeholder content
      setStoryState(prev => ({
        ...prev,
        currentText: `${storyConfig.heroName || 'Alex'} began a magical adventure full of infinite possibilities. The world awaited to be explored!`,
        currentImage: getRandomPlaceholderImage(),
        isLoading: false,
        isPlaying: true
      }))
    }
  }

  const generateStorySegment = async () => {
    setStoryState(prev => ({ ...prev, isLoading: true }))
    
    try {
      const response = await callStoryAPI('continue', {
        features: settings.features
      })
      
      console.log('📦 Continue response:', {
        text: response.text_chunk?.substring(0, 50),
        audio: response.audio_url,
        image: response.image_url
      })
      
      setStoryState(prev => ({
        ...prev,
        currentText: response.text_chunk,
        currentImage: response.image_url ? `http://localhost:8000/${response.image_url}` : getRandomPlaceholderImage(),
        currentAudio: `http://localhost:8000/${response.audio_url}`,
        isLoading: false,
        isPlaying: true,
        currentInteraction: response.story_state?.current_interaction || prev.currentInteraction,
        maxInteractions: response.story_state?.max_interactions || prev.maxInteractions,
        isComplete: response.story_state?.is_complete || false
      }))
      
      // Auto-play audio if available
      if (response.audio_url) {
        console.log('🔊 Playing audio:', response.audio_url)
        playAudio(`http://localhost:8000/${response.audio_url}`)
      }
      
    } catch (error) {
      console.error('Failed to generate story:', error)
      // Fallback content
      const fallbackTexts = [
        `${storyConfig.heroName} paused to think about the next step of the adventure.`,
        `A gentle breeze brought new possibilities to ${storyConfig.heroName}.`,
        `${storyConfig.heroName}'s heart beat fast with the excitement of discovery.`
      ]
      
      setStoryState(prev => ({
        ...prev,
        currentText: fallbackTexts[Math.floor(Math.random() * fallbackTexts.length)],
        currentImage: getRandomPlaceholderImage(),
        isLoading: false,
        isPlaying: true
      }))
    }
  }

  const getRandomPlaceholderImage = () => {
    const placeholderImages = [
      'http://localhost:8000/static/default1.png?w=1920&h=1080&fit=crop',
      'http://localhost:8000/static/default2.png?w=1920&h=1080&fit=crop',
      'http://localhost:8000/static/default3.png?w=1920&h=1080&fit=crop',
      'http://localhost:8000/static/default4.png?w=1920&h=1080&fit=crop',
      'http://localhost:8000/static/default5.png?w=1920&h=1080&fit=crop'
    ]
    return placeholderImages[Math.floor(Math.random() * placeholderImages.length)]
  }

  const callStoryAPI = async (endpoint, data = null) => {
    try {
      const response = await fetch(`http://localhost:8000/api/story/${endpoint}`, {
        method: data ? 'POST' : 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
        body: data ? JSON.stringify(data) : null
      })
      
      if (!response.ok) {
        throw new Error(`API Error: ${response.status} ${response.statusText}`)
      }
      
      return await response.json()
    } catch (error) {
      console.error(`Failed to call ${endpoint}:`, error)
      throw error
    }
  }

  const playAudio = (audioUrl) => {
    if (audioRef.current) {
        // Add cache buster to force reload
        const urlWithCacheBuster = `${audioUrl}?t=${Date.now()}`
        audioRef.current.src = urlWithCacheBuster
        audioRef.current.load() // Force reload
            
        // Browser requires user interaction before playing audio
        audioRef.current.play().catch(error => {
          console.error("Error playing audio:", error)
        })
    }
  }

  const handleMicrophoneToggle = () => {
    setIsListening(!isListening)
    
    if (!isListening) {
      // Start listening for interruptions
      setTimeout(() => {
        // Mock interruption detection
        const interruptions = ['Dragon!', 'Princess!', 'Castle!', 'Treasure!']
        const randomInterruption = interruptions[Math.floor(Math.random() * interruptions.length)]
        
        console.log(`Detected interruption: ${randomInterruption}`)
        handleInterruption(randomInterruption)
        setIsListening(false)
      }, 3000)
    }
  }

  const handleInterruption = async (keyword) => {
    console.log(`Story interrupted with: ${keyword}`)
    
    setStoryState(prev => ({ ...prev, isLoading: true }))
    
    try {
      const response = await callStoryAPI('interrupt', {
        keyword: keyword.toLowerCase(),
        userInput: `User said: ${keyword}`
      })
      
      setStoryState(prev => ({
        ...prev,
        currentText: response.text_chunk,
        currentImage: response.image_url ? `http://localhost:8000/${response.image_url}` : getRandomPlaceholderImage(),
        currentAudio: `http://localhost:8000/${response.audio_url}`,
        isLoading: false,
        isPlaying: true,
        currentInteraction: response.story_state?.current_interaction || prev.currentInteraction,
        maxInteractions: response.story_state?.max_interactions || prev.maxInteractions,
        isComplete: response.story_state?.is_complete || false
      }))
      
      // Auto-play audio if available
      if (response.audio_url) {
        playAudio(`http://localhost:8000/${response.audio_url}`)
      }
      
    } catch (error) {
      console.error('Failed to handle interruption:', error)
      // Fallback to regular story generation
      generateStorySegment()
    }
  }

  const handlePlayPause = () => {
    if (audioRef.current) {
      if (isPaused) {
        audioRef.current.play()
      } else {
        audioRef.current.pause()
      }
      setIsPaused(!isPaused)
    }
  }

  const handleNext = () => {
    generateStorySegment()
  }

  return (
    <div className="relative min-h-screen overflow-hidden">
      {/* Background image with Ken Burns effect */}
 <AnimatePresence mode="wait">
  {storyState.currentImage && (
    <motion.div
      key={storyState.currentImage}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 1.5 }}
      className="fixed inset-0 z-0" 
    >
      <motion.div
        className="absolute inset-0 bg-cover bg-center bg-no-repeat"
        style={{
          backgroundImage: `url("${storyState.currentImage}")`, 
        }}
        animate={{ scale: [1, 1.1] }}
        transition={{ duration: 20, ease: "linear", repeat: Infinity, repeatType: "reverse" }}
      />
      {/* Overlay para leitura */}
      <div className="absolute inset-0 bg-black/40" /> 
      <div className="absolute inset-0 bg-gradient-to-t from-purple-900 via-transparent to-transparent" />
    </motion.div>
  )}
</AnimatePresence>

      {/* Navigation header */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="absolute top-4 left-4 right-4 z-20 flex justify-between items-center"
      >
        <motion.button
          onClick={() => navigate('/')}
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="glass-panel p-3 text-white hover:bg-white/20 transition-all"
        >
          <ArrowLeft className="w-5 h-5" />
        </motion.button>

        {/* Story controls */}
        <div className="flex items-center gap-2">
          <motion.button
            onClick={handlePlayPause}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            className="glass-panel p-3 text-white hover:bg-white/20 transition-all"
          >
            {isPaused ? <Play className="w-5 h-5" /> : <Pause className="w-5 h-5" />}
          </motion.button>
          
          <motion.button
            onClick={handleNext}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            className="glass-panel p-3 text-white hover:bg-white/20 transition-all"
          >
            <SkipForward className="w-5 h-5" />
          </motion.button>
        </div>
      </motion.div>

      {/* Story text area */}
      <div className="absolute bottom-0 left-0 right-0 z-10 p-8">
        <motion.div
          initial={{ opacity: 0, y: 50 }}
          animate={{ opacity: 1, y: 0 }}
          className="glass-panel p-6 max-w-4xl mx-auto"
        >
          {storyState.isLoading ? (
            <motion.div
              animate={{ opacity: [0.5, 1, 0.5] }}
              transition={{ duration: 1.5, repeat: Infinity }}
              className="text-center text-white/70"
            >
              ✨ Creating magic...
            </motion.div>
          ) : (
            <TypewriterEffect
              text={storyState.currentText}
              speed={50}
              className="text-center text-lg md:text-xl leading-relaxed"
              onComplete={() => {
                // Auto-continue if looping is enabled and story is not complete
                if (settings.features.looping && !storyState.isComplete) {
                  setTimeout(generateStorySegment, 5000)
                }
              }}
            />
          )}
        </motion.div>
      </div>

      {/* Character info overlay */}
      <motion.div
        initial={{ opacity: 0, x: -50 }}
        animate={{ opacity: 1, x: 0 }}
        className="absolute top-20 left-4 z-20"
      >
        <div className="glass-panel p-4">
          <h3 className="text-white font-semibold mb-2">Story Hero</h3>
          <p className="text-white/80">{storyConfig.heroName}</p>
          <p className="text-white/60 text-sm">{storyConfig.heroAge} years old</p>
          <p className="text-white/60 text-sm mt-1">{storyConfig.adventureTheme}</p>
          
          {/* Progress indicator */}
          <div className="mt-4 pt-4 border-t border-white/20">
            <div className="flex justify-between text-xs text-white/60 mb-1">
              <span>Progress</span>
              <span>{storyState.currentInteraction}/{storyState.maxInteractions}</span>
            </div>
            <div className="w-full bg-white/20 rounded-full h-2">
              <motion.div
                className="bg-magic-400 h-2 rounded-full"
                initial={{ width: 0 }}
                animate={{ 
                  width: `${(storyState.currentInteraction / storyState.maxInteractions) * 100}%` 
                }}
                transition={{ duration: 0.5 }}
              />
            </div>
            {storyState.isComplete && (
              <motion.p
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className="text-magic-300 text-xs mt-2 text-center"
              >
                ✨ Story Complete!
              </motion.p>
            )}
          </div>
        </div>
      </motion.div>

      {/* Always listening microphone */}
      <MicrophoneIndicator
        isListening={isListening}
        isActive={settings.features.generateVoice}
        onToggle={handleMicrophoneToggle}
      />

      {/* Hidden audio player */}
      <audio
        ref={audioRef}
        onEnded={() => setIsPaused(false)}
        onPlay={() => setIsPaused(false)}
        onPause={() => setIsPaused(true)}
        style={{ display: 'none' }}
      />

      {/* Loading overlay */}
      <AnimatePresence>
        {storyState.isLoading && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center"
          >
            <motion.div
              animate={{ rotate: 360 }}
              transition={{ duration: 2, repeat: Infinity, ease: "linear" }}
              className="w-16 h-16 border-4 border-magic-400 border-t-transparent rounded-full"
            />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

export default StoryView