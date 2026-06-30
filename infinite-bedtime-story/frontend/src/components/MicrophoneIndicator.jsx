import React, { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import { Mic, MicOff } from 'lucide-react'

const MicrophoneIndicator = ({ 
  isListening = false, 
  isActive = true, 
  onToggle,
  className = "" 
}) => {
  const [isGlowing, setIsGlowing] = useState(false)

  useEffect(() => {
    if (isListening) {
      const interval = setInterval(() => {
        setIsGlowing(prev => !prev)
      }, 800)
      return () => clearInterval(interval)
    } else {
      setIsGlowing(false)
    }
  }, [isListening])

  return (
    <motion.div
      initial={{ scale: 0, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      className={`fixed bottom-6 right-6 z-50 ${className}`}
    >
      <motion.button
        onClick={onToggle}
        disabled={!isActive}
        whileHover={{ scale: 1.1 }}
        whileTap={{ scale: 0.95 }}
        className={`
          relative p-4 rounded-full backdrop-blur-md border
          transition-all duration-300 focus:outline-none
          ${isListening 
            ? 'bg-red-500/20 border-red-400/50 text-red-300' 
            : 'bg-white/10 border-white/30 text-white hover:bg-white/20'
          }
          ${!isActive && 'opacity-50 cursor-not-allowed'}
        `}
      >
        {/* Pulsing glow effect when listening */}
        {isListening && (
          <motion.div
            animate={{
              scale: [1, 1.5, 1],
              opacity: [0.5, 0, 0.5]
            }}
            transition={{
              duration: 1.5,
              repeat: Infinity,
              ease: "easeInOut"
            }}
            className="absolute inset-0 rounded-full bg-red-400/30"
          />
        )}
        
        {/* Sparkle effects */}
        {isGlowing && (
          <>
            {[...Array(6)].map((_, i) => (
              <motion.div
                key={i}
                initial={{ scale: 0, opacity: 0 }}
                animate={{ 
                  scale: [0, 1, 0],
                  opacity: [0, 1, 0],
                  x: [0, (Math.random() - 0.5) * 60],
                  y: [0, (Math.random() - 0.5) * 60]
                }}
                transition={{
                  duration: 1.5,
                  delay: i * 0.1,
                  ease: "easeOut"
                }}
                className="absolute top-1/2 left-1/2 w-1 h-1 bg-yellow-300 rounded-full"
              />
            ))}
          </>
        )}
        
        {/* Microphone icon */}
        <motion.div
          animate={isListening ? { scale: [1, 1.1, 1] } : {}}
          transition={{ duration: 0.8, repeat: Infinity }}
        >
          {isActive ? (
            <Mic className="w-6 h-6" />
          ) : (
            <MicOff className="w-6 h-6" />
          )}
        </motion.div>
      </motion.button>
      
      {/* Tooltip */}
      <motion.div
        initial={{ opacity: 0, x: 10 }}
        animate={{ opacity: isListening ? 1 : 0, x: isListening ? 0 : 10 }}
        className="absolute right-full mr-3 top-1/2 -translate-y-1/2 whitespace-nowrap"
      >
        <div className="bg-black/80 text-white text-sm px-3 py-1 rounded-lg">
          {isListening ? 'Listening...' : 'Click to speak'}
        </div>
      </motion.div>
    </motion.div>
  )
}

export default MicrophoneIndicator