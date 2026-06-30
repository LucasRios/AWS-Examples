import React, { useState } from 'react'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import { Settings, Play, Moon, Stars } from 'lucide-react'
import BackgroundCarousel from '../components/BackgroundCarousel'
import WizardOverlay from '../components/WizardOverlay'
import { useSettings } from '../contexts/SettingsContext'

const Home = () => {
  const navigate = useNavigate()
  const { isConfigured } = useSettings()
  const [showWizard, setShowWizard] = useState(false)

  const handleStartStory = () => {
    if (!isConfigured()) {
      // Redirect to settings if not configured
      navigate('/settings')
      return
    }
    setShowWizard(true)
  }

  const handleWizardComplete = (storyConfig) => {
    setShowWizard(false)
    navigate('/story', { state: { storyConfig } })
  }

  return (
    <div className="relative min-h-screen overflow-hidden">
      {/* Background carousel */}
      <BackgroundCarousel />
      
      {/* Main content */}
      <div className="relative z-10 flex flex-col items-center justify-center min-h-screen p-8">
        {/* Title */}
        <motion.div
          initial={{ opacity: 0, y: -50 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 1, delay: 0.5 }}
          className="text-center mb-12"
        >
          <motion.div
            animate={{ rotate: [0, 5, -5, 0] }}
            transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
            className="inline-block mb-4"
          >
            <Moon className="w-16 h-16 text-yellow-300 mx-auto" />
          </motion.div>
          
          <h1 className="text-6xl font-bold bg-gradient-to-r from-magic-300 to-dream-300 bg-clip-text text-transparent mb-4">
            Infinite Bedtime Stories
          </h1>
          
          <motion.p
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 1 }}
            className="text-xl text-white/80 max-w-2xl mx-auto"
          >
            Magical stories that adapt to your imagination. 
            Each adventure is unique and interactive!
          </motion.p>
        </motion.div>

        {/* Action buttons */}
        <motion.div
          initial={{ opacity: 0, y: 50 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 1.2 }}
          className="flex flex-col sm:flex-row gap-6 items-center"
        >
          {/* Start Story Button */}
          <motion.button
            onClick={handleStartStory}
            whileHover={{ scale: 1.05, y: -2 }}
            whileTap={{ scale: 0.95 }}
            className="magic-button flex items-center gap-3 text-lg px-8 py-4"
          >
            <Play className="w-6 h-6" />
            Start New Story
          </motion.button>

          {/* Settings Button */}
          <motion.button
            onClick={() => navigate('/settings')}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            className="glass-panel px-6 py-3 text-white hover:bg-white/20 transition-all duration-300 flex items-center gap-2"
          >
            <Settings className="w-5 h-5" />
            Settings
          </motion.button>
        </motion.div>

        {/* Configuration status */}
        {!isConfigured() && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 1.5 }}
            className="mt-8 glass-panel p-4 text-center"
          >
            <p className="text-yellow-300 mb-2">
              ⚠️ Configure your AWS credentials first
            </p>
            <button
              onClick={() => navigate('/settings')}
              className="text-magic-300 hover:text-magic-200 underline"
            >
              Go to Settings
            </button>
          </motion.div>
        )}

        {/* Floating elements */}
        <div className="absolute inset-0 pointer-events-none">
          {[...Array(15)].map((_, i) => (
            <motion.div
              key={i}
              className="absolute"
              style={{
                left: `${Math.random() * 100}%`,
                top: `${Math.random() * 100}%`,
              }}
              animate={{
                y: [0, -20, 0],
                opacity: [0.3, 0.8, 0.3],
                rotate: [0, 180, 360]
              }}
              transition={{
                duration: 4 + Math.random() * 4,
                repeat: Infinity,
                delay: Math.random() * 2,
                ease: "easeInOut"
              }}
            >
              <Stars className="w-4 h-4 text-yellow-300/60" />
            </motion.div>
          ))}
        </div>
      </div>

      {/* Wizard overlay */}
      <WizardOverlay
        isVisible={showWizard}
        onComplete={handleWizardComplete}
      />
    </div>
  )
}

export default Home