/// <reference types="vite/client" />

interface Window {
  fs: {
    readFile: (path: string) => Promise<{ success: boolean; data?: string; error?: string }>;
    writeFile: (path: string, content: string) => Promise<{ success: boolean; error?: string }>;
    log: (msg: string) => Promise<boolean>;
  }
}
