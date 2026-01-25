import { TiledMap } from './TiledTypes';

export class MapLoader {
  /**
   * Loads a Tiled Map JSON file from the given absolute path.
   * @param path Absolute path to the .tmj or .json file.
   */
  public async loadMap(path: string): Promise<TiledMap | null> {
    try {
      console.log(`[MapLoader] Loading map from: ${path}`);
      const result = await window.fs.readFile(path);
      
      if (!result.success || !result.data) {
        console.error(`[MapLoader] Failed to read file: ${result.error}`);
        return null;
      }

      const mapData: TiledMap = JSON.parse(result.data);
      
      // Basic validation
      if (!mapData.layers || !mapData.width || !mapData.height) {
        console.error('[MapLoader] Invalid Tiled Map data structures.');
        return null;
      }

      console.log(`[MapLoader] Successfully loaded map: ${mapData.width}x${mapData.height}`);
      // @ts-ignore
      if (window.fs && window.fs.log) window.fs.log(`Map Loaded: ${path}`);
      return mapData;

    } catch (e) {
      console.error(`[MapLoader] Error parsing map JSON: ${e}`);
      return null;
    }
  }
}
