import { Menu } from './MenuSystem';
import { Game } from '../Game';
import { PokemonInstance } from '../data/DataTypes';

export class PCMenu implements Menu {
    private game: Game;
    private mode: 'main' | 'withdraw' | 'deposit' = 'main';
    private selection: number = 0; // Grid Index (0-29) or Menu Index or Party Index
    
    // Navigation State
    private isInHeader: boolean = false;

    // Grid View State
    private currentBoxIndex: number = 0;
    private readonly GRID_COLS = 6;
    private readonly GRID_ROWS = 5;
    private readonly BOX_CAPACITY = 30;

    // Assets
    private iconCache: Map<string, HTMLImageElement> = new Map();
    private isLoadingIcons: boolean = false;

    constructor(game: Game) {
        this.game = game;
        this.currentBoxIndex = this.game.storageSystem.currentBox; // Restore last box state?
    }

    public onOpen(): void {
        console.log('[PCMenu] Opened');
        this.mode = 'main';
        this.selection = 0;
        this.isInHeader = false;
        this.currentBoxIndex = 0;
        this.loadIconsForBox(0); // Preload first box
    }

    public onClose(): void {
        console.log('[PCMenu] Closed');
    }

    private loadIconsForBox(boxIndex: number): void {
        console.log(`[PCMenu] Loading icons for Box ${boxIndex}`);
        const box = this.game.storageSystem.boxes[boxIndex];
        if (box) {
             box.forEach(p => this.loadIcon(p.speciesId));
        }
        // Preload Party too?
        this.game.party.forEach(p => this.loadIcon(p.speciesId));
    }

    private loadIcon(speciesId: string): void {
        if (this.iconCache.has(speciesId)) return;
        
        // Pad ID
        const id = parseInt(speciesId).toString().padStart(3, '0');
        const path = `data/pokemon/images/${id}/front.png`;
        
        // console.log(`[PCMenu] Requesting icon: ${path}`);
        (window as any).fs.readImage(path).then((res: any) => {
            if (res.success) {
                // console.log(`[PCMenu] Loaded icon for ${speciesId}`);
                const img = new Image();
                // img.onload = () => console.log(`[PCMenu] Image object loaded for ${speciesId}`);
                img.src = `data:image/png;base64,${res.data}`; 
                this.iconCache.set(speciesId, img);
            } else {
                console.error(`[PCMenu] Failed to load icon ${path}:`, res.error);
            }
        });
    }

    public update(dt: number): void {
        const input = this.game.input;
        // console.log(`[PCMenu] Update. Mode: ${this.mode}`); 

        if (this.mode === 'main') {
            this.updateMainMenu(input);
        } else if (this.mode === 'withdraw') {
             this.updateWithdraw(input);
        } else if (this.mode === 'deposit') {
            this.updateDeposit(input);
        }
    }

    private updateMainMenu(input: any): void {
        if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) this.selection = (this.selection + 1) % 3; 
        if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) this.selection = (this.selection + 2) % 3; // wrap back

        if (input.isJustPressed('KeyZ') || input.isJustPressed('Space') || input.isJustPressed('Enter')) {
            if (this.selection === 0) {
                this.mode = 'withdraw';
                this.selection = 0; // Reset cursor to 0,0
                this.isInHeader = false;
                this.loadIconsForBox(this.currentBoxIndex);
            } else if (this.selection === 1) {
                this.mode = 'deposit';
                this.selection = 0;
                this.isInHeader = false;
                this.loadIconsForBox(this.currentBoxIndex); // Preload target box icons too
            } else {
                this.game.menuSystem.pop();
            }
        }
        if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
            this.game.menuSystem.pop();
        }
    }

    private updateWithdraw(input: any): void {
        // Navigation (WASD)
        if (this.isInHeader) {
            // Header Stats: Box Switch
            if (input.isJustPressed('KeyD')) {
                this.currentBoxIndex = (this.currentBoxIndex + 1) % this.game.storageSystem.boxes.length;
                this.loadIconsForBox(this.currentBoxIndex);
                console.log(`[PCMenu] Switched to Box ${this.currentBoxIndex}`);
            }
            if (input.isJustPressed('KeyA')) {
                this.currentBoxIndex = (this.currentBoxIndex - 1 + this.game.storageSystem.boxes.length) % this.game.storageSystem.boxes.length;
                this.loadIconsForBox(this.currentBoxIndex);
                console.log(`[PCMenu] Switched to Box ${this.currentBoxIndex}`);
            }
            // Move Down to Grid
            if (input.isJustPressed('KeyS') || input.isJustPressed('ArrowDown')) {
                this.isInHeader = false;
                this.selection = 0; // Or keep column? 0 is safer default
            }
        } else {
            // Grid Navigation
            if (input.isJustPressed('KeyD') || input.isJustPressed('ArrowRight')) {
                 if ((this.selection + 1) % this.GRID_COLS !== 0) this.selection++;
            }
            if (input.isJustPressed('KeyA') || input.isJustPressed('ArrowLeft')) {
                 if (this.selection % this.GRID_COLS !== 0) this.selection--;
            }
            if (input.isJustPressed('KeyS') || input.isJustPressed('ArrowDown')) {
                 if (this.selection + this.GRID_COLS < this.BOX_CAPACITY) this.selection += this.GRID_COLS;
            }
            // Move Up (Check for Header)
            if (input.isJustPressed('KeyW') || input.isJustPressed('ArrowUp')) {
                 if (this.selection - this.GRID_COLS >= 0) {
                     this.selection -= this.GRID_COLS;
                 } else {
                     // Move to Header
                     this.isInHeader = true;
                 }
            }
        }

        // Action: Withdraw
        if ((input.isJustPressed('KeyZ') || input.isJustPressed('Space')) && !this.isInHeader) {
            const box = this.game.storageSystem.boxes[this.currentBoxIndex];
            if (this.selection < box.length) {
                const mon = box[this.selection];
                if (this.game.party.length < 6) {
                    box.splice(this.selection, 1);
                    this.game.party.push(mon);
                    console.log(`[PC] Withdrew ${mon.nickname}`);
                } else {
                    console.log(`[PC] Party Full!`);
                }
            }
        }

        if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
            console.log('[PCMenu] Exiting Withdraw Mode');
            this.mode = 'main';
        }
    }

    private updateDeposit(input: any): void {
        const party = this.game.party;
        
        // Navigation (List vertical)
        if (this.isInHeader) {
            // Box Switching
            if (input.isJustPressed('KeyD')) {
                this.currentBoxIndex = (this.currentBoxIndex + 1) % this.game.storageSystem.boxes.length;
                this.loadIconsForBox(this.currentBoxIndex);
            }
            if (input.isJustPressed('KeyA')) {
                this.currentBoxIndex = (this.currentBoxIndex - 1 + this.game.storageSystem.boxes.length) % this.game.storageSystem.boxes.length;
                this.loadIconsForBox(this.currentBoxIndex);
            }
             // Move Down to List
            if (input.isJustPressed('KeyS') || input.isJustPressed('ArrowDown')) {
                this.isInHeader = false;
                this.selection = 0;
            }
        } else {
            // List Nav
            if (input.isJustPressed('KeyS') || input.isJustPressed('ArrowDown')) {
                 if (this.selection < party.length - 1) this.selection++;
            }
            if (input.isJustPressed('KeyW') || input.isJustPressed('ArrowUp')) {
                if (this.selection > 0) {
                    this.selection--;
                } else {
                    this.isInHeader = true;
                }
            }
        }

        // Action: Deposit
        if ((input.isJustPressed('KeyZ') || input.isJustPressed('Space')) && !this.isInHeader) {
            if (party.length <= 1) {
                console.log("[PC] Cannot deposit last Pokemon!");
                return;
            }
            
            const mon = party[this.selection];
            const added = this.game.storageSystem.addPokemon(mon, this.currentBoxIndex);
            
            if (added) {
                party.splice(this.selection, 1);
                console.log(`[PC] Deposited ${mon.nickname} to Box ${this.currentBoxIndex + 1}`);
                if (this.selection >= party.length) this.selection = party.length - 1;
            } else {
                console.log(`[PC] Box ${this.currentBoxIndex + 1} is Full!`);
            }
        }

        if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
            console.log('[PCMenu] Exiting Deposit Mode');
            this.mode = 'main';
        }
    }

    public render(ctx: CanvasRenderingContext2D): void {
        const width = this.game.display.width;
        const height = this.game.display.height;
        
        ctx.fillStyle = '#222';
        ctx.fillRect(0, 0, width, height);

        if (this.mode === 'main') {
            this.renderMainMenu(ctx, width, height);
        } else if (this.mode === 'withdraw') {
            this.renderGridMode(ctx, width, height, true);
        } else if (this.mode === 'deposit') {
            this.renderGridMode(ctx, width, height, false); // Reusing layout
        }
        
        // Render Footer Controls
        ctx.fillStyle = '#95a5a6';
        ctx.font = '14px monospace';
        ctx.textAlign = 'left';
        
        if (this.mode === 'main') {
            ctx.fillText('[Z] Select  [X] Exit', 20, height - 20);
        } else {
            ctx.fillText('[WASD] Navigate  [Z] Action  [X] Back', 20, height - 20);
        }
    }

    private renderMainMenu(ctx: CanvasRenderingContext2D, w: number, h: number): void {
        ctx.fillStyle = '#fff';
        ctx.font = '30px monospace';
        ctx.textAlign = 'center';
        ctx.fillText('PC SYSTEM', w/2, 50);

        ctx.font = '20px monospace';
        const opts = ['Withdraw Pokemon', 'Deposit Pokemon', 'Log Off'];
        
        opts.forEach((opt, i) => {
            const y = h/2 - 30 + (i * 40);
            if (i === this.selection) {
                ctx.fillStyle = '#f1c40f';
                ctx.fillText(`> ${opt} <`, w/2, y);
            } else {
                ctx.fillStyle = '#7f8c8d';
                ctx.fillText(opt, w/2, y);
            }
        });
    }

    private renderGridMode(ctx: CanvasRenderingContext2D, w: number, h: number, isWithdraw: boolean): void {
        const marginX = 20;
        const marginY = 60;
        
        // 1. Header
        ctx.textAlign = 'center';
        const title = isWithdraw ? 'WITHDRAW' : 'DEPOSIT (Select Party Member)';
        
        ctx.fillStyle = '#ecf0f1';
        ctx.font = '20px monospace';
        ctx.fillText(title, w/2, 30);
        
        // Box Selector
        const boxName = `Box ${this.currentBoxIndex + 1}`;
        if (this.isInHeader) {
            ctx.fillStyle = '#f1c40f';
            ctx.fillText(`< ${boxName} >`, w/2, 55);
            ctx.strokeStyle = '#f1c40f';
            ctx.lineWidth = 2;
            ctx.strokeRect(w/2 - 80, 38, 160, 24);
        } else {
            ctx.fillStyle = '#bdc3c7';
            ctx.fillText(`< ${boxName} >`, w/2, 55);
        }

        // 2. Left Panel: GRID or PARTY
        if (isWithdraw) {
            this.renderGrid(ctx, marginX, marginY);
        } else {
            this.renderPartyList(ctx, marginX, marginY);
        }

        // 3. Right Panel: DETAILS
        const panelX = w - 240; // Fixed width sidebar
        this.renderDetailsPanel(ctx, panelX, marginY, isWithdraw);
    }

    private renderGrid(ctx: CanvasRenderingContext2D, x: number, y: number): void {
        const box = this.game.storageSystem.boxes[this.currentBoxIndex];
        const cellSize = 50; // 40px icon + padding
        const iconSize = 40;
        
        for (let r = 0; r < this.GRID_ROWS; r++) {
            for (let c = 0; c < this.GRID_COLS; c++) {
                const cx = x + c * cellSize;
                const cy = y + r * cellSize;
                
                // Reset Default Style for every cell
                ctx.strokeStyle = '#34495e';
                ctx.lineWidth = 1;

                ctx.strokeRect(cx, cy, cellSize, cellSize);
                
                const idx = r * this.GRID_COLS + c;
                
                // Draw Cursor
                if (idx === this.selection) {
                    ctx.fillStyle = 'rgba(241, 196, 15, 0.3)';
                    ctx.fillRect(cx + 2, cy + 2, cellSize - 4, cellSize - 4);
                    
                    // Highlight Style
                    ctx.strokeStyle = '#f1c40f';
                    ctx.lineWidth = 2;
                    ctx.strokeRect(cx, cy, cellSize, cellSize);
                }

                // Draw Icon
                if (idx < box.length) {
                    const mon = box[idx];
                    const icon = this.iconCache.get(mon.speciesId);
                    if (icon) {
                        ctx.drawImage(icon, cx + (cellSize - iconSize)/2, cy + (cellSize - iconSize)/2, iconSize, iconSize);
                    } else {
                        // Placeholder circle
                        ctx.fillStyle = '#2ecc71';
                        ctx.beginPath();
                        ctx.arc(cx + cellSize/2, cy + cellSize/2, 10, 0, Math.PI*2);
                        ctx.fill();
                    }
                }
            }
        }
        ctx.lineWidth = 1; // Reset
    }

    private renderPartyList(ctx: CanvasRenderingContext2D, x: number, y: number): void {
        const party = this.game.party;
        
        party.forEach((mon, i) => {
            const py = y + i * 50;
            
            // Cursor
            if (i === this.selection) {
                ctx.fillStyle = 'rgba(241, 196, 15, 0.3)';
                ctx.fillRect(x, py, 200, 45);
                ctx.strokeStyle = '#f1c40f';
                ctx.strokeRect(x, py, 200, 45);
            }

            // Icon
            const icon = this.iconCache.get(mon.speciesId);
            if (icon) {
                ctx.drawImage(icon, x + 5, py + 2, 40, 40);
            }
            
            // Text
            ctx.fillStyle = '#fff';
            ctx.textAlign = 'left';
            ctx.font = '16px monospace';
            ctx.fillText(mon.nickname || '???', x + 50, py + 20);
            ctx.font = '12px monospace';
            ctx.fillText(`Lv.${mon.level}`, x + 50, py + 38);
        });
    }

    private renderDetailsPanel(ctx: CanvasRenderingContext2D, x: number, y: number, isWithdraw: boolean): void {
        let mon: PokemonInstance | undefined;

        if (isWithdraw) {
            const box = this.game.storageSystem.boxes[this.currentBoxIndex];
            if (this.selection < box.length) mon = box[this.selection];
        } else {
             mon = this.game.party[this.selection];
        }

        // Panel BG
        ctx.fillStyle = '#2c3e50';
        ctx.fillRect(x, y, 220, 340);
        ctx.strokeStyle = '#95a5a6';
        ctx.strokeRect(x, y, 220, 340);

        if (!mon) {
            ctx.fillStyle = '#7f8c8d';
            ctx.textAlign = 'center';
            ctx.fillText('Empty Slot', x + 110, y + 150);
            return;
        }

        // Content
        const cx = x + 110;
        
        // 1. Large Type/Icon Display
        // Ideally we'd show the Front Sprite, but loading it might be async/slow or flicker.
        // Let's use the Icon scaled up for now, or just text.
        const icon = this.iconCache.get(mon.speciesId);
        if (icon) {
             ctx.drawImage(icon, cx - 32, y + 10, 64, 64);
        }

        // 2. Info
        ctx.fillStyle = '#fff';
        ctx.textAlign = 'center';
        ctx.font = 'bold 18px monospace';
        ctx.fillText(mon.nickname || '???', cx, y + 90);
        
        ctx.font = '14px monospace';
        ctx.fillText(`Lv.${mon.level} ${mon.gender}`, cx, y + 110);
        
        // Stats
        ctx.textAlign = 'left';
        ctx.font = '14px monospace';
        const startY = y + 135;
        const col1 = x + 15;
        const col2 = x + 115;
        const lineHeight = 18;

        ctx.fillText(`HP: ${mon.currentHp}/${mon.currentStats.hp}`, col1, startY);
        
        ctx.fillText(`Atk: ${mon.currentStats.attack}`, col1, startY + lineHeight);
        ctx.fillText(`Def: ${mon.currentStats.defense}`, col2, startY + lineHeight);
        
        ctx.fillText(`SpA: ${mon.currentStats.spAttack}`, col1, startY + lineHeight * 2);
        ctx.fillText(`SpD: ${mon.currentStats.spDefense}`, col2, startY + lineHeight * 2);
        
        ctx.fillText(`Spe: ${mon.currentStats.speed}`, col1, startY + lineHeight * 3);

        // Nature / Ability
        ctx.fillStyle = '#ecf0f1';
        // Ability
        ctx.fillText(`Abl: ${mon.ability}`, col1, startY + lineHeight * 4);
        // Nature (Moved below Ability)
        ctx.fillText(`Nat: ${mon.nature}`, col1, startY + lineHeight * 5);

        // Moves
        const movesY = startY + lineHeight * 6 + 10;
        ctx.fillStyle = '#f39c12';
        ctx.fillText('Moves:', col1, movesY);
        
        ctx.fillStyle = '#fff';
        ctx.font = '12px monospace';
        mon.moves.forEach((m, i) => {
            const moveData = this.game.dataManager.getMove(m.moveId);
            const name = moveData ? moveData.name : m.moveId;
            ctx.fillText(`- ${name}`, x + 20, movesY + 20 + (i * 16));
        });
    }
}
