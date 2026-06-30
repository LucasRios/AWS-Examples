import React, { useState } from 'react'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import { 
  ArrowLeft, 
  Save, 
  RotateCcw, 
  Eye, 
  EyeOff,
  CheckCircle,
  AlertCircle,
  Settings as SettingsIcon
} from 'lucide-react'
import { useSettings } from '../contexts/SettingsContext'

const Settings = () => {
  const navigate = useNavigate()
  const { settings, updateSettings, updateFeatures, resetSettings, isConfigured } = useSettings()
  const [showSecrets, setShowSecrets] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [localSettings, setLocalSettings] = useState(settings)

  const handleSave = async () => {
    setIsSaving(true)
    
    // Simulate API call delay
    await new Promise(resolve => setTimeout(resolve, 1000))
    
    updateSettings(localSettings)
    setIsSaving(false)
  }

  const handleReset = () => {
    if (confirm('Are you sure you want to reset all settings?')) {
      resetSettings()
      setLocalSettings(settings)
    }
  }

  const handleFeatureToggle = (feature) => {
    const newFeatures = {
      ...localSettings.features,
      [feature]: !localSettings.features[feature]
    }
    setLocalSettings(prev => ({
      ...prev,
      features: newFeatures
    }))
  }

  return (
    <div className="h-screen w-full overflow-y-auto p-6 scrollbar-thin scrollbar-thumb-magic-500">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between mb-8"
      >
        <div className="flex items-center gap-4">
          <motion.button
            onClick={() => navigate('/')}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            className="glass-panel p-3 text-white hover:bg-white/20 transition-all"
          >
            <ArrowLeft className="w-5 h-5" />
          </motion.button>
          
          <div>
            <h1 className="text-3xl font-bold text-white flex items-center gap-3">
              <SettingsIcon className="w-8 h-8 text-magic-400" />
              Settings
            </h1>
            <p className="text-white/70">Customize your magical experience</p>
          </div>
        </div>

        {/* Status indicator */}
        <motion.div
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
          className={`flex items-center gap-2 px-4 py-2 rounded-full ${
            isConfigured() 
              ? 'bg-green-500/20 text-green-300 border border-green-500/30' 
              : 'bg-red-500/20 text-red-300 border border-red-500/30'
          }`}
        >
          {isConfigured() ? (
            <>
              <CheckCircle className="w-4 h-4" />
              Configured
            </>
          ) : (
            <>
              <AlertCircle className="w-4 h-4" />
              Not configured
            </>
          )}
        </motion.div>
      </motion.div>

      <div className="max-w-4xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* AWS Configuration */}
        <motion.div
          initial={{ opacity: 0, x: -50 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.1 }}
          className="glass-panel p-6"
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-2">
            🔐 AWS Credentials
          </h2>

          <div className="space-y-4">
            <div>
              <label className="block text-white/80 mb-2">Access Key ID</label>
              <input
                type={showSecrets ? 'text' : 'password'}
                value={localSettings.awsAccessKey}
                onChange={(e) => setLocalSettings(prev => ({
                  ...prev,
                  awsAccessKey: e.target.value
                }))}
                className="wizard-input"
                placeholder="AKIA..."
              />
            </div>

            <div>
              <label className="block text-white/80 mb-2">Secret Access Key</label>
              <div className="relative">
                <input
                  type={showSecrets ? 'text' : 'password'}
                  value={localSettings.awsSecretKey}
                  onChange={(e) => setLocalSettings(prev => ({
                    ...prev,
                    awsSecretKey: e.target.value
                  }))}
                  className="wizard-input pr-12"
                  placeholder="wJalrXUtnFEMI/K7MDENG..."
                />
                <button
                  onClick={() => setShowSecrets(!showSecrets)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-white/50 hover:text-white"
                >
                  {showSecrets ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-white/80 mb-2">Region</label>
              <select
                value={localSettings.awsRegion}
                onChange={(e) => setLocalSettings(prev => ({
                  ...prev,
                  awsRegion: e.target.value
                }))}
                className="wizard-input"
              >
                <option value="us-east-1">us-east-1 (N. Virginia)</option>
                <option value="us-west-2">us-west-2 (Oregon)</option>
                <option value="eu-west-1">eu-west-1 (Ireland)</option>
                <option value="ap-northeast-1">ap-northeast-1 (Tokyo)</option>
              </select>
            </div>
          </div>
        </motion.div>

        {/* Model Configuration */}
        <motion.div
          initial={{ opacity: 0, x: 50 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.2 }}
          className="glass-panel p-6"
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-2">
            🤖 Amazon Nova Models
          </h2>

          <div className="space-y-4">
            <div>
              <label className="block text-white/80 mb-2">Text (Nova Micro/Pro)</label>
              <select
                value={localSettings.modelIds.text}
                onChange={(e) => setLocalSettings(prev => ({
                  ...prev,
                  modelIds: { ...prev.modelIds, text: e.target.value }
                }))}
                className="wizard-input"
              >
                <option value="amazon.nova-micro-v1:0">Nova Micro (Fast)</option>
                <option value="amazon.nova-pro-v1:0">Nova Pro (Advanced)</option>
              </select>
            </div>

            <div>
              <label className="block text-white/80 mb-2">Audio (Nova Sonic)</label>
              <input
                type="text"
                value={localSettings.modelIds.audio}
                onChange={(e) => setLocalSettings(prev => ({
                  ...prev,
                  modelIds: { ...prev.modelIds, audio: e.target.value }
                }))}
                className="wizard-input"
                placeholder="amazon.nova-2-sonic-v1:0"
              />
            </div>

            <div>
              <label className="block text-white/80 mb-2">Image (Nova Canvas)</label>
              <input
                type="text"
                value={localSettings.modelIds.image}
                onChange={(e) => setLocalSettings(prev => ({
                  ...prev,
                  modelIds: { ...prev.modelIds, image: e.target.value }
                }))}
                className="wizard-input"
                placeholder="amazon.nova-canvas-v1:0"
              />
            </div>
          </div>
        </motion.div>

        {/* Feature Toggles */}
        <motion.div
          initial={{ opacity: 0, y: 50 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
          className="glass-panel p-6 lg:col-span-2"
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-2">
            ⚡ Features
          </h2>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {[
              { key: 'generateText', label: 'Generate Text', icon: '📝' },
              { key: 'generateVoice', label: 'Generate Voice', icon: '🔊' },
              { key: 'generateImage', label: 'Generate Image', icon: '🎨' },
              { key: 'looping', label: 'Continuous Loop', icon: '🔄' }
            ].map((feature) => (
              <motion.button
                key={feature.key}
                onClick={() => handleFeatureToggle(feature.key)}
                whileHover={{ scale: 1.02 }}
                whileTap={{ scale: 0.98 }}
                className={`p-4 rounded-xl border-2 transition-all duration-300 ${
                  localSettings.features[feature.key]
                    ? 'bg-magic-500/20 border-magic-400 text-magic-200'
                    : 'bg-white/5 border-white/20 text-white/60 hover:bg-white/10'
                }`}
              >
                <div className="text-2xl mb-2">{feature.icon}</div>
                <div className="text-sm font-medium">{feature.label}</div>
              </motion.button>
            ))}
          </div>
        </motion.div>

        {/* Story Configuration */}
        <motion.div
          initial={{ opacity: 0, y: 50 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.35 }}
          className="glass-panel p-6 lg:col-span-2"
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-2">
            📖 Story Configuration
          </h2>

          <div className="max-w-md">
            <label className="block text-white/80 mb-2">
              Number of Interactions (Story Length)
            </label>
            <input
              type="number"
              min="5"
              max="50"
              value={localSettings.storyConfig?.maxInteractions || 10}
              onChange={(e) => setLocalSettings(prev => ({
                ...prev,
                storyConfig: {
                  ...prev.storyConfig,
                  maxInteractions: parseInt(e.target.value) || 10
                }
              }))}
              className="wizard-input"
            />
            <p className="text-white/50 text-sm mt-2">
              Each interaction is a story segment. More interactions = longer story.
              Recommended: 10-15 for bedtime stories.
            </p>
          </div>
        </motion.div>
      </div>

      {/* Action buttons */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.4 }}
        className="flex justify-center gap-4 mt-8"
      >
        <motion.button
          onClick={handleSave}
          disabled={isSaving}
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="magic-button flex items-center gap-2 disabled:opacity-50"
        >
          <Save className="w-5 h-5" />
          {isSaving ? 'Saving...' : 'Save Settings'}
        </motion.button>

        <motion.button
          onClick={handleReset}
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="glass-panel px-6 py-3 text-white hover:bg-red-500/20 hover:border-red-400/50 transition-all flex items-center gap-2"
        >
          <RotateCcw className="w-5 h-5" />
          Reset
        </motion.button>
      </motion.div>
    </div>
  )
}

export default Settings