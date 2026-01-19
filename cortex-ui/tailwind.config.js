/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        "cortex-blue": "#1e40af",
        "cortex-cyan": "#06b6d4",
      },
    },
  },
  plugins: [],
};
