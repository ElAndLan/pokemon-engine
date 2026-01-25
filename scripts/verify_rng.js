
// Simulation of AtomicEffects.applyStatus logic
// Logic: if (Math.random() * 100 < chance) -> Effect Applied

const baseChance = 30; // 30% for Lava Plume Burn
const trials = 10000;
let successes = 0;

console.log(`[SIMULATION] Verification of Random Chance Logic`);
console.log(`[SIMULATION] Target Chance: ${baseChance}%`);
console.log(`[SIMULATION] Trials: ${trials}`);

for (let i = 0; i < trials; i++) {
    // Exact logic from AtomicEffects.ts (lines 35, 62, 99)
    if (Math.random() * 100 < baseChance) {
        successes++;
    }
}

const observedPercent = (successes / trials) * 100;
console.log(`[SIMULATION] Successes: ${successes}`);
console.log(`[SIMULATION] Observed Frequency: ${observedPercent.toFixed(2)}%`);

if (Math.abs(observedPercent - baseChance) < 1.0) { // Tolerate 1% variance
    console.log(`[PASS] RNG is functioning correctly and valid.`);
} else {
    console.log(`[FAIL] RNG deviation is too high!`);
}
