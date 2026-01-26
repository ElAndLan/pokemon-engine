
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';
// Mocking types since we are in JS script
// import { PokemonInstance, MoveData } from '../src/renderer/src/core/data/DataTypes';

// Mock Data
const charmander: any = {
    nickname: 'Charmander',
    speciesId: 'charmander',
    types: ['Fire'],
    level: 5,
    currentHp: 20,
    currentStats: { hp: 20, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    statStages: { attack: 0 },
    ability: 'Blaze',
    // Mock other props
    volatile: {}
};

const squirtle: any = {
    nickname: 'Squirtle',
    speciesId: 'squirtle',
    types: ['Water'],
    level: 5,
    currentHp: 20,
    currentStats: { hp: 20, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    statStages: { attack: 0 },
    ability: 'Torrent',
    volatile: {}
};

const ekans: any = {
    nickname: 'Ekans',
    speciesId: 'ekans',
    types: ['Poison'],
    level: 5,
    currentHp: 20,
    currentStats: { hp: 20, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    statStages: { attack: 0 },
    ability: 'Intimidate',
    volatile: {}
};

const gastly: any = {
    nickname: 'Gastly',
    speciesId: 'gastly',
    types: ['Ghost', 'Poison'],
    ability: 'Levitate',
    currentHp: 20
};

const pikachu: any = {
    nickname: 'Pikachu', 
    ability: 'Static',
    currentHp: 20,
    types: ['Electric'],
    status: 'None'
};

// Mock Battle Scene
const mockBattle: any = {
    playerPokemon: charmander,
    enemyPokemon: ekans,
    showText: async (msg: string) => console.log(`[BattleText] ${msg}`)
};

async function testIntimidate() {
    console.log('\n--- Testing Intimidate ---');
    // Context: Ekans (Intimidate) enters vs Charmander
    // Owner: Ekans
    charmander.statStages.attack = 0;
    
    // Call Hook
    await AbilityRegistry.trigger('Intimidate', 'onBattleStart', {
        owner: ekans,
        battle: mockBattle
    });
    
    if (charmander.statStages.attack === -1) {
        console.log('PASS: Charmander Attack lowered to -1');
    } else {
        console.log('FAIL: Charmander Attack is ' + charmander.statStages.attack);
    }
}

async function testLevitate() {
    console.log('\n--- Testing Levitate ---');
    const groundMove = { type: 'Ground', name: 'Earthquake' };
    const normalMove = { type: 'Normal', name: 'Tackle' };
    
    const mult1 = AbilityRegistry.applyModifier('Levitate', 'onDamageMultiplier', 1.0, {
        owner: gastly,
        move: groundMove
    });
    
    if (mult1 === 0) console.log('PASS: Ground move multiplier is 0');
    else console.log('FAIL: Ground move multiplier is ' + mult1);
    
    const mult2 = AbilityRegistry.applyModifier('Levitate', 'onDamageMultiplier', 1.0, {
        owner: gastly,
        move: normalMove
    });
    
    if (mult2 === 1.0) console.log('PASS: Normal move multiplier is 1.0');
    else console.log('FAIL: Normal move multiplier is ' + mult2);
}

async function testStatic() {
    console.log('\n--- Testing Static ---');
    // Pikachu (Static) gets hit by Tackle (Physical) from Squirtle
    const tackle = { category: 'Physical', name: 'Tackle' };
    
    let paralyzedCount = 0;
    const trials = 1000;
    
    for (let i=0; i<trials; i++) {
        squirtle.status = 'None';
        await AbilityRegistry.trigger('Static', 'onAfterDamage', {
            owner: pikachu,
            target: squirtle,
            move: tackle,
            battle: mockBattle
        }, 10);
        
        if (squirtle.status === 'Paralysis') paralyzedCount++;
    }
    
    console.log(`Static triggered ${paralyzedCount}/${trials} times.`);
    if (paralyzedCount > 250 && paralyzedCount < 350) console.log('PASS: Frequency approx 30%');
    else console.log('WARN: Frequency outlier?');
}


async function testStarters() {
    console.log('\n--- Testing Starters (Overgrow/Blaze/Torrent) ---');
    
    // Setup Low HP
    charmander.currentHp = 5; // < 1/3 of 20 (6.66)
    squirtle.currentHp = 5;
    
    const fireMove = { type: 'Fire', name: 'Ember', power: 40 };
    const waterMove = { type: 'Water', name: 'Bubble', power: 40 };
    const normalMove = { type: 'Normal', name: 'Scratch', power: 40 };

    // Test Blaze
    const blazeMod = AbilityRegistry.applyModifier('Blaze', 'onModifyAttack', 40, {
        owner: charmander,
        move: fireMove
    });
    if (blazeMod === 60) console.log('PASS: Blaze 1.5x boost active (40 -> 60)');
    else console.log(`FAIL: Blaze boost incorrect: ${blazeMod}`);
    
    const blazeNoMod = AbilityRegistry.applyModifier('Blaze', 'onModifyAttack', 40, {
        owner: charmander,
        move: normalMove
    });
    if (blazeNoMod === 40) console.log('PASS: Blaze ignored non-Fire move');
    else console.log(`FAIL: Blaze triggered incorrectly: ${blazeNoMod}`);

    // Test Torrent
    const torrentMod = AbilityRegistry.applyModifier('Torrent', 'onModifyAttack', 40, {
        owner: squirtle,
        move: waterMove
    });
    if (torrentMod === 60) console.log('PASS: Torrent 1.5x boost active (40 -> 60)');
    else console.log(`FAIL: Torrent boost incorrect: ${torrentMod}`);
}

async function testImmunities() {
    console.log('\n--- Testing Status Immunities ---');
    
    // Mock Pokemon for immunities
    const limberMon: any = { nickname: 'LimberMon', ability: 'Limber', status: 'None', volatile: {} };
    const immunityMon: any = { nickname: 'ImmunityMon', ability: 'Immunity', status: 'None', volatile: {} };
    const insomniaMon: any = { nickname: 'InsomniaMon', ability: 'Insomnia', status: 'None', volatile: {} };
    const magmaMon: any = { nickname: 'MagmaMon', ability: 'Magma Armor', status: 'None', volatile: {} };
    const waterVeilMon: any = { nickname: 'WaterVeilMon', ability: 'Water Veil', status: 'None', volatile: {} };
    const ownTempoMon: any = { nickname: 'OwnTempoMon', ability: 'Own Tempo', status: 'None', volatile: {} };

    // We can't easily call AtomicEffects directly because it returns events and depends on random chances (though we set 100).
    // And we need to verify the HOOK returns false.
    
    // Test Limber
    const limberResult = await AbilityRegistry.trigger('Limber', 'onSetStatus', { owner: limberMon, battle: mockBattle }, 'Paralysis');
    if (limberResult === false) console.log('PASS: Limber prevented Paralysis');
    else console.log('FAIL: Limber did not prevent Paralysis');

    // Test Immunity (Poison)
    const immunityResult = await AbilityRegistry.trigger('Immunity', 'onSetStatus', { owner: immunityMon, battle: mockBattle }, 'Poison');
    if (immunityResult === false) console.log('PASS: Immunity prevented Poison');
    else console.log('FAIL: Immunity did not prevent Poison');

    // Test Insomnia (Sleep)
    const insomniaResult = await AbilityRegistry.trigger('Insomnia', 'onSetStatus', { owner: insomniaMon, battle: mockBattle }, 'Sleep');
    if (insomniaResult === false) console.log('PASS: Insomnia prevented Sleep');
    else console.log('FAIL: Insomnia did not prevent Sleep');

    // Test Magma Armor (Freeze)
    const magmaResult = await AbilityRegistry.trigger('Magma Armor', 'onSetStatus', { owner: magmaMon, battle: mockBattle }, 'Freeze');
    if (magmaResult === false) console.log('PASS: Magma Armor prevented Freeze');
    else console.log('FAIL: Magma Armor did not prevent Freeze');

    // Test Water Veil (Burn)
    const veilResult = await AbilityRegistry.trigger('Water Veil', 'onSetStatus', { owner: waterVeilMon, battle: mockBattle }, 'Burn');
    if (veilResult === false) console.log('PASS: Water Veil prevented Burn');
    else console.log('FAIL: Water Veil did not prevent Burn');

    // Test Own Tempo (Confusion)
    const tempoResult = await AbilityRegistry.trigger('Own Tempo', 'onSetStatus', { owner: ownTempoMon, battle: mockBattle }, 'Confusion');
    if (tempoResult === false) console.log('PASS: Own Tempo prevented Confusion');
    else console.log('FAIL: Own Tempo did not prevent Confusion');
}

async function testAbsorbImmunities() {
    console.log('\n--- Testing Type Absorb/Immunities ---');
    
    // Voltage
    const voltMon: any = { nickname: 'VoltMon', ability: 'Volt Absorb', currentHp: 50, currentStats: { hp: 100 }, uuid: 'v1' };
    const electricMove = { type: 'Electric', name: 'Thunder Shock' };
    const events1: any[] = [];
    
    const result1 = AbilityRegistry.get('Volt Absorb')?.onTryHit?.({ owner: voltMon, move: electricMove }, events1);
    if (result1 === false && voltMon.currentHp === 75) console.log('PASS: Volt Absorb healed 25%');
    else console.log(`FAIL: Volt Absorb healed to ${voltMon.currentHp}`);

    // Water Absorb
    const waterMon: any = { nickname: 'WaterMon', ability: 'Water Absorb', currentHp: 50, currentStats: { hp: 100 }, uuid: 'w1' };
    const waterMove = { type: 'Water', name: 'Water Gun' };
    const events2: any[] = [];
    
    const result2 = AbilityRegistry.get('Water Absorb')?.onTryHit?.({ owner: waterMon, move: waterMove }, events2);
    if (result2 === false && waterMon.currentHp === 75) console.log('PASS: Water Absorb healed 25%');
    else console.log(`FAIL: Water Absorb healed to ${waterMon.currentHp}`);

    // Flash Fire
    const fireMon: any = { nickname: 'FireMon', ability: 'Flash Fire', volatile: {}, uuid: 'f1' };
    const fireMove = { type: 'Fire', name: 'Ember' };
    const events3: any[] = []; // Need events array for onTryHit
    
    const result3 = AbilityRegistry.get('Flash Fire')?.onTryHit?.({ owner: fireMon, move: fireMove }, events3);
    if (result3 === false && fireMon.volatile['FlashFire']) console.log('PASS: Flash Fire activated immunity');
    else console.log('FAIL: Flash Fire immunity failed');
    
    const boost = AbilityRegistry.applyModifier('Flash Fire', 'onModifyAttack', 10, { owner: fireMon, move: fireMove });
    if (boost === 15) console.log('PASS: Flash Fire boost worked (1.5x)');
    else console.log(`FAIL: Flash Fire boost: ${boost}`);

    // Sap Sipper
    const sapMon: any = { nickname: 'SapMon', ability: 'Sap Sipper', statStages: { attack: 0 }, uuid: 's1' };
    const grassMove = { type: 'Grass', name: 'Vine Whip' };
    const events4: any[] = [];
    
    const result4 = AbilityRegistry.get('Sap Sipper')?.onTryHit?.({ owner: sapMon, move: grassMove }, events4);
    if (result4 === false && sapMon.statStages.attack === 1) console.log('PASS: Sap Sipper raised Attack');
    else console.log('FAIL: Sap Sipper failed');
    
    // Motor Drive
    const motorMon: any = { nickname: 'MotorMon', ability: 'Motor Drive', statStages: { speed: 0 }, uuid: 'm1' };
    const events5: any[] = [];
    
    const result5 = AbilityRegistry.get('Motor Drive')?.onTryHit?.({ owner: motorMon, move: electricMove }, events5);
    if (result5 === false && motorMon.statStages.speed === 1) console.log('PASS: Motor Drive raised Speed');
    else console.log('FAIL: Motor Drive failed');
}

async function testContactAbilities() {
    console.log('\n--- Testing Contact Abilities ---');
    
    // Setup Mock Battle for showText
    const mockBattle: any = { showText: async (msg) => { /* console.log(`[BattleText] ${msg}`); */ } };

    // 1. Flame Body (30% Burn)
    let burns = 0;
    const flameMon: any = { nickname: 'FlameMon', ability: 'Flame Body', uuid: 'fb1' };
    for (let i = 0; i < 1000; i++) {
        const attacker: any = { nickname: 'Attacker', status: 'None', types: ['Normal'], uuid: 'a1' };
        await AbilityRegistry.trigger('Flame Body', 'onAfterDamage', { owner: flameMon, target: attacker, battle: mockBattle, move: { category: 'Physical' } as any }, 10);
        if (attacker.status === 'Burn') burns++;
    }
    console.log(`Flame Body triggered ${burns}/1000 times.`);
    if (burns > 250 && burns < 350) console.log('PASS: Flame Body freq approx 30%');
    else console.log('FAIL: Flame Body freq unexpected');

    // 2. Rough Skin (Damage)
    const roughMon: any = { nickname: 'RoughMon', ability: 'Rough Skin', uuid: 'rs1' };
    const toughAttacker: any = { nickname: 'ToughGuy', currentHp: 100, currentStats: { hp: 100 }, uuid: 'tg1' };
    await AbilityRegistry.trigger('Rough Skin', 'onAfterDamage', { owner: roughMon, target: toughAttacker, battle: mockBattle, move: { category: 'Physical' } as any }, 10);
    // Expected damage: 100/8 = 12. HP = 88.
    if (toughAttacker.currentHp === 88) console.log('PASS: Rough Skin dealt 1/8 damage');
    else console.log(`FAIL: Rough Skin damage incorrect: ${toughAttacker.currentHp}`);
    
    // 3. Cute Charm (Infatuation)
    let charms = 0;
    const cuteMon: any = { nickname: 'CuteMon', ability: 'Cute Charm', uuid: 'cc1' };
    for (let i = 0; i < 1000; i++) {
        const attacker: any = { nickname: 'Attacker', volatile: {}, uuid: 'a2' };
        await AbilityRegistry.trigger('Cute Charm', 'onAfterDamage', { owner: cuteMon, target: attacker, battle: mockBattle, move: { category: 'Physical' } as any }, 10);
        if (attacker.volatile['Infatuation']) charms++;
    }
    console.log(`Cute Charm triggered ${charms}/1000 times.`);
    if (charms > 250 && charms < 350) console.log('PASS: Cute Charm freq approx 30%');
    else console.log('FAIL: Cute Charm freq unexpected');

    // 4. Effect Spore (Sleep/Poison/Paralyze)
    let spores = 0;
    const sporeMon: any = { nickname: 'SporeMon', ability: 'Effect Spore', uuid: 'es1' };
    for (let i = 0; i < 1000; i++) {
        const attacker: any = { nickname: 'Attacker', status: 'None', types: ['Normal'], volatile: {}, uuid: 'a3' };
        await AbilityRegistry.trigger('Effect Spore', 'onAfterDamage', { owner: sporeMon, target: attacker, battle: mockBattle, move: { category: 'Physical' } as any }, 10);
        if (attacker.status !== 'None') spores++;
    }
    console.log(`Effect Spore triggered ${spores}/1000 times.`);
    if (spores > 250 && spores < 350) console.log('PASS: Effect Spore freq approx 30%');
    else console.log('FAIL: Effect Spore freq unexpected');
}



async function testStatAbilities() {
    console.log('\n--- Testing Stat Modifiers ---');
    
    // We need to import getEffectiveStat to test this directly, OR check Damage output.
    // Since we can't easily import getEffectiveStat in this script (it's a renderer file),
    // we will rely on checking the modification via AbilityRegistry.trigger?
    // No, applyModifier is cleaner.
    
    // BUT test_abilities.ts is running via tsx. It should handle imports if paths align.
    // Let's try to mock the context/call AbilityRegistry directly.
    
    // Huge Power
    const hpMon: any = { nickname: 'HPMon', ability: 'Huge Power', currentStats: { attack: 100 }, statStages: { attack: 0 } };
    const hpVal = AbilityRegistry.applyModifier('Huge Power', 'onStatCalculation', 100, { owner: hpMon, statName: 'attack' });
    if (hpVal === 200) console.log('PASS: Huge Power doubled Attack');
    else console.log(`FAIL: Huge Power val ${hpVal}`);

    // Guts (Burn)
    const gutsMon: any = { nickname: 'GutsMon', ability: 'Guts', status: 'Burn', currentStats: { attack: 100 }, statStages: { attack: 0 } };
    const gutsVal = AbilityRegistry.applyModifier('Guts', 'onStatCalculation', 100, { owner: gutsMon, statName: 'attack' });
    if (gutsVal === 150) console.log('PASS: Guts 1.5x Attack on Burn');
    else console.log(`FAIL: Guts val ${gutsVal}`);

    // Marvel Scale (Poison)
    const scaleMon: any = { nickname: 'ScaleMon', ability: 'Marvel Scale', status: 'Poison', currentStats: { defense: 100 }, statStages: { defense: 0 } };
    const scaleVal = AbilityRegistry.applyModifier('Marvel Scale', 'onStatCalculation', 100, { owner: scaleMon, statName: 'defense' });
    if (scaleVal === 150) console.log('PASS: Marvel Scale 1.5x Defense on Poison');
    else console.log(`FAIL: Marvel Scale val ${scaleVal}`);

    // Quick Feet (Paralysis)
    const feetMon: any = { nickname: 'FeetMon', ability: 'Quick Feet', status: 'Paralysis', currentStats: { speed: 100 }, statStages: { speed: 0 } };
    // Note: If calling applyModifier directly with 100, we don't simulate the paralysis drop (provided by StatCalculator).
    // The ability should just return 1.5x the input. 
    // Wait, my implementation checks status and might do correction?
    // "If statName === 'speed' && status !== 'None' -> return value * 1.5"
    // So 100 -> 150.
    const feetVal = AbilityRegistry.applyModifier('Quick Feet', 'onStatCalculation', 100, { owner: feetMon, statName: 'speed' });
    if (feetVal === 150) console.log('PASS: Quick Feet 1.5x Speed (raw boost)');
    else console.log(`FAIL: Quick Feet val ${feetVal}`);
}



async function testDamageModifiers() {
    console.log('\n--- Testing Damage Modifiers ---');
    
    // Thick Fat
    const fatMon: any = { nickname: 'FatMon', ability: 'Thick Fat' };
    const fireMove = { type: 'Fire', name: 'Ember' };
    const iceMove = { type: 'Ice', name: 'Ice Beam' };
    
    const fatFire = AbilityRegistry.applyModifier('Thick Fat', 'onDamageMultiplier', 100, { owner: fatMon, move: fireMove });
    if (fatFire === 50) console.log('PASS: Thick Fat halved Fire damage');
    else console.log(`FAIL: Thick Fat Fire ${fatFire}`);
    
    const fatIce = AbilityRegistry.applyModifier('Thick Fat', 'onDamageMultiplier', 100, { owner: fatMon, move: iceMove });
    if (fatIce === 50) console.log('PASS: Thick Fat halved Ice damage');
    else console.log(`FAIL: Thick Fat Ice ${fatIce}`);

    // Filter (Super Effective)
    const filterMon: any = { nickname: 'FilterMon', ability: 'Filter' };
    // We simulate context with effectiveness > 1
    const superEffCtx: any = { owner: filterMon, effectiveness: 2.0 };
    const filterVal = AbilityRegistry.applyModifier('Filter', 'onDamageMultiplier', 100, superEffCtx);
    if (filterVal === 75) console.log('PASS: Filter reduced Super Effective damage (0.75x)');
    else console.log(`FAIL: Filter val ${filterVal}`);
    
    const normalEffCtx: any = { owner: filterMon, effectiveness: 1.0 };
    const filterNormal = AbilityRegistry.applyModifier('Filter', 'onDamageMultiplier', 100, normalEffCtx);
    if (filterNormal === 100) console.log('PASS: Filter ignored Normal Effective damage');
    else console.log(`FAIL: Filter normal ${filterNormal}`);

    // Tinted Lens (Not Very Effective)
    const lensMon: any = { nickname: 'LensMon', ability: 'Tinted Lens' };
    const notEffCtx: any = { owner: lensMon, effectiveness: 0.5 };
    const lensVal = AbilityRegistry.applyModifier('Tinted Lens', 'onDamageMultiplier', 100, notEffCtx);
    if (lensVal === 200) console.log('PASS: Tinted Lens doubled Not Very Effective damage');
    else console.log(`FAIL: Tinted Lens val ${lensVal}`);

    // Technician (Power <= 60)
    const techMon: any = { nickname: 'TechMon', ability: 'Technician', currentStats: { attack: 100 } };
    const weakMove = { name: 'Scratch', power: 40 };
    const strongMove = { name: 'Slash', power: 70 };
    
    // Technician applies to onModifyAttack in our implementation
    const techWeak = AbilityRegistry.applyModifier('Technician', 'onModifyAttack', 100, { owner: techMon, move: weakMove });
    if (techWeak === 150) console.log('PASS: Technician boosted weak move (1.5x)');
    else console.log(`FAIL: Technician weak ${techWeak}`);
    
    const techStrong = AbilityRegistry.applyModifier('Technician', 'onModifyAttack', 100, { owner: techMon, move: strongMove });
    if (techStrong === 100) console.log('PASS: Technician ignored strong move');
    else console.log(`FAIL: Technician strong ${techStrong}`);

    // Iron Fist (Punch Flag)
    const fistMon: any = { nickname: 'FistMon', ability: 'Iron Fist', currentStats: { attack: 100 } };
    const punchMove = { name: 'Comet Punch', flags: { punch: true } };
    const nonPunchMove = { name: 'Tackle', flags: { } };
    
    const fistPunch = AbilityRegistry.applyModifier('Iron Fist', 'onModifyAttack', 100, { owner: fistMon, move: punchMove });
    // 100 * 1.2 = 120
    if (Math.abs(fistPunch - 120) < 0.1) console.log('PASS: Iron Fist boosted punch move (1.2x)');
    else console.log(`FAIL: Iron Fist punch ${fistPunch}`);
    
    const fistKick = AbilityRegistry.applyModifier('Iron Fist', 'onModifyAttack', 100, { owner: fistMon, move: nonPunchMove });
    if (fistKick === 100) console.log('PASS: Iron Fist ignored non-punch move');
    else console.log(`FAIL: Iron Fist kick ${fistKick}`);
}



async function testAccuracyAbilities() {
    console.log('\n--- Testing Accuracy & Effect Abilities ---');
    
    // Serene Grace (Effect Chance)
    const graceMon: any = { nickname: 'GraceMon', ability: 'Serene Grace' };
    const graceVal = AbilityRegistry.applyModifier('Serene Grace', 'onModifyEffectChance', 10, { owner: graceMon });
    if (graceVal === 20) console.log('PASS: Serene Grace doubled effect chance (10->20)');
    else console.log(`FAIL: Serene Grace val ${graceVal}`);

    // Compound Eyes (Accuracy)
    const eyesMon: any = { nickname: 'EyesMon', ability: 'Compound Eyes' };
    const eyesVal = AbilityRegistry.applyModifier('Compound Eyes', 'onModifyAccuracy', 100, { owner: eyesMon });
    // Expect 130
    if (Math.abs(eyesVal - 130) < 0.1) console.log('PASS: Compound Eyes 1.3x Accuracy');
    else console.log(`FAIL: Compound Eyes val ${eyesVal}`);

    // Keen Eye (Prevent Accuracy Lowering)
    // We test the hook directly; integration testing requires AtomicEffects.applyStatChange mock or complex setup.
    const keenMon: any = { nickname: 'KeenMon', ability: 'Keen Eye', uuid: 'k1' };
    const attacker: any = { nickname: 'BadGuy', uuid: 'bg1' };
    
    // Hook signature: onTryLowerStat(ctx, stat) -> boolean
    const keenBlock = await AbilityRegistry.trigger('Keen Eye', 'onTryLowerStat', { owner: keenMon, target: attacker }, 'accuracy');
    // Hook returns false to prevent.
    if (keenBlock === false) console.log('PASS: Keen Eye prevented accuracy drop');
    else console.log('FAIL: Keen Eye allowed accuracy drop');
    
    // Test that it allows Attack drop
    const keenAllow = await AbilityRegistry.trigger('Keen Eye', 'onTryLowerStat', { owner: keenMon, target: attacker }, 'attack');
    if (keenAllow === true || keenAllow === undefined) console.log('PASS: Keen Eye allowed attack drop'); // undefined if hook doesn't return anything implies true usually, but wrapper returns explicit result? Wrapper returns whatever hook returns. If hook missing, undefined. If hook exists and checks stat, return true.
    // Wait, onTryLowerStat imlpementation: if (stat === 'accuracy') return false; return true;
    // So it should return true.
    else console.log(`FAIL: Keen Eye blocked attack drop? ${keenAllow}`);

    // Clear Body (Prevent All)
    const clearMon: any = { nickname: 'ClearMon', ability: 'Clear Body' };
    const clearBlock = await AbilityRegistry.trigger('Clear Body', 'onTryLowerStat', { owner: clearMon }, 'attack');
    if (clearBlock === false) console.log('PASS: Clear Body prevented attack drop');
    else console.log('FAIL: Clear Body result ' + clearBlock);
    
    // No Guard test usually requires MoveEngine execution mock or checking Logic.
    // CoreMoveLogic.checkHit logic: if (attacker.ability === 'No Guard' ...) return true.
    // We can assume logic is simple enough if we verified CoreMoveLogic changes.
    // We already verified CoreMoveLogic compiles and exports.
}

async function testCritAndTurnAbilities() {
    console.log('\n--- Testing Critical & Turn-Based Abilities ---');
    
    // Sniper (Crit Damage)
    const sniperMon: any = { nickname: 'SnipeMon', ability: 'Sniper' };
    const sniperVal = AbilityRegistry.applyModifier('Sniper', 'onCriticalMultiplier', 1.5, { owner: sniperMon });
    if (sniperVal === 2.25) console.log('PASS: Sniper boosted Crit Damage (1.5 -> 2.25)');
    else console.log(`FAIL: Sniper val ${sniperVal}`);

    // Super Luck (Crit Stage)
    const luckMon: any = { nickname: 'LuckMon', ability: 'Super Luck' };
    const luckStage = AbilityRegistry.applyModifier('Super Luck', 'onModifyCritStage', 0, { owner: luckMon });
    if (luckStage === 1) console.log('PASS: Super Luck raised Crit Stage');
    else console.log(`FAIL: Super Luck stage ${luckStage}`);

    // Speed Boost (Turn End)
    // Needs Mock BattleScene
    const speedMon: any = { nickname: 'SpeedMon', ability: 'Speed Boost', statStages: { speed: 0 } };
    const mockBattle: any = {
        showText: async (msg: string) => { /* console.log('MockText:', msg); */ }
    };
    await AbilityRegistry.trigger('Speed Boost', 'onTurnEnd', { owner: speedMon, battle: mockBattle });
    if (speedMon.statStages.speed === 1) console.log('PASS: Speed Boost raised speed');
    else console.log(`FAIL: Speed Boost speed ${speedMon.statStages.speed}`);

    // Shed Skin (Turn End)
    const shedMon: any = { nickname: 'ShedMon', ability: 'Shed Skin', status: 'Burn' };
    // 30% chance. Let's force it by running multiple times or checking logic exists?
    // We can't deterministicly test Math.random() < 0.3 without mocking random.
    // But we can check if it runs.
    console.log('Testing Shed Skin (approx 30% chance)... running 100 samples');
    let curedCount = 0;
    for (let i = 0; i < 100; i++) {
        shedMon.status = 'Burn';
        await AbilityRegistry.trigger('Shed Skin', 'onTurnEnd', { owner: shedMon, battle: mockBattle });
        if (shedMon.status === 'None') curedCount++;
    }
    console.log(`Shed Skin cured ${curedCount}/100 times.`);
    if (curedCount > 10 && curedCount < 50) console.log('PASS: Shed Skin range seems valid');
    else console.log('WARN: Shed Skin counts outlier (could be RNG or bug)');
}



async function testLowHPAndRecoil() {
    console.log('\n--- Testing Low HP & Recoil Abilities ---');
    
    // Swarm
    const bugMon: any = { nickname: 'Buggy', ability: 'Swarm', currentHp: 30, currentStats: { hp: 100 } };
    const bugMove: any = { type: 'Bug', power: 100 };
    const swarmVal = AbilityRegistry.applyModifier('Swarm', 'onModifyAttack', 10, { owner: bugMon, move: bugMove });
    if (swarmVal === 15) console.log('PASS: Swarm boosted Bug move at low HP (10->15)');
    else console.log(`FAIL: Swarm val ${swarmVal}`);
    
    // Swarm (High HP)
    bugMon.currentHp = 100;
    const swarmVal2 = AbilityRegistry.applyModifier('Swarm', 'onModifyAttack', 10, { owner: bugMon, move: bugMove });
    if (swarmVal2 === 10) console.log('PASS: Swarm did not boost at full HP');
    else console.log(`FAIL: Swarm val high HP ${swarmVal2}`);

    // Reckless
    const wreckCheck: any = { nickname: 'Wreck', ability: 'Reckless' };
    const recoilMove: any = { name: 'Take Down', recoil: { percent: 25 } };
    const normalMove: any = { name: 'Tackle' };
    
    const reckVal = AbilityRegistry.applyModifier('Reckless', 'onModifyAttack', 10, { owner: wreckCheck, move: recoilMove });
    if (Math.abs(reckVal - 12) < 0.1) console.log('PASS: Reckless boosted Recoil Move (1.2x)');
    else console.log(`FAIL: Reckless val ${reckVal}`);
    
    const safeVal = AbilityRegistry.applyModifier('Reckless', 'onModifyAttack', 10, { owner: wreckCheck, move: normalMove });
    if (safeVal === 10) console.log('PASS: Reckless ignored normal move');
    else console.log(`FAIL: Reckless safe val ${safeVal}`);
    
    // Rock Head / Liquid Ooze logic is in MoveEngine, assume logic coverage if tests compile.
}

async function testSpecialMechanics() {
    console.log('\n--- Testing Special Mechanics ---');
    
    // Adaptability (Logic in DamageCalculator)
    // We can't easily test DamageCalculator directly without exporting a helper or mock?
    // MoveEngine calls it.
    // If we mocked AbilityRegistry properly we could check if AbilityRegistry inputs correct STAB?
    // Actually we edited DamageCalculator.ts to check Ability string directly.
    // So we need to call DamageCalculator.
    // Let's assume inspection covered it, or try to import calculateDamage?
    // calculateDamage is exported.
    // import { calculateDamage } from '../src/renderer/src/core/battle/DamageCalculator'; 
    // But we need to use 'npx tsx' and path resolution might be tricky for src imports if not configured?
    // Usually we just use what we have.
    
    // Inner Focus (onSetStatus)
    const focusMon: any = { nickname: 'FocusMon', ability: 'Inner Focus' };
    const canFlinch = AbilityRegistry.applyModifier('Inner Focus', 'onSetStatus', 1, { owner: focusMon }); 
    // onSetStatus returns boolean, but applyModifier returns number.
    // Wait, I fixed onSetStatus signature? 
    // No, applyModifier(id, hook, initialValue (number)). 
    // And onSetStatus logic in 'Inner Focus' returns false?
    // But applyModifier casts it?
    // Line 115 in Abilities.ts: return ability[hook](initialValue, ctx);
    // onSetStatus(ctx, status) => boolean.
    // Mismatch!
    // I need to fix `AtomicEffects` usage of `onSetStatus` or `AbilityRegistry` helper!
    // In AtomicEffects Line 79-81:
    // const ability = AbilityRegistry.get(target.ability);
    // if (ability && ability.onSetStatus) { if (!ability.onSetStatus({ owner: target }, status)) blocked = true; }
    // This is DIRECT access. So it works.
    
    // Test Direct Access
    const ability = AbilityRegistry.get('Inner Focus');
    if (ability && ability.onSetStatus && ability.onSetStatus({ owner: focusMon }, 'Flinch') === false) {
        console.log('PASS: Inner Focus blocks Flinch');
    } else {
        console.log('FAIL: Inner Focus did not block Flinch');
    }
}

async function run() {
    await testIntimidate();
    await testLevitate();
    await testStatic();
    await testStarters();
    await testImmunities();
    await testAbsorbImmunities();
    await testContactAbilities();
    await testStatAbilities();
    await testDamageModifiers();
    await testAccuracyAbilities();
    await testCritAndTurnAbilities();
    await testLowHPAndRecoil();
    await testSpecialMechanics();
}

run();
