/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        'fairy': ['Comfortaa', 'cursive'],
        'story': ['Merriweather', 'serif']
      },
      colors: {
        border: "rgba(255, 255, 255, 0.2)", // Uma borda branca semi-transparente para o efeito glass
        magic: {
          50: '#fdf4ff',
          100: '#fae8ff',
          200: '#f5d0fe',
          300: '#f0abfc',
          400: '#e879f9',
          500: '#d946ef',
          600: '#c026d3',
          700: '#a21caf',
          800: '#86198f',
          900: '#701a75'
        },
        dream: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          200: '#bae6fd',
          300: '#7dd3fc',
          400: '#38bdf8',
          500: '#0ea5e9',
          600: '#0284c7',
          700: '#0369a1',
          800: '#075985',
          900: '#0c4a6e'
        }
      },
      animation: {
        'float': 'float 6s ease-in-out infinite',
        'pulse-soft': 'pulse-soft 2s ease-in-out infinite',
        'ken-burns': 'ken-burns 20s ease-out infinite',
        'sparkle': 'sparkle 1.5s ease-in-out infinite'
      },
      keyframes: {
        float: {
          '0%, 100%': { transform: 'translateY(0px)' },
          '50%': { transform: 'translateY(-10px)' }
        },
        'pulse-soft': {
          '0%, 100%': { opacity: '0.6' },
          '50%': { opacity: '1' }
        },
        'ken-burns': {
          '0%': { transform: 'scale(1.0)' },
          '100%': { transform: 'scale(1.15)' }
        },
        sparkle: {
          '0%, 100%': { opacity: '0', transform: 'scale(0)' },
          '50%': { opacity: '1', transform: 'scale(1)' }
        }
      }
    },
  },
  plugins: [],
}