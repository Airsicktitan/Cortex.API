export const THEME_STORAGE_KEY = "cortex-theme";

export type ThemeMode = "light" | "dark";

function isThemeMode(value: string | null): value is ThemeMode {
  return value === "light" || value === "dark";
}

export function getPreferredTheme(): ThemeMode {
  if (typeof window === "undefined") {
    return "light";
  }

  const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
  if (isThemeMode(storedTheme)) {
    return storedTheme;
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

export function applyTheme(theme: ThemeMode) {
  if (typeof document === "undefined") {
    return;
  }

  const isDarkMode = theme === "dark";
  const root = document.documentElement;
  const body = document.body;

  root.classList.toggle("dark", isDarkMode);
  root.dataset.theme = theme;
  root.style.colorScheme = theme;

  if (body) {
    body.classList.toggle("dark", isDarkMode);
    body.dataset.theme = theme;
  }

  if (typeof window !== "undefined") {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  }
}
