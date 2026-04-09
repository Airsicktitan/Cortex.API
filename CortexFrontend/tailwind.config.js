/** @type {import('tailwindcss').Config} */
export default {
  darkMode: "class",
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        "cortex-blue": "#0b9b87",
        "cortex-blue-dark": "#087062",
        "cortex-blue-soft": "#dff4ee",
        "cortex-cyan": "#46c7b2",
        "cortex-ink": "#17343a",
        "cortex-ink-dark": "#0f2429",
        "cortex-surface": "#f4f8f6",
        "cortex-surface-alt": "#deebe7",
      },
    },
  },
  plugins: [],
};
