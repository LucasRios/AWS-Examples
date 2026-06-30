import React, { useState, useEffect } from 'react'
import { motion } from 'framer-motion'

const TypewriterEffect = ({ 
  text, 
  speed = 50, 
  onComplete, 
  className = "",
  startDelay = 0 
}) => {
  const [displayedText, setDisplayedText] = useState('')
  const [currentIndex, setCurrentIndex] = useState(0)
  const [isComplete, setIsComplete] = useState(false)

  useEffect(() => {
    if (!text) return

    const timer = setTimeout(() => {
      if (currentIndex < text.length) {
        setDisplayedText(prev => prev + text[currentIndex])
        setCurrentIndex(prev => prev + 1)
      } else if (!isComplete) {
        setIsComplete(true)
        onComplete?.()
      }
    }, currentIndex === 0 ? startDelay : speed)

    return () => clearTimeout(timer)
  }, [text, currentIndex, speed, onComplete, startDelay, isComplete])

  // Reset when text changes
  useEffect(() => {
    setDisplayedText('')
    setCurrentIndex(0)
    setIsComplete(false)
  }, [text])

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5 }}
      className={`story-text ${className}`}
    >
      {displayedText}
      {!isComplete && (
        <motion.span
          animate={{ opacity: [1, 0] }}
          transition={{ duration: 0.8, repeat: Infinity }}
          className="inline-block w-0.5 h-6 bg-white ml-1"
        />
      )}
    </motion.div>
  )
}

export default TypewriterEffect