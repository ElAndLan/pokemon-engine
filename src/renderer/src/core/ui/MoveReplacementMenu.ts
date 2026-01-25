import { Menu } from './MenuSystem';
import { Game } from '../Game';
import { PokemonInstance } from '../data/DataTypes';
import type { MoveData } from '../data/DataTypes';

export class MoveReplacementMenu implements Menu {
    private game: Game;
    private pokemon: PokemonInstance;
    private newMoveData: MoveData;
    private selection: number = 0;

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
    }

    public onClose(): void {
        console.log('[MoveReplacementMenu] Closed');
    }

    public update(dt: number): void {
        const input = this.game.input;

        const totalOptions = this.pokemon.moves.length + 1;

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
            if (this.selection === this.pokemon.moves.length) {
                if (this.onResult) {
                    this.onResult(false);
                }
            } else {
                if (this.onResult) {
                    const oldMoveId = this.pokemon.moves[this.selection].moveId;
                    this.onResult(true, oldMoveId);
                }
            }
        }
    }

    public render(ctx: CanvasRenderingContext2D): void {
        const width = 320;
        const height = 200;
        const x = Math.floor((this.game.display.width - width) / 2);
        const y = Math.floor((this.game.display.height - height) / 2);

        const borderColor = '#303030';
        const bgColor = '#ffffff';
        const highlightColor = '#a0d8ef';

        ctx.fillStyle = bgColor;
        ctx.strokeStyle = borderColor;
        ctx.lineWidth = 4;

        ctx.fillRect(x, y, width, height);
        ctx.strokeRect(x, y, width, height);

        const padding = 8;
        const startY = y + padding;
        const moveHeight = 30;

        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';

        for (let i = 0; i <= this.pokemon.moves.length; i++) {
            const moveY = startY + i * moveHeight;

            if (i === this.selection) {
                ctx.fillStyle = highlightColor;
                ctx.fillRect(x + 2, moveY, width - 4, moveHeight - 2);
            }

            ctx.fillStyle = '#000000';

            if (i < this.pokemon.moves.length) {
                const moveId = this.pokemon.moves[i].moveId;
                const moveData = this.game.dataManager.getMove(moveId);
                const moveName = moveData?.name || moveId;
                const pp = `${this.pokemon.moves[i].pp}/${this.pokemon.moves[i].maxPp}`;

                ctx.font = 'bold 13px sans-serif';
                ctx.fillText(moveName, x + padding, moveY + moveHeight / 2);

                ctx.font = '12px sans-serif';
                ctx.textAlign = 'right';
                ctx.fillText(`PP:${pp}`, x + width - padding, moveY + moveHeight / 2);
                ctx.textAlign = 'left';
            } else {
                ctx.font = 'bold 13px sans-serif';
                ctx.fillText('STOP', x + padding, moveY + moveHeight / 2);
            }
        }
    }

    public isActive(): boolean {
        return true;
    }
}
