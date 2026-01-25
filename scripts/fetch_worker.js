const { parentPort } = require('worker_threads');
const https = require('https');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const movesDb = require(movesPath);

const moveNameMap = {};
Object.keys(movesDb).forEach(moveId => {
    const moveName = movesDb[moveId].name.toLowerCase().replace(/[^a-z0-9]/g, '');
    moveNameMap[moveName] = moveId;
});

function mapMoveName(pokeApiName) {
    const normalized = pokeApiName.toLowerCase().replace(/-/g, '_');
    if (movesDb[normalized]) return normalized;
    const stripped = pokeApiName.toLowerCase().replace(/[^a-z0-9]/g, '');
    if (moveNameMap[stripped]) return moveNameMap[stripped];
    
    const specialCases = {
        'sand-attack': 'sand_attack',
        'solarbeam': 'solar_beam',
        'thunderpunch': 'thunder_punch',
        'ice-punch': 'ice_punch',
        'fire-punch': 'fire_punch',
        'ancientpower': 'ancient_power',
        'extremespeed': 'extreme_speed',
        'double-edge': 'double_edge',
        'self-destruct': 'self_destruct',
        'smelling-salts': 'smelling_salts',
        'wake-up-slap': 'wake_up_slap',
        'feint-attack': 'feint_attack',
        'poison-jab': 'poison_jab',
        'draco-meteor': 'draco_meteor',
        'seed-bomb': 'seed_bomb',
        'night-slash': 'night_slash',
        'air-slash': 'air_slash',
        'bug-buzz': 'bug_buzz',
        'dragon-pulse': 'dragon_pulse',
        'dark-pulse': 'dark_pulse',
        'aura-sphere': 'aura_sphere',
        'rock-polish': 'rock_polish',
        'earth-power': 'earth_power',
        'giga-impact': 'giga_impact',
        'stone-edge': 'stone_edge',
        'cross-poison': 'cross_poison',
        'x-scissor': 'x_scissor',
        'night-shade': 'night_shade',
        'aerial-ace': 'aerial_ace',
        'double-team': 'double_team',
        'pay-day': 'pay_day',
        'quick-attack': 'quick_attack',
        'rage': 'rage',
        'bide': 'bide',
        'mist': 'mist',
        'focus-energy': 'focus_energy',
        'teleport': 'teleport',
        'screech': 'screech',
        'acid-armor': 'acid_armor',
        'haze': 'haze',
        'soft-boiled': 'soft_boiled',
        'hi-jump-kick': 'hi_jump_kick',
        'glare': 'glare',
        'conversion': 'conversion',
        'substitute': 'substitute',
        'sketch': 'sketch',
        'triple-kick': 'triple_kick',
        'thief': 'thief',
        'spider-web': 'spider_web',
        'mind-reader': 'mind_reader',
        'nightmare': 'nightmare',
        'flame-wheel': 'flame_wheel',
        'curse': 'curse',
        'protect': 'protect',
        'scary-face': 'scary_face',
        'belly-drum': 'belly_drum',
        'sludge-bomb': 'sludge_bomb',
        'milk-drink': 'milk_drink',
        'spark': 'spark',
        'foresight': 'foresight',
        'destiny-bond': 'destiny_bond',
        'perish-song': 'perish_song',
        'icy-wind': 'icy_wind',
        'detect': 'detect',
        'bone-rush': 'bone_rush',
        'lock-on': 'lock_on',
        'outrage': 'outrage',
        'sandstorm': 'sandstorm',
        'giga-drain': 'giga_drain',
        'endure': 'endure',
        'charm': 'charm',
        'rollout': 'rollout',
        'false-swipe': 'false_swipe',
        'swagger': 'swagger',
        'metal-claw': 'metal_claw',
        'fury-cutter': 'fury_cutter',
        'steel-wing': 'steel_wing',
        'mean-look': 'mean_look',
        'attract': 'attract',
        'sleep-talk': 'sleep_talk',
        'heal-bell': 'heal_bell',
        'return': 'return',
        'present': 'present',
        'frustration': 'frustration',
        'sacred-fire': 'sacred_fire',
        'magnitude': 'magnitude',
        'dynamic-punch': 'dynamic_punch',
        'megahorn': 'megahorn',
        'dragon-breath': 'dragon_breath',
        'baton-pass': 'baton_pass',
        'encore': 'encore',
        'pursuit': 'pursuit',
        'rapid-spin': 'rapid_spin',
        'sweet-scent': 'sweet_scent',
        'iron-tail': 'iron_tail',
        'metal-sound': 'metal_sound',
        'vital-throw': 'vital_throw',
        'morning-sun': 'morning_sun',
        'synthesis': 'synthesis',
        'moonlight': 'moonlight',
        'hidden-power': 'hidden_power',
        'cross-chop': 'cross_chop',
        'swords-dance': 'swords_dance',
        'crunch': 'crunch',
        'mirror-coat': 'mirror_coat',
        'psych-up': 'psych_up',
        'extreme-speed': 'extreme_speed',
        'ancient-power': 'ancient_power',
        'shadow-ball': 'shadow_ball',
        'future-sight': 'future_sight',
        'rock-smash': 'rock_smash',
        'whirlpool': 'whirlpool',
        'beat-up': 'beat_up',
        'fake-out': 'fake_out',
        'uproar': 'uproar',
        'stockpile': 'stockpile',
        'spit-up': 'spit_up',
        'swallow': 'swallow',
        'heat-wave': 'heat_wave',
        'will-o-wisp': 'will_o_wisp',
        'facade': 'facade',
        'focus-punch': 'focus_punch',
        'smellingsalt': 'smellingsalt',
        'superpower': 'superpower',
        'magic-coat': 'magic_coat',
        'recycle': 'recycle',
        'brick-break': 'brick_break',
        'knock-off': 'knock_off',
        'endeavor': 'endeavor',
        'eruption': 'eruption',
        'skill-swap': 'skill_swap',
        'imprison': 'imprison',
        'refresh': 'refresh',
        'grudge': 'grudge',
        'snatch': 'snatch',
        'secret-power': 'secret_power',
        'dive': 'dive',
        'arm-thrust': 'arm_thrust',
        'camouflage': 'camouflage',
        'tail-glow': 'tail_glow',
        'luster-purge': 'luster_purge',
        'mist-ball': 'mist_ball',
        'feather-dance': 'feather_dance',
        'teeter-dance': 'teeter_dance',
        'blaze-kick': 'blaze_kick',
        'mud-sport': 'mud_sport',
        'water-sport': 'water_sport',
        'bullet-seed': 'bullet_seed',
        'aerial-ace': 'aerial_ace',
        'icicle-spear': 'icicle_spear',
        'iron-defense': 'iron_defense',
        'block': 'block',
        'howl': 'howl',
        'dragon-claw': 'dragon_claw',
        'bulk-up': 'bulk_up',
        'bounce': 'bounce',
        'mud-shot': 'mud_shot',
        'poison-tail': 'poison_tail',
        'covet': 'covet',
        'volt-tackle': 'volt_tackle',
        'magical-leaf': 'magical_leaf',
        'water-sport': 'water_sport',
        'calm-mind': 'calm_mind',
        'leaf-blade': 'leaf_blade',
        'dragon-dance': 'dragon_dance',
        'rock-blast': 'rock_blast',
        'shock-wave': 'shock_wave',
        'water-pulse': 'water_pulse',
        'doom-desire': 'doom_desire',
        'psycho-boost': 'psycho_boost'
    };
    
    if (specialCases[pokeApiName]) {
        return specialCases[pokeApiName];
    }
    
    return null;
}

function fetchJson(url) {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => res.statusCode === 200 ? resolve(JSON.parse(data)) : reject(new Error(`Failed: ${res.statusCode}`)));
        }).on('error', reject);
    });
}

async function fetchPokemonData(id) {
    const [pokemon, species] = await Promise.all([
        fetchJson(`https://pokeapi.co/api/v2/pokemon/${id}`),
        fetchJson(`https://pokeapi.co/api/v2/pokemon-species/${id}`)
    ]);
    return { pokemon, species };
}

async function fetchEvolutionChain(url) {
    return fetchJson(url);
}

function extractLevelUpMoves(pokemonData, targetGeneration) {
    const generationVersions = {
        1: ['red-blue', 'yellow'],
        2: ['gold-silver', 'crystal'],
        3: ['ruby-sapphire', 'emerald', 'fire-red-leaf-green'],
        4: ['diamond-pearl', 'platinum', 'heart-gold-soul-silver'],
        5: ['black-white', 'black-2-white-2']
    };
    
    const targetVersions = generationVersions[targetGeneration];
    const levelUpMoves = pokemonData.moves.filter(m => 
        m.version_group_details.some(v => v.move_learn_method.name === 'level-up' && targetVersions.includes(v.version_group.name))
    );
    
    const learnset = [];
    
    for (const move of levelUpMoves) {
        const versionDetails = move.version_group_details.filter(v => 
            v.move_learn_method.name === 'level-up' && targetVersions.includes(v.version_group.name)
        );
        
        const moveId = mapMoveName(move.move.name);
        if (moveId && versionDetails.length > 0) {
            const levels = [...new Set(versionDetails.map(v => v.level_learned_at))];
            levels.forEach(level => {
                if (!learnset.some(l => l.level === level && l.moveId === moveId)) {
                    learnset.push({ level, moveId });
                }
            });
        }
    }
    
    learnset.sort((a, b) => a.level - b.level);
    return learnset;
}

async function extractEvolutionData(speciesData, pokemonId) {
    const evolution = {};
    
    if (speciesData.evolves_from_species && speciesData.evolves_from_species.url) {
        evolution.preEvolutionId = speciesData.evolves_from_species.url.split('/').filter(Boolean).pop();
    }
    
    const currentId = pokemonId.toString();
    
    if (!speciesData.evolution_chain || !speciesData.evolution_chain.url) {
        return evolution;
    }
    
    try {
        const chainData = await fetchEvolutionChain(speciesData.evolution_chain.url);
        
        const findEvolution = (chain, targetId) => {
            const speciesId = chain.species.url.split('/').filter(Boolean).pop();
            if (speciesId === targetId) {
                if (chain.evolves_to.length > 0) {
                    const evolutions = [];
                    for (const nextEvo of chain.evolves_to) {
                        const targetSpeciesId = nextEvo.species.url.split('/').filter(Boolean).pop();
                        const evoDetail = nextEvo.evolution_details[0];
                        const evoData = { targetSpeciesId };
                        
                        if (evoDetail.min_level) evoData.level = evoDetail.min_level;
                        if (evoDetail.min_happiness) evoData.friendship = evoDetail.min_happiness;
                        if (evoDetail.min_affection) evoData.affection = evoDetail.min_affection;
                        if (evoDetail.item) {
                            evoData.item = evoDetail.item.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
                        }
                        if (evoDetail.held_item) {
                            evoData.heldItem = evoDetail.held_item.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
                        }
                        if (evoDetail.trigger.name === 'trade') evoData.trade = true;
                        if (evoDetail.known_move) {
                            evoData.knownMove = evoDetail.known_move.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
                        }
                        if (evoDetail.known_move_type) {
                            evoData.knownMoveType = evoDetail.known_move_type.name.charAt(0).toUpperCase() + evoDetail.known_move_type.name.slice(1);
                        }
                        if (evoDetail.time_of_day && evoDetail.time_of_day !== '') {
                            evoData.timeOfDay = evoDetail.time_of_day;
                        }
                        if (evoDetail.min_beauty) evoData.beauty = evoDetail.min_beauty;
                        if (evoDetail.relative_physical_stats !== undefined && evoDetail.relative_physical_stats !== null) {
                            evoData.relativePhysicalStats = evoDetail.relative_physical_stats;
                        }
                        if (evoDetail.needs_overworld_rain) evoData.needsOverworldRain = true;
                        if (evoDetail.turn_upside_down) evoData.turnUpsideDown = true;
                        if (evoDetail.location) {
                            evoData.location = evoDetail.location.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
                        }
                        if (evoDetail.party_species) {
                            evoData.partySpecies = evoDetail.party_species.name.charAt(0).toUpperCase() + evoDetail.party_species.name.slice(1);
                        }
                        if (evoDetail.party_type) {
                            evoData.partyType = evoDetail.party_type.name.charAt(0).toUpperCase() + evoDetail.party_type.name.slice(1);
                        }
                        if (evoDetail.gender) evoData.gender = evoDetail.gender;
                        
                        evolutions.push(evoData);
                    }
                    return evolutions;
                }
            }
            for (const evo of chain.evolves_to) {
                const result = findEvolution(evo, targetId);
                if (result) return result;
            }
            return null;
        };
        
        const nextEvos = findEvolution(chainData.chain, currentId);
        if (nextEvos && nextEvos.length > 0) {
            evolution.next = nextEvos;
        }
    } catch (error) {
        console.log(`  Warning: Could not fetch evolution chain for #${currentId}`);
    }
    
    return evolution;
}

async function processPokemon(id, generation) {
    try {
        const data = await fetchPokemonData(id);
        
        const pokemonData = data.pokemon;
        const speciesData = data.species;
        const name = pokemonData.name.charAt(0).toUpperCase() + pokemonData.name.slice(1);
        
        const learnset = extractLevelUpMoves(pokemonData, generation);
        const evolution = await extractEvolutionData(speciesData, id);
        
        return {
            id: id.toString(),
            name: name,
            generation: generation,
            learnset: learnset,
            evolution: evolution
        };
    } catch (error) {
        throw new Error(`Failed to fetch Pokemon #${id}: ${error.message}`);
    }
}

parentPort.on('message', async (message) => {
    if (message.type === 'process') {
        try {
            const result = await processPokemon(message.id, message.generation);
            parentPort.postMessage({ type: 'success', data: result });
        } catch (error) {
            parentPort.postMessage({ type: 'error', data: { id: message.id, error: error.message } });
        }
    }
});
