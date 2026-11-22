/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50:  '#eff6ff',
          100: '#dbeafe',
          500: '#3b82f6',
          600: '#2563eb',
          700: '#1d4ed8',
          800: '#1e40af',
          900: '#1e3a5f',
        },
        ensa: {
          blue:  '#0070f3',
          navy:  '#1e3a5f',
          sky:   '#38bdf8',
        }
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'Segoe UI', 'sans-serif'],
      },
      boxShadow: {
        card: '0 2px 8px rgba(0,0,0,0.07)',
        elevated: '0 8px 32px rgba(0,112,243,0.18)',
      },
      borderRadius: {
        xl2: '1rem',
        xl3: '1.25rem',
      }
    },
  },
  plugins: [],
  // Important to avoid conflicts with SAP UI5 components
  corePlugins: {
    preflight: false,
  },
}

