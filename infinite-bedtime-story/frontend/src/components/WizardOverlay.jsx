import React, { useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Mic, ArrowRight, Sparkles } from 'lucide-react'
import { useSettings } from '../contexts/SettingsContext'

const questions = [
  {
    id: 'heroName',
    question: 'What is the hero or heroine\'s name?',
    placeholder: 'Enter the adventurer\'s name...',
    type: 'text'
  },
  {
    id: 'heroAge',
    question: 'How old are they?',
    placeholder: 'Enter the age...',
    type: 'number'
  },
  {
    id: 'adventureTheme',
    question: 'What is today\'s adventure about?',
    placeholder: 'Dragons, castles, magical forests...',
    type: 'text'
  }
]

const WizardOverlay = ({ onComplete, isVisible = true }) => {
  const { updateStoryConfig } = useSettings()
  const [currentStep, setCurrentStep] = useState(0)
  const [answers, setAnswers] = useState({})
  const [isListening, setIsListening] = useState(false)

  const currentQuestion = questions[currentStep]

  const handleAnswer = (value) => {
    const newAnswers = {
      ...answers,
      [currentQuestion.id]: value
    }
    setAnswers(newAnswers)

    if (currentStep < questions.length - 1) {
      setCurrentStep(currentStep + 1)
    } else {
      // Complete wizard
      updateStoryConfig(newAnswers)
      onComplete?.(newAnswers)
    }
  }

  const handleVoiceInput = () => {
    setIsListening(true)
    
    // Mock speech recognition - replace with actual implementation
    setTimeout(() => {
      const mockResponses = {
        heroName: ['Luna', 'Pedro', 'Sofia', 'Miguel', 'Ana'],
        heroAge: ['5', '7', '8', '6', '9'],
        adventureTheme: ['magical dragons', 'enchanted castle', 'mysterious forest', 'fairy kingdom']
      }
      
      const responses = mockResponses[currentQuestion.id]
      const randomResponse = responses[Math.floor(Math.random() * responses.length)]
      
      handleAnswer(randomResponse)
      setIsListening(false)
    }, 2000)
  }

  const handleKeyPress = (e) => {
    if (e.key === 'Enter' && e.target.value.trim()) {
      handleAnswer(e.target.value.trim())
    }
  }

  if (!isVisible) return null

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
    >
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" />
      
      {/* Wizard Panel */}
      <motion.div
        initial={{ scale: 0.8, opacity: 0, y: 50 }}
        animate={{ scale: 1, opacity: 1, y: 0 }}
        exit={{ scale: 0.8, opacity: 0, y: 50 }}
        transition={{ type: "spring", damping: 20, stiffness: 300 }}
        className="relative glass-panel p-8 max-w-md w-full"
      >
        {/* Magic sparkles */}
        <div className="absolute -top-4 -right-4">
          <motion.div
            animate={{ rotate: 360 }}
            transition={{ duration: 4, repeat: Infinity, ease: "linear" }}
          >
            <Sparkles className="w-8 h-8 text-yellow-300" />
          </motion.div>
        </div>

        {/* Progress indicator */}
        <div className="flex justify-center mb-6">
          {questions.map((_, index) => (
            <motion.div
              key={index}
              className={`w-3 h-3 rounded-full mx-1 ${
                index <= currentStep ? 'bg-magic-400' : 'bg-white/30'
              }`}
              animate={index === currentStep ? { scale: [1, 1.2, 1] } : {}}
              transition={{ duration: 0.5 }}
            />
          ))}
        </div>

        {/* Question */}
        <AnimatePresence mode="wait">
          <motion.div
            key={currentStep}
            initial={{ opacity: 0, x: 50 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -50 }}
            transition={{ duration: 0.3 }}
            className="text-center"
          >
            <h2 className="text-2xl font-bold mb-6 text-white">
              {currentQuestion.question}
            </h2>

            {/* Input field */}
            <div className="relative mb-6">
              <input
                type={currentQuestion.type}
                placeholder={currentQuestion.placeholder}
                className="wizard-input text-center"
                onKeyPress={handleKeyPress}
                autoFocus
                disabled={isListening}
              />
              
              {/* Voice input button */}
              <motion.button
                onClick={handleVoiceInput}
                disabled={isListening}
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className={`absolute right-3 top-1/2 -translate-y-1/2 p-2 rounded-full transition-all ${
                  isListening 
                    ? 'bg-red-500/20 text-red-300' 
                    : 'bg-white/20 text-white hover:bg-white/30'
                }`}
              >
                <motion.div
                  animate={isListening ? { scale: [1, 1.2, 1] } : {}}
                  transition={{ duration: 0.8, repeat: Infinity }}
                >
                  <Mic className="w-4 h-4" />
                </motion.div>
              </motion.button>
            </div>

            {/* Voice feedback */}
            {isListening && (
              <motion.div
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className="text-sm text-magic-300 mb-4"
              >
                🎤 Listening... Speak now!
              </motion.div>
            )}

            {/* Skip button */}
            <motion.button
              onClick={() => handleAnswer('')}
              whileHover={{ scale: 1.05 }}
              className="text-white/70 hover:text-white text-sm underline"
            >
              Skip question
            </motion.button>
          </motion.div>
        </AnimatePresence>

        {/* Next indicator */}
        {currentStep < questions.length - 1 && (
          <motion.div
            animate={{ x: [0, 5, 0] }}
            transition={{ duration: 1.5, repeat: Infinity }}
            className="absolute right-4 top-1/2 -translate-y-1/2"
          >
            <ArrowRight className="w-6 h-6 text-white/50" />
          </motion.div>
        )}
      </motion.div>
    </motion.div>
  )
}

export default WizardOverlay