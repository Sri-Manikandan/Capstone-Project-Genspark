/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
        ink: "#F5F3FA",
        "ink-soft": "#B7B1C4",
        muted: "#8A8398",
        paper: "#0E0B14",
        surface: "#17131F",
        line: "#2A2435",
        plum: { DEFAULT: "#8B5CF6", dark: "#7C3AED", tint: "#211A38" },
        teal: { DEFAULT: "#2DD4BF", dark: "#14B8A6", tint: "#10241F" },
        gold: { DEFAULT: "#F59E0B", tint: "#2A200E" },
        rose: { DEFAULT: "#FB7185", dark: "#F43F5E", tint: "#2C1620" },
      },
      fontFamily: {
        display: ['"Space Grotesk"', 'system-ui', 'sans-serif'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['"Space Mono"', 'ui-monospace', 'monospace'],
      },
      boxShadow: {
        card: "0 1px 2px rgba(0,0,0,0.4), 0 12px 30px -18px rgba(0,0,0,0.7)",
        lift: "0 22px 55px -22px rgba(0,0,0,0.8)",
      },
      letterSpacing: {
        eyebrow: "0.22em",
      },
    },
  },
  plugins: [],
};
