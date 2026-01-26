import { WeatherType } from './data/DataTypes';
import { Game } from './Game';

export class WeatherManager {
    private game: Game;
    public currentWeather: WeatherType = 'None';
    
    // Duration Logic for Overworld?
    // Usually Overworld weather is permanent until map change or scripted event.
    // Battle weather can be temporary.
    
    constructor(game: Game) {
        this.game = game;
    }

    public setWeather(type: WeatherType): void {
        if (this.currentWeather !== type) {
            console.log(`[WeatherManager] Weather changed from ${this.currentWeather} to ${type}`);
            this.currentWeather = type;
            // TODO: Trigger visual effects or notifications
        }
    }

    public update(dt: number): void {
        // Potential for dynamic weather changes or particle updates
    }
}
