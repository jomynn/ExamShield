import type { Config } from 'tailwindcss'

export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
      colors: {
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        border: 'hsl(var(--border))',
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
      },
      borderRadius: {
        sm: 'calc(var(--radius) - 4px)',
        md: 'calc(var(--radius) - 2px)',
        lg: 'var(--radius)',
        xl: 'calc(var(--radius) + 4px)',
        '2xl': '1rem',
        '3xl': '1.5rem',
        '4xl': '2rem',
      },
      backdropBlur: {
        xs: '2px',
        '2xl': '40px',
        '3xl': '60px',
      },
      boxShadow: {
        glass: '0 4px 24px rgba(0,0,0,0.08), inset 0 1px 0 rgba(255,255,255,0.06)',
        'glass-lg': '0 12px 48px rgba(0,0,0,0.12), inset 0 1px 0 rgba(255,255,255,0.08)',
        'glass-dark': '0 8px 32px rgba(0,0,0,0.32), inset 0 1px 0 rgba(255,255,255,0.06)',
        'glow-blue': '0 0 20px rgba(79,142,247,0.25)',
        'glow-sm': '0 0 8px rgba(79,142,247,0.2)',
      },
      animation: {
        'fade-in': 'fadeIn 0.25s ease',
        'slide-up': 'slideUp 0.3s ease',
        'slide-in-left': 'slideInLeft 0.25s ease',
        'aurora': 'aurora 20s ease-in-out infinite',
        'pulse-slow': 'pulse 4s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'spin-slow': 'spin 3s linear infinite',
      },
      keyframes: {
        fadeIn: {
          from: { opacity: '0' },
          to: { opacity: '1' },
        },
        slideUp: {
          from: { opacity: '0', transform: 'translateY(10px)' },
          to: { opacity: '1', transform: 'translateY(0)' },
        },
        slideInLeft: {
          from: { opacity: '0', transform: 'translateX(-8px)' },
          to: { opacity: '1', transform: 'translateX(0)' },
        },
        aurora: {
          '0%, 100%': { backgroundPosition: '0% 50%' },
          '33%': { backgroundPosition: '100% 50%' },
          '66%': { backgroundPosition: '50% 100%' },
        },
      },
      transitionDuration: {
        '150': '150ms',
        '250': '250ms',
      },
      spacing: {
        '18': '4.5rem',
        '22': '5.5rem',
      },
    },
  },
  plugins: [],
} satisfies Config
