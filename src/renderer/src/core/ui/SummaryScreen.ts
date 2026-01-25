import { Game } from '../Game';
import { PokemonInstance } from '../data/DataTypes';

export class SummaryScreen {
    private game: Game;
    private pokemon: PokemonInstance;
    private partyIndex: number; // Track position in party
    private currentTab: number = 0; // 0=Info, 1=Skills, 2=Moves, 3=Placeholder
    private moveSelection: number = 0;
    private moveDetailMode: boolean = false; // For Moves tab - toggle with Z/X
    private pokemonSprite: HTMLImageElement | null = null;
    
    public onClose: (() => void) | null = null;

    constructor(game: Game, pokemon: PokemonInstance) {
        this.game = game;
        this.pokemon = pokemon;
        // Find this Pokemon's index in the party
        this.partyIndex = this.game.party.findIndex(p => p.uuid === pokemon.uuid);
        if (this.partyIndex === -1) this.partyIndex = 0; // Fallback
        this.loadSprite();
    }

    private async loadSprite(): Promise<void> {
        const species = this.game.dataManager.getPokemonSpecies(this.pokemon.speciesId);
        if (!species) return;

        try {
            const spritePath = species.assets.front;
            const response = await (window as any).fs.readImage(spritePath);
            if (response.success) {
                const img = new Image();
                img.src = `data:image/png;base64,${response.data}`;
                this.pokemonSprite = img;
            }
        } catch (e) {
            console.error('[SummaryScreen] Failed to load sprite:', e);
        }
    }

    public update(dt: number): void {
        const input = this.game.input;

        // Party navigation (W/S) - unless in move detail mode
        if (!this.moveDetailMode) {
            if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
                this.partyIndex = (this.partyIndex - 1 + this.game.party.length) % this.game.party.length;
                this.switchPokemon();
            }
            if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
                this.partyIndex = (this.partyIndex + 1) % this.game.party.length;
                this.switchPokemon();
            }
        }

        // Tab navigation (A/D)
        if (input.isJustPressed('ArrowRight') || input.isJustPressed('KeyD')) {
            this.currentTab = (this.currentTab + 1) % 4;
            this.moveSelection = 0;
            this.moveDetailMode = false;
        }
        if (input.isJustPressed('ArrowLeft') || input.isJustPressed('KeyA')) {
            this.currentTab = (this.currentTab + 3) % 4;
            this.moveSelection = 0;
            this.moveDetailMode = false;
        }

        // Moves tab - toggle move detail mode with Z
        if (this.currentTab === 2) {
            if (input.isJustPressed('KeyZ') || input.isJustPressed('Space') || input.isJustPressed('Enter')) {
                this.moveDetailMode = true;
            }
            
            // Move selection (only in detail mode)
            if (this.moveDetailMode) {
                const maxMoves = this.pokemon.moves.length;
                if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
                    this.moveSelection = Math.min(this.moveSelection + 1, maxMoves); // +1 for Cancel
                }
                if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
                    this.moveSelection = Math.max(this.moveSelection - 1, 0);
                }
                
                // Exit move detail mode with X
                if (input.isJustPressed('KeyX') || input.isJustPressed('Escape')) {
                    this.moveDetailMode = false;
                    this.moveSelection = 0;
                    return; // Don't close summary
                }
            }
        }

        // Close summary (X/Escape) - but not if in move detail mode
        if (!this.moveDetailMode && (input.isJustPressed('Escape') || input.isJustPressed('KeyX'))) {
            if (this.onClose) this.onClose();
        }
    }

    private switchPokemon(): void {
        this.pokemon = this.game.party[this.partyIndex];
        this.moveSelection = 0;
        this.moveDetailMode = false;
        this.loadSprite();
    }

    public render(ctx: CanvasRenderingContext2D): void {
        const width = 960;
        const height = 640;

        // Background
        ctx.fillStyle = '#e8e8d0';
        ctx.fillRect(0, 0, width, height);

        // Header
        this.renderHeader(ctx, width);

        // Tab indicators
        this.renderTabIndicators(ctx, width);

        // Pokemon sprite (right side)
        this.renderPokemonSprite(ctx, width, height);

        // Current tab content
        switch (this.currentTab) {
            case 0:
                this.renderInfoPage(ctx, width, height);
                break;
            case 1:
                this.renderSkillsPage(ctx, width, height);
                break;
            case 2:
                this.renderMovesPage(ctx, width, height);
                break;
            case 3:
                this.renderPlaceholderPage(ctx, width, height);
                break;
        }

        // Cancel button
        this.renderCancelButton(ctx, width, height);
    }

    private renderHeader(ctx: CanvasRenderingContext2D, width: number): void {
        const titles = ['Pokémon Info', 'Pokémon Skills', 'Battle Moves', 'Ribbons'];
        
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, width, 60);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 3;
        ctx.strokeRect(0, 0, width, 60);

        ctx.fillStyle = '#000';
        ctx.font = 'bold 28px monospace';
        ctx.fillText(titles[this.currentTab], 20, 40);
    }

    private renderTabIndicators(ctx: CanvasRenderingContext2D, width: number): void {
        const tabWidth = 40;
        const tabHeight = 30;
        const startX = width / 2 - (4 * tabWidth) / 2;
        const y = 70;

        for (let i = 0; i < 4; i++) {
            const x = startX + i * (tabWidth + 10);
            
            if (i === this.currentTab) {
                ctx.fillStyle = '#000';
            } else {
                ctx.fillStyle = '#ccc';
            }
            
            ctx.fillRect(x, y, tabWidth, tabHeight);
            ctx.strokeStyle = '#000';
            ctx.lineWidth = 2;
            ctx.strokeRect(x, y, tabWidth, tabHeight);
        }
    }

    private renderPokemonSprite(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        // Pokemon info box (right side)
        const boxX = width - 280;
        const boxY = 120;
        const boxWidth = 260;
        const boxHeight = 200;

        ctx.fillStyle = '#fff';
        ctx.fillRect(boxX, boxY, boxWidth, boxHeight);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(boxX, boxY, boxWidth, boxHeight);

        // Pokemon name and level
        const species = this.game.dataManager.getPokemonSpecies(this.pokemon.speciesId);
        ctx.fillStyle = '#000';
        ctx.font = 'bold 20px monospace';
        ctx.fillText(this.pokemon.nickname || species?.name || 'Unknown', boxX + 10, boxY + 25);
        ctx.font = '16px monospace';
        ctx.fillText(`Lv${this.pokemon.level}`, boxX + 10, boxY + 45);

        // Gender symbol
        if (this.pokemon.gender === 'Male') {
            ctx.fillStyle = '#0080ff';
            ctx.fillText('♂', boxX + boxWidth - 30, boxY + 25);
        } else if (this.pokemon.gender === 'Female') {
            ctx.fillStyle = '#ff4080';
            ctx.fillText('♀', boxX + boxWidth - 30, boxY + 25);
        }

        // Pokeball icon (placeholder)
        ctx.fillStyle = '#ff0000';
        ctx.beginPath();
        ctx.arc(boxX + boxWidth - 30, boxY + 45, 10, 0, Math.PI * 2);
        ctx.fill();

        // HP Bar (only on Skills page)
        if (this.currentTab === 1) {
            const hpBarY = boxY + 70;
            const hpBarWidth = boxWidth - 20;
            const hpPercent = this.pokemon.currentHp / this.pokemon.currentStats.hp;
            
            ctx.fillStyle = '#000';
            ctx.font = '14px monospace';
            ctx.fillText('HP', boxX + 10, hpBarY);
            
            // HP Bar background
            ctx.fillStyle = '#ddd';
            ctx.fillRect(boxX + 10, hpBarY + 5, hpBarWidth, 15);
            
            // HP Bar fill
            const fillColor = hpPercent > 0.5 ? '#00ff00' : hpPercent > 0.2 ? '#ffff00' : '#ff0000';
            ctx.fillStyle = fillColor;
            ctx.fillRect(boxX + 10, hpBarY + 5, hpBarWidth * hpPercent, 15);
            
            // HP Bar border
            ctx.strokeStyle = '#000';
            ctx.lineWidth = 1;
            ctx.strokeRect(boxX + 10, hpBarY + 5, hpBarWidth, 15);
            
            // HP Text
            ctx.fillStyle = '#000';
            ctx.font = '12px monospace';
            ctx.fillText(`${this.pokemon.currentHp}/${this.pokemon.currentStats.hp}`, boxX + 10, hpBarY + 35);
        }

        // Sprite
        if (this.pokemonSprite) {
            const spriteSize = 120;
            const spriteX = boxX + (boxWidth - spriteSize) / 2;
            const spriteY = boxY + 60;
            ctx.drawImage(this.pokemonSprite, spriteX, spriteY, spriteSize, spriteSize);
        }
    }

    private renderCancelButton(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        const buttonX = width - 150;
        const buttonY = height - 60;

        ctx.fillStyle = '#ff0000';
        ctx.fillRect(buttonX, buttonY, 130, 40);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(buttonX, buttonY, 130, 40);

        ctx.fillStyle = '#fff';
        ctx.font = 'bold 20px monospace';
        ctx.fillText('Cancel', buttonX + 20, buttonY + 27);
    }

    private renderInfoPage(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        const species = this.game.dataManager.getPokemonSpecies(this.pokemon.speciesId);
        if (!species) return;

        const startX = 20;
        const startY = 140;
        const lineHeight = 45;

        ctx.font = '18px monospace';

        // Dex Number
        this.renderInfoRow(ctx, startX, startY, 'No.', species.id.padStart(3, '0'));

        // Name
        this.renderInfoRow(ctx, startX, startY + lineHeight, 'NAME', this.pokemon.nickname || species.name);

        // Type
        const typeText = species.types.join(' / ');
        this.renderInfoRow(ctx, startX, startY + lineHeight * 2, 'TYPE', typeText);

        // OT
        this.renderInfoRow(ctx, startX, startY + lineHeight * 3, 'OT', this.pokemon.originalTrainer || 'Player');

        // ID No (generate stable ID from Pokemon UUID)
        const idNo = this.getStableTrainerID();
        this.renderInfoRow(ctx, startX, startY + lineHeight * 4, 'ID No.', idNo);

        // Held Item
        this.renderInfoRow(ctx, startX, startY + lineHeight * 5, 'ITEM', this.pokemon.heldItem || 'None');

        // Nature and met info
        ctx.fillStyle = '#0000ff';
        ctx.font = 'bold 18px monospace';
        ctx.fillText(`${this.pokemon.nature} nature,`, startX, startY + lineHeight * 7);
        
        ctx.fillStyle = '#000';
        ctx.font = '18px monospace';
        ctx.fillText(`met at Lv.${this.pokemon.level},`, startX, startY + lineHeight * 7.5);
        
        ctx.fillStyle = '#0000ff';
        ctx.font = 'bold 18px monospace';
        ctx.fillText('Unknown Location.', startX, startY + lineHeight * 8);
    }

    private renderInfoRow(ctx: CanvasRenderingContext2D, x: number, y: number, label: string, value: string): void {
        // Label box
        ctx.fillStyle = '#333';
        ctx.fillRect(x, y - 20, 120, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px monospace';
        ctx.fillText(label, x + 5, y);

        // Value box
        ctx.fillStyle = '#fff';
        ctx.fillRect(x + 130, y - 20, 250, 30);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(x + 130, y - 20, 250, 30);
        
        ctx.fillStyle = '#000';
        ctx.font = '18px monospace';
        ctx.fillText(value, x + 140, y);
    }

    private renderSkillsPage(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        const startX = 20;
        const startY = 140;
        const lineHeight = 40;

        ctx.font = '18px monospace';

        // HP
        this.renderStatRow(ctx, startX, startY, 'HP', this.pokemon.currentStats.hp, this.pokemon.ivs.hp, this.pokemon.currentHp, this.pokemon.currentStats.hp);

        // Other stats
        this.renderStatRow(ctx, startX, startY + lineHeight, 'ATTACK', this.pokemon.currentStats.attack, this.pokemon.ivs.attack);
        this.renderStatRow(ctx, startX, startY + lineHeight * 2, 'DEFENSE', this.pokemon.currentStats.defense, this.pokemon.ivs.defense);
        this.renderStatRow(ctx, startX, startY + lineHeight * 3, 'SP.ATK', this.pokemon.currentStats.spAttack, this.pokemon.ivs.spAttack);
        this.renderStatRow(ctx, startX, startY + lineHeight * 4, 'SP.DEF', this.pokemon.currentStats.spDefense, this.pokemon.ivs.spDefense);
        this.renderStatRow(ctx, startX, startY + lineHeight * 5, 'SPEED', this.pokemon.currentStats.speed, this.pokemon.ivs.speed);

        // EXP bar
        const expY = startY + lineHeight * 6.5;
        ctx.fillStyle = '#333';
        ctx.fillRect(startX, expY - 20, 80, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px monospace';
        ctx.fillText('EXP.', startX + 5, expY);

        const currentExp = this.pokemon.experience || 0;
        const nextLevelExp = Math.pow(this.pokemon.level + 1, 3);
        ctx.fillStyle = '#000';
        ctx.font = '16px monospace';
        ctx.fillText(`${currentExp} / ${nextLevelExp}`, startX + 90, expY);

        // Ability
        const abilityY = expY + 60;
        ctx.fillStyle = '#fff';
        ctx.fillRect(startX, abilityY, 600, 80);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(startX, abilityY, 600, 80);

        ctx.fillStyle = '#000';
        ctx.font = 'bold 18px monospace';
        ctx.fillText(this.pokemon.ability || 'Unknown', startX + 10, abilityY + 25);
        
        ctx.font = '14px monospace';
        ctx.fillText('Ability description would go here.', startX + 10, abilityY + 50);
    }

    private renderStatRow(ctx: CanvasRenderingContext2D, x: number, y: number, label: string, value: number, iv: number, currentHp?: number, maxHp?: number): void {
        // IV Grade
        const grade = this.getIVGrade(iv);
        const gradeColor = this.getGradeColor(grade);
        
        ctx.fillStyle = gradeColor;
        ctx.fillRect(x, y - 20, 30, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 18px monospace';
        ctx.fillText(grade, x + 8, y);

        // Label
        ctx.fillStyle = '#333';
        ctx.fillRect(x + 40, y - 20, 120, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px monospace';
        ctx.fillText(label, x + 45, y);

        // Stat indicator (nature modifier placeholder)
        ctx.fillStyle = '#90EE90';
        ctx.fillRect(x + 170, y - 20, 30, 30);
        ctx.fillStyle = '#000';
        ctx.font = 'bold 14px monospace';
        ctx.fillText('●', x + 178, y);

        // Value
        ctx.fillStyle = '#000';
        ctx.font = '18px monospace';
        ctx.fillText(value.toString(), x + 210, y);

        // Note: HP bar is now in renderPokemonSprite for Skills page
    }

    private getStableTrainerID(): string {
        // Generate a stable 5-digit ID from the Pokemon's UUID
        let hash = 0;
        const uuid = this.pokemon.uuid || 'default';
        for (let i = 0; i < uuid.length; i++) {
            hash = ((hash << 5) - hash) + uuid.charCodeAt(i);
            hash = hash & hash; // Convert to 32bit integer
        }
        const id = Math.abs(hash) % 100000;
        return id.toString().padStart(5, '0');
    }

    private getIVGrade(iv: number): string {
        if (iv === 31) return 'S';
        if (iv >= 29) return 'A';
        if (iv >= 26) return 'B';
        if (iv >= 21) return 'C';
        if (iv >= 16) return 'D';
        return 'F';
    }

    private getGradeColor(grade: string): string {
        switch (grade) {
            case 'S': return '#FFD700'; // Gold
            case 'A': return '#00ff00'; // Green
            case 'B': return '#0080ff'; // Blue
            case 'C': return '#ffff00'; // Yellow
            case 'D': return '#ff8000'; // Orange
            case 'F': return '#ff0000'; // Red
            default: return '#888';
        }
    }

    private renderMovesPage(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        const startX = 20;
        const startY = 140;
        const lineHeight = 70;

        // Move list
        for (let i = 0; i < 4; i++) {
            const move = this.pokemon.moves[i];
            const y = startY + i * lineHeight;
            const isSelected = this.moveSelection === i;

            if (move) {
                const moveData = this.game.dataManager.getMove(move.moveId);
                this.renderMoveSlot(ctx, startX, y, moveData, move, isSelected);
            } else {
                this.renderEmptyMoveSlot(ctx, startX, y, isSelected);
            }
        }

        // Cancel option
        const cancelY = startY + 4 * lineHeight;
        const isSelected = this.moveSelection === this.pokemon.moves.length;
        
        ctx.fillStyle = isSelected ? '#ffff00' : '#fff';
        ctx.fillRect(startX, cancelY, 300, 40);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(startX, cancelY, 300, 40);
        
        ctx.fillStyle = '#000';
        ctx.font = 'bold 18px monospace';
        ctx.fillText('Cancel', startX + 100, cancelY + 27);

        // Move details (if move selected)
        if (this.moveSelection < this.pokemon.moves.length) {
            const selectedMove = this.pokemon.moves[this.moveSelection];
            if (selectedMove) {
                const moveData = this.game.dataManager.getMove(selectedMove.moveId);
                this.renderMoveDetails(ctx, width, height, moveData);
            }
        }
    }

    private renderMoveSlot(ctx: CanvasRenderingContext2D, x: number, y: number, moveData: any, moveInstance: any, isSelected: boolean): void {
        ctx.fillStyle = isSelected ? '#00ffff' : '#fff';
        ctx.fillRect(x, y, 600, 60);
        ctx.strokeStyle = isSelected ? '#ff0000' : '#000';
        ctx.lineWidth = isSelected ? 3 : 2;
        ctx.strokeRect(x, y, 600, 60);

        if (moveData) {
            // Type badge
            const typeColor = this.getTypeColor(moveData.type);
            ctx.fillStyle = typeColor;
            ctx.fillRect(x + 10, y + 10, 80, 25);
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 14px monospace';
            ctx.fillText(moveData.type.toUpperCase(), x + 15, y + 28);

            // Move name
            ctx.fillStyle = '#000';
            ctx.font = 'bold 18px monospace';
            ctx.fillText(moveData.name, x + 100, y + 28);

            // PP
            ctx.font = '16px monospace';
            ctx.fillText(`PP  ${moveInstance.pp}/${moveInstance.maxPp}`, x + 400, y + 28);

            // PP bars (visual)
            const ppBars = 4;
            for (let i = 0; i < ppBars; i++) {
                const barX = x + 10 + i * 15;
                const barY = y + 40;
                ctx.fillStyle = i < Math.ceil((moveInstance.pp / moveInstance.maxPp) * ppBars) ? '#00ffff' : '#888';
                ctx.fillRect(barX, barY, 10, 10);
            }
        }
    }

    private renderEmptyMoveSlot(ctx: CanvasRenderingContext2D, x: number, y: number, isSelected: boolean): void {
        ctx.fillStyle = isSelected ? '#ffff00' : '#eee';
        ctx.fillRect(x, y, 600, 60);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(x, y, 600, 60);

        ctx.fillStyle = '#888';
        ctx.font = '16px monospace';
        ctx.fillText('---', x + 10, y + 35);
    }

    private renderMoveDetails(ctx: CanvasRenderingContext2D, width: number, height: number, moveData: any): void {
        if (!moveData) return;

        const boxX = 20;
        const boxY = height - 180;
        const boxWidth = width - 300;
        const boxHeight = 160;

        ctx.fillStyle = '#fff';
        ctx.fillRect(boxX, boxY, boxWidth, boxHeight);
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.strokeRect(boxX, boxY, boxWidth, boxHeight);

        // Power and Accuracy
        ctx.fillStyle = '#333';
        ctx.fillRect(boxX + 10, boxY + 10, 100, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px monospace';
        ctx.fillText('POWER', boxX + 15, boxY + 30);

        ctx.fillStyle = '#000';
        ctx.font = '18px monospace';
        ctx.fillText(moveData.power > 0 ? moveData.power.toString() : '---', boxX + 120, boxY + 30);

        ctx.fillStyle = '#333';
        ctx.fillRect(boxX + 200, boxY + 10, 120, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px monospace';
        ctx.fillText('ACCURACY', boxX + 205, boxY + 30);

        ctx.fillStyle = '#000';
        ctx.font = '18px monospace';
        ctx.fillText(moveData.accuracy > 0 ? moveData.accuracy.toString() : '---', boxX + 330, boxY + 30);

        // Category icon (placeholder)
        const categoryX = boxX + 400;
        ctx.fillStyle = moveData.category === 'Physical' ? '#ff8000' : '#8080ff';
        ctx.fillRect(categoryX, boxY + 10, 80, 30);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 14px monospace';
        ctx.fillText(moveData.category === 'Physical' ? 'PHYS' : 'SPEC', categoryX + 10, boxY + 30);

        // Description
        ctx.fillStyle = '#000';
        ctx.font = '14px monospace';
        const desc = moveData.description || 'No description available.';
        this.wrapText(ctx, desc, boxX + 10, boxY + 60, boxWidth - 20, 18);
    }

    private wrapText(ctx: CanvasRenderingContext2D, text: string, x: number, y: number, maxWidth: number, lineHeight: number): void {
        const words = text.split(' ');
        let line = '';
        let currentY = y;

        for (let i = 0; i < words.length; i++) {
            const testLine = line + words[i] + ' ';
            const metrics = ctx.measureText(testLine);
            
            if (metrics.width > maxWidth && i > 0) {
                ctx.fillText(line, x, currentY);
                line = words[i] + ' ';
                currentY += lineHeight;
            } else {
                line = testLine;
            }
        }
        ctx.fillText(line, x, currentY);
    }

    private renderPlaceholderPage(ctx: CanvasRenderingContext2D, width: number, height: number): void {
        ctx.fillStyle = '#000';
        ctx.font = 'bold 24px monospace';
        ctx.fillText('Coming Soon...', width / 2 - 100, height / 2);
    }

    private getTypeColor(type: string): string {
        const colors: { [key: string]: string } = {
            'Normal': '#A8A878',
            'Fire': '#F08030',
            'Water': '#6890F0',
            'Grass': '#78C850',
            'Electric': '#F8D030',
            'Ice': '#98D8D8',
            'Fighting': '#C03028',
            'Poison': '#A040A0',
            'Ground': '#E0C068',
            'Flying': '#A890F0',
            'Psychic': '#F85888',
            'Bug': '#A8B820',
            'Rock': '#B8A038',
            'Ghost': '#705898',
            'Dragon': '#7038F8',
            'Dark': '#705848',
            'Steel': '#B8B8D0',
            'Fairy': '#EE99AC'
        };
        return colors[type] || '#888';
    }
}
