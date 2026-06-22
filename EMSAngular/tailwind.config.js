/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
        ink: "#241B2E",
        "ink-soft": "#4A4358",
        muted: "#6B6577",
        paper: "#F4F3F7",
        surface: "#FFFFFF",
        line: "#E6E3EC",
        plum: { DEFAULT: "#A02B72", dark: "#7E2059", tint: "#FBEFF6" },
        teal: { DEFAULT: "#0E8C7F", dark: "#0B6F65", tint: "#E4F3F1" },
        gold: { DEFAULT: "#B45309", tint: "#FBF0E2" },
        rose: { DEFAULT: "#B23A48", dark: "#963041", tint: "#FBECEE" },
      },
      fontFamily: {
        display: ['Fraunces', 'Georgia', 'serif'],
        sans: ['"Hanken Grotesk"', 'system-ui', 'sans-serif'],
        mono: ['"Space Mono"', 'ui-monospace', 'monospace'],
      },
      boxShadow: {
        card: "0 1px 2px rgba(36,27,46,0.04), 0 10px 30px -18px rgba(36,27,46,0.22)",
        lift: "0 18px 50px -22px rgba(36,27,46,0.32)",
      },
      letterSpacing: {
        eyebrow: "0.22em",
      },
    },
  },
  plugins: [],
};
