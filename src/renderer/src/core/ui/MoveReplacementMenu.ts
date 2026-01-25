import { Menu } from './MenuSystem';
import { Game } from '../Game';
import { PokemonInstance } from '../data/DataTypes';
import type { MoveData } from '../data/DataTypes';

export class MoveReplacementMenu implements Menu {
    private game: Game;
    private pokemon: PokemonInstance;
    private newMoveData: MoveData;
    private selection: number = 0;
    private newMoveSelected: boolean = false;

    public onResult: ((replaced: boolean, oldMoveId?: string) => void) | null = null;

    constructor(game: Game, pokemon: PokemonInstance, newMoveData: MoveData) {
        this.game = game;
        this.pokemon = pokemon;
        this.newMoveData = newMoveData;
        this.selection = 0;
    }

    public async onOpen(): Promise<void> {
        console.log('[MoveReplacementMenu] Opened');
        this.selection = 0;
        this.newMoveSelected = false;
    }

    public onClose(): void {
        console.log('[MoveReplacementMenu] Closed');
    }

    public update(dt: number): void {
        const input = this.game.input;

        const totalOptions = this.pokemon.moves.length + 2;

        if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
            this.selection = (this.selection + 1) % totalOptions;
        }
        if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
            this.selection = (this.selection - 1 + totalOptions) % totalOptions;
        }

        if (input.isJustPressed('Escape')) {
            if (this.onResult) {
                this.onResult(false);
            }
        }

        if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
            const lastIndex = this.pokemon.moves.length + 1;

            if (this.selection === lastIndex) {
                if (this.onResult) {
                    this.onResult(false);
                }
            } else if (this.selection < this.pokemon.moves.length) {
                if (this.onResult) {
                    const oldMoveId = this.pokemon.moves[this.selection].moveId;
                    this.onResult(true, oldMoveId);
                }
            }
        }
    }

    public render(ctx: CanvasRenderingContext2D): void {
        const width = 400;
        const height = 320;
        const x = (this.game.canvas.width - width) / 2;
        const y = (this.game.canvas.height - height) / 2;

        ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
        ctx.fillRect(x, y, width, height);

        ctx.fillStyle = '#f8f8f8';
        ctx.strokeStyle = '#303030';
        ctx.lineWidth = 4;
        ctx.fillRect(x, y, width, height);
        ctx.strokeRect(x, y, width, height);

        ctx.fillStyle = '#303030';
        ctx.font = 'bold 18px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(`${this.pokemon.nickname || this.pokemon.speciesId}`, x + width / 2, y + 30);

        ctx.font = '14px sans-serif';
        ctx.fillText(`Which move to forget?`, x + width / 2, y + 55);

        ctx.textAlign = 'left';
        let currentY = y + 90;
        const lineHeight = 35;

        for (let i = 0; i < this.pokemon.moves.length; i++) {
            const moveId = this.pokemon.moves[i].moveId;
            const moveData = this.game.dataManager.getMove(moveId);
            
            if (i === this.selection) {
                ctx.fillStyle = '#303030';
                ctx.fillRect(x + 20, currentY - 20, width - 40, 28);
                ctx.fillStyle = '#ffffff';
            } else {
                ctx.fillStyle = '#303030';
            }

            const moveName = moveData?.name || moveId;
            const pp = `${this.pokemon.moves[i].pp}/${this.pokemon.moves[i].maxPp}`;
            
            ctx.font = 'bold 14px sans-serif';
            ctx.fillText(moveName, x + 30, currentY);
            
            ctx.font = '12px sans-serif';
            ctx.textAlign = 'right';
            ctx.fillText(`PP: ${pp}`, x + width - 30, currentY);
            ctx.textAlign = 'left';

            currentY += lineHeight;
        }

        currentY += 10;
        
        const lastIndex = this.pokemon.moves.length + 1;
        
        if (this.selection === lastIndex) {
            ctx.fillStyle = '#303030';
            ctx.fillRect(x + 20, currentY - 20, width - 40, 28);
            ctx.fillStyle = '#ffffff';
        } else {
            ctx.fillStyle = '#303030';
        }
        
        ctx.textAlign = 'center';
        ctx.font = 'bold 14px sans-serif';
        ctx.fillText('STOP', x + width / 2, currentY);

        currentY += lineHeight + 15;

        ctx.fillStyle = '#303030';
        ctx.font = '12px sans-serif';
        const moveDescription = this.newMoveData.description || this.newMoveData.effect || 'No description available.';
        const words = moveDescription.split(' ');
        let line = '';
        const maxWidth = width - 60;
        
        for (let i = 0; i < words.length; i++) {
            const testLine = line + words[i] + ' ';
            const metrics = ctx.measureText(testLine);
            if (metrics.width > maxWidth && i > 0) {
                ctx.fillText(line.trim(), x + width / 2, currentY);
                line = words[i] + ' ';
                currentY += 16;
            } else {
                line = testLine;
            }
        }
        ctx.fillText(line.trim(), x + width / 2, currentY);
    }

    public isActive(): boolean {
        return true;
    }
}
