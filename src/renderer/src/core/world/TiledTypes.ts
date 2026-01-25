export interface TiledMap {
  width: number;
  height: number;
  tilewidth: number;
  tileheight: number;
  orientation: string;
  layers: (TiledTileLayer | TiledObjectLayer)[];
  tilesets: TiledTileset[];
  properties?: TiledProperty[];
}

export interface TiledLayerBase {
  id: number;
  name: string;
  type: string;
  visible: boolean;
  opacity: number;
  x: number;
  y: number;
}

export interface TiledTileLayer extends TiledLayerBase {
  type: 'tilelayer';
  width: number;
  height: number;
  data: number[];
}

export interface TiledObjectLayer extends TiledLayerBase {
  type: 'objectgroup';
  objects: TiledObject[];
}

export interface TiledObject {
  id: number;
  name: string;
  type: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  visible: boolean;
  properties?: TiledProperty[];
}

export interface TiledProperty {
  name: string;
  type: string; // string, int, float, bool, color, file
  value: any;
}

export interface TiledTileset {
  firstgid: number;
  name: string;
  image: string;
  imagewidth: number;
  imageheight: number;
  tilewidth: number;
  tileheight: number;
  tilecount: number;
  columns: number;
}
