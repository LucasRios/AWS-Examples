import React from 'react'
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { SettingsProvider } from './contexts/SettingsContext'
import Home from './pages/Home'
import Settings from './pages/Settings'
import StoryView from './pages/StoryView'

function App() {
  return (
    <SettingsProvider>
      <Router>
        <div className="min-h-screen bg-gradient-to-br from-purple-900 via-blue-900 to-indigo-900">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="/story" element={<StoryView />} />
          </Routes>
        </div>
      </Router>
    </SettingsProvider>
  )
}

export default App