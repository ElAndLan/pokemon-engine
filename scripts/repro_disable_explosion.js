
// Mock structures based on MoveEngine.ts usage
const battleLog = [];

const mockPokemon = (name, speed) => ({
    uuid: Math.random().toString(),
    nickname: name,
    speciesId: name.toLowerCase(),
    types: ['Normal'],
    level: 50,
    currentHp: 100,
    currentStats: { hp: 100, attack: 50, defense: 50, spAttack: 50, spDefense: 50, speed },
    statStages: {},
    status: 'None',
    volatile: {},
    moves: [],
    heldItem: undefined,
    lastMoveUsed: undefined,
    disabledMoveId: undefined
});

const mockMoves = {
    'bite': {
        id: 'bite', name: 'Bite', type: 'Dark', category: 'Physical', power: 60, accuracy: 100, 
        target: 'SelectedEnemy', effects: [{ type: 'Damage' }]
    },
    'disable': {
        id: 'disable', name: 'Disable', type: 'Normal', category: 'Status', power: 0, accuracy: 100,
        target: 'SelectedEnemy', 
        effects: [{ type: 'Unique', volatileStatus: 'Disable', chance: 100 }]
    },
    'explosion': {
        id: 'explosion', name: 'Explosion', type: 'Normal', category: 'Physical', power: 250, accuracy: 100,
        target: 'AllEnemies',
        recoil: { type: 'MaxHP', percent: 100 },
        effects: [{ type: 'Damage' }]
    }
};

// Minimal Engine Simulation
function executeMove(attacker, defender, move) {
    console.log(`\n--- ${attacker.nickname} uses ${move.name} ---`);
    
    // 1. Disable Check (The Logic to Test)
    if (attacker.volatile['Disable'] && attacker.disabledMoveId === move.id) {
        console.log(`[FAIL] ${attacker.nickname}'s ${move.name} is disabled!`);
        return;
    }

    // 2. Set Last Move (The Logic to Test)
    attacker.lastMoveUsed = move.id;
    console.log(`[DEBUG] ${attacker.nickname} lastMoveUsed set to ${move.id}`);

    // 3. Effects
    move.effects.forEach(effect => {
        if (effect.type === 'Damage') {
            console.log(`[EFFECT] Dealt damage to ${defender.nickname}`);
            defender.currentHp -= 10; 
        }
        if (effect.type === 'Unique' && effect.volatileStatus === 'Disable') {
            if (defender.lastMoveUsed) {
                defender.volatile['Disable'] = 4;
                defender.disabledMoveId = defender.lastMoveUsed;
                console.log(`[EFFECT] Disabled ${defender.nickname}'s ${defender.lastMoveUsed}`);
            } else {
                console.log(`[EFFECT] Disable failed (No last move)`);
            }
        }
    });

    // 4. Recoil (The Logic to Test)
    if (move.recoil && move.recoil.type === 'MaxHP') {
        const recoil = Math.floor(attacker.currentStats.hp * (move.recoil.percent / 100));
        attacker.currentHp = Math.max(0, attacker.currentHp - recoil);
        console.log(`[RECOIL] ${attacker.nickname} took ${recoil} MaxHP damage. HP: ${attacker.currentHp}`);
    }
}

// --- TEST 1: DISABLE ---
console.log('=== TEST 1: DISABLE ===');
const p1 = mockPokemon('Vibrava', 100);
const p2 = mockPokemon('Venomoth', 90);

// Turn 1: Vibrava uses Bite
executeMove(p1, p2, mockMoves['bite']);

// Turn 1: Venomoth uses Disable
executeMove(p2, p1, mockMoves['disable']);

// Turn 2: Vibrava tries Bite again
executeMove(p1, p2, mockMoves['bite']); // Should Fail

// --- TEST 2: EXPLOSION ---
console.log('\n=== TEST 2: EXPLOSION ===');
const p3 = mockPokemon('Golem', 50);
executeMove(p3, p2, mockMoves['explosion']);

if (p3.currentHp === 0) {
    console.log('[PASS] Golem fainted from Explosion.');
} else {
    console.log('[FAIL] Golem survived Explosion.');
}
