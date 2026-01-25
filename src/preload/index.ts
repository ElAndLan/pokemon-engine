import { contextBridge, ipcRenderer } from 'electron';
console.log('Preload script loaded!');

contextBridge.exposeInMainWorld('fs', {
  readFile: (path: string) => ipcRenderer.invoke('read-file', path),
  readImage: (path: string) => ipcRenderer.invoke('read-image', path),
  writeFile: (path: string, content: string) => ipcRenderer.invoke('write-file', path, content),
  listDir: (path: string) => ipcRenderer.invoke('list-dir', path),
  deleteFile: (path: string) => ipcRenderer.invoke('delete-file', path),
  renameFile: (oldPath: string, newPath: string) => ipcRenderer.invoke('rename-file', oldPath, newPath),
  createDirectory: (path: string) => ipcRenderer.invoke('create-directory', path),
  log: (msg: string) => ipcRenderer.invoke('log-message', msg),
  showOpenDialog: (options: any) => ipcRenderer.invoke('show-open-dialog', options),
  readEditorConfig: () => ipcRenderer.invoke('read-editor-config'),
  writeEditorConfig: (config: any) => ipcRenderer.invoke('write-editor-config', config),
  readScripts: () => ipcRenderer.invoke('read-file', 'data/db/scripts.json') // Reuse read-file
});
