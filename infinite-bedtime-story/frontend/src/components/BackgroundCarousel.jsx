import React, { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'

// Placeholder fantasy images - replace with actual assets
const fantasyImages = [
  'https://images.unsplash.com/photo-1578662996442-48f60103fc96?w=1920&h=1080&fit=crop',
  'https://images.unsplash.com/photo-1518709268805-4e9042af2176?w=1920&h=1080&fit=crop',
  'https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=1920&h=1080&fit=crop',
  'https://images.unsplash.com/photo-1441974231531-c6227db76b6e?w=1920&h=1080&fit=crop',
  'https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=1920&h=1080&fit=crop'
]

const BackgroundCarousel = ({ 
  images = fantasyImages, 
  interval = 8000,
  kenBurnsEffect = true,
  className = "" 
}) => {
  const [currentIndex, setCurrentIndex] = useState(0)

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentIndex((prevIndex) => 
        prevIndex === images.length - 1 ? 0 : prevIndex + 1
      )
    }, interval)

    return () => clearInterval(timer)
  }, [images.length, interval])

  return (
    <div className={`fixed inset-0 -z-10 ${className}`}>
      <AnimatePresence mode="wait">
        <motion.div
          key={currentIndex}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 2 }}
          className="absolute inset-0"
        >
          <motion.div
            className="w-full h-full bg-cover bg-center bg-no-repeat"
            style={{
              backgroundImage: `url(${images[currentIndex]})`
            }}
            animate={kenBurnsEffect ? {
              scale: [1, 1.15]
            } : {}}
            transition={{
              duration: 20,
              ease: "easeOut"
            }}
          />
          
          {/* Magical overlay */}
          <div className="absolute inset-0 bg-gradient-to-br from-purple-900/40 via-blue-900/30 to-indigo-900/40" />
          
          {/* Sparkle effects */}
          <div className="absolute inset-0">
            {[...Array(20)].map((_, i) => (
              <motion.div
                key={i}
                className="absolute w-1 h-1 bg-white rounded-full"
                style={{
                  left: `${Math.random() * 100}%`,
                  top: `${Math.random() * 100}%`,
                }}
                animate={{
                  opacity: [0, 1, 0],
                  scale: [0, 1, 0]
                }}
                transition={{
                  duration: 3,
                  delay: Math.random() * 5,
                  repeat: Infinity,
                  repeatDelay: Math.random() * 3
                }}
              />
            ))}
          </div>
        </motion.div>
      </AnimatePresence>
      
      {/* Image indicators */}
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex space-x-2 z-10">
        {images.map((_, index) => (
          <motion.button
            key={index}
            onClick={() => setCurrentIndex(index)}
            className={`w-2 h-2 rounded-full transition-all duration-300 ${
              index === currentIndex 
                ? 'bg-white scale-125' 
                : 'bg-white/50 hover:bg-white/75'
            }`}
            whileHover={{ scale: 1.2 }}
            whileTap={{ scale: 0.9 }}
          />
        ))}
      </div>
    </div>
  )
}

export default BackgroundCarousel