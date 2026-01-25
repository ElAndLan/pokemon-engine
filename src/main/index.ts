import { app, BrowserWindow, ipcMain } from 'electron';
import { join } from 'path';
import { promises as fs } from 'fs';

function createWindow() {
  console.log('[Main] Creating Window');
  const preloadPath = join(__dirname, '../preload/index.js');
  console.log('[Main] Preload Path:', preloadPath);

  const mainWindow = new BrowserWindow({
    width: 980, // 960 + frame padding
    height: 700, // 640 + frame padding
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: preloadPath,
      sandbox: false,
      contextIsolation: true
    }
  });

  mainWindow.on('ready-to-show', () => {
    mainWindow.maximize();
    mainWindow.show();
  });

  if (process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL']);
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'));
  }
}

// IPC Handlers
ipcMain.handle('read-file', async (_event, path: string) => {
  console.log(`[Main] Request to read file: ${path}`);
  // ... existing code ...
  try {
    const data = await fs.readFile(path, 'utf-8');
    // ...
    return { success: true, data };
  } catch (error: any) {
    // ...
    return { success: false, error: error.message };
  }
});

ipcMain.handle('read-image', async (_event, path: string) => {
  console.log(`[Main] Request to read image: ${path}`);
  try {
    const data = await fs.readFile(path, { encoding: 'base64' });
    console.log(`[Main] Successfully read image. Length: ${data.length}`);
    return { success: true, data }; // Returns Base64 string
  } catch (error: any) {
    console.error(`[Main] Error reading image:`, error);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('write-file', async (_event, path: string, content: string) => {
  try {
    const absPath = require('path').resolve(path);
    console.log(`[Main] Writing to: ${absPath}`);
    await fs.writeFile(absPath, content, 'utf-8');
    return { success: true, path: absPath };
  } catch (error: any) {
    return { success: false, error: error.message };
  }
});

ipcMain.handle('log-message', async (_event, msg: string) => {
  console.log(`[Renderer] ${msg}`);
  return true;
});

ipcMain.handle('list-dir', async (_event, path: string) => {
  const absPath = require('path').resolve(path);
  console.log(`[Main] Request to list directory: ${path} (Resolved: ${absPath})`);
  try {
    const files = await fs.readdir(absPath, { withFileTypes: true });
    const result = files.map(file => ({
      name: file.name,
      isDirectory: file.isDirectory()
    }));
    return { success: true, files: result };
  } catch (error: any) {
    console.error(`[Main] Error listing directory:`, error);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('delete-file', async (_event, path: string) => {
  const absPath = require('path').resolve(path);
  console.log(`[Main] Request to delete file: ${path} (Resolved: ${absPath})`);
  try {
    await fs.unlink(absPath);
    return { success: true };
  } catch (error: any) {
    console.error(`[Main] Error deleting file:`, error);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('rename-file', async (_event, oldPath: string, newPath: string) => {
  const absOld = require('path').resolve(oldPath);
  const absNew = require('path').resolve(newPath);
  console.log(`[Main] Request to rename: ${oldPath} -> ${newPath}`);
  try {
    await fs.rename(absOld, absNew);
    return { success: true };
  } catch (error: any) {
    console.error(`[Main] Error renaming:`, error);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('create-directory', async (_event, path: string) => {
  const absPath = require('path').resolve(path);
  console.log(`[Main] Request to create directory: ${path} (Resolved: ${absPath})`);
  try {
    await fs.mkdir(absPath, { recursive: true });
    return { success: true };
  } catch (error: any) {
    console.error(`[Main] Error creating directory:`, error);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('show-open-dialog', async (_event, options: any) => {
  const { dialog } = require('electron');
  try {
    const result = await dialog.showOpenDialog(options);
    return { success: true, canceled: result.canceled, filePaths: result.filePaths };
  } catch (error: any) {
    return { success: false, error: error.message };
  }
});

ipcMain.handle('read-editor-config', async () => {
  const configPath = join(app.getPath('userData'), 'editor_config.json');
  console.log(`[Main] Reading editor config from: ${configPath}`);
  try {
    const data = await fs.readFile(configPath, 'utf-8');
    console.log(`[Main] Config data read: ${data}`);
    return { success: true, config: JSON.parse(data) };
  } catch (error: any) {
    console.warn(`[Main] No config found: ${error.message}`);
    return { success: false, error: error.message };
  }
});

ipcMain.handle('write-editor-config', async (_event, config: any) => {
  const configPath = join(app.getPath('userData'), 'editor_config.json');
  console.log(`[Main] Writing editor config to: ${configPath}`);
  console.log(`[Main] Config content:`, config);
  try {
    await fs.writeFile(configPath, JSON.stringify(config, null, 2), 'utf-8');
    return { success: true };
  } catch (error: any) {
    console.error(`[Main] Failed to write config:`, error);
    return { success: false, error: error.message };
  }
});

app.whenReady().then(() => {
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
