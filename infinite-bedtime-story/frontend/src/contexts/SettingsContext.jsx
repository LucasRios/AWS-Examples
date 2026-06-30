import React, { createContext, useContext, useState, useEffect } from 'react'

const SettingsContext = createContext()

export const useSettings = () => {
  const context = useContext(SettingsContext)
  if (!context) {
    throw new Error('useSettings must be used within a SettingsProvider')
  }
  return context
}

const defaultSettings = {
  // AWS Configuration
  awsAccessKey: 'AKI...',
  awsSecretKey: 'xci...',
  awsRegion: 'us-east-1',
  
  // Model IDs
  modelIds: {
    text: 'amazon.nova-micro-v1:0',
    audio: 'amazon.nova-2-sonic-v1:0',
    image: 'amazon.nova-canvas-v1:0'
  },
  
  // Feature Toggles
  features: {
    generateText: false,
    generateVoice: false,
    generateImage: false,
    looping: false
  },
  
  // Story Configuration
  storyConfig: {
    heroName: '',
    heroAge: '',
    adventureTheme: '',
    maxInteractions: 10
  }
}

export const SettingsProvider = ({ children }) => {
  const [settings, setSettings] = useState(defaultSettings)
  const [isLoading, setIsLoading] = useState(true)

  // Load settings from localStorage on mount
  useEffect(() => {
    try {
      const savedSettings = localStorage.getItem('bedtime-story-settings')
      if (savedSettings) {
        const parsed = JSON.parse(savedSettings)
        setSettings(prev => ({ ...prev, ...parsed }))
      }
    } catch (error) {
      console.error('Failed to load settings:', error)
    } finally {
      setIsLoading(false)
    }
  }, [])

  // Save settings to localStorage whenever they change
  useEffect(() => {
    if (!isLoading) {
      try {
        localStorage.setItem('bedtime-story-settings', JSON.stringify(settings))
      } catch (error) {
        console.error('Failed to save settings:', error)
      }
    }
  }, [settings, isLoading])

  const updateSettings = (newSettings) => {
    setSettings(prev => ({
      ...prev,
      ...newSettings
    }))
  }

  const updateStoryConfig = (config) => {
    setSettings(prev => ({
      ...prev,
      storyConfig: {
        ...prev.storyConfig,
        ...config
      }
    }))
  }

  const updateFeatures = (features) => {
    setSettings(prev => ({
      ...prev,
      features: {
        ...prev.features,
        ...features
      }
    }))
  }

  const resetSettings = () => {
    setSettings(defaultSettings)
    localStorage.removeItem('bedtime-story-settings')
  }

  const isConfigured = () => {
    return settings.awsAccessKey && settings.awsSecretKey && settings.awsRegion
  }

  const value = {
    settings,
    updateSettings,
    updateStoryConfig,
    updateFeatures,
    resetSettings,
    isConfigured,
    isLoading
  }

  return (
    <SettingsContext.Provider value={value}>
      {children}
    </SettingsContext.Provider>
  )
}