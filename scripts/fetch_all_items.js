// Script to fetch ALL Pokemon items from PokeAPI and create items.json
const fs = require('fs');
const path = require('path');

const POKEAPI_BASE = 'https://pokeapi.co/api/v2';
const OUTPUT_FILE = path.join(__dirname, '..', 'data', 'db', 'items.json');

// Rate limiting
const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

async function fetchWithRetry(url, retries = 3) {
  for (let i = 0; i < retries; i++) {
    try {
      const response = await fetch(url);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return await response.json();
    } catch (error) {
      if (i === retries - 1) throw error;
      await delay(1000 * (i + 1));
    }
  }
}

function categorizeItem(item) {
  const category = item.category?.name || '';
  
  // Map PokeAPI categories to our categories
  if (category.includes('medicine') || category.includes('healing') || category.includes('status-cures') || 
      category.includes('revival') || category.includes('pp-recovery') || category.includes('vitamins')) {
    return 'medicine';
  }
  if (category.includes('poke-balls') || category.includes('special-balls') || category.includes('standard-balls')) {
    return 'pokeballs';
  }
  if (category.includes('stat-boosts') || category.includes('effort-drop') || category.includes('battle-items') ||
      category.includes('in-a-pinch')) {
    return 'battle';
  }
  if (category.includes('berries')) {
    return 'berries';
  }
  if (category.includes('machines') || category.includes('all-machines')) {
    return 'tms';
  }
  if (category.includes('event-items') || category.includes('plot-advancement') || category.includes('key-items')) {
    return 'key';
  }
  
  // Default based on attributes
  if (item.attributes?.some(attr => attr.name === 'holdable')) return 'battle';
  return 'key';
}

function determineUsability(item) {
  const category = item.category?.name || '';
  const attributes = item.attributes?.map(a => a.name) || [];
  
  const canUseInBattle = 
    category.includes('medicine') ||
    category.includes('healing') ||
    category.includes('status-cures') ||
    category.includes('revival') ||
    category.includes('poke-balls') ||
    category.includes('stat-boosts') ||
    attributes.includes('usable-in-battle');
    
  const canUseInOverworld =
    category.includes('medicine') ||
    category.includes('healing') ||
    category.includes('status-cures') ||
    category.includes('revival') ||
    category.includes('evolution') ||
    category.includes('vitamins') ||
    attributes.includes('usable-overworld');
  
  return { canUseInBattle, canUseInOverworld };
}

async function fetchAllItems() {
  console.log('Fetching item list from PokeAPI...');
  
  // Fetch all item IDs (limit to first 1000 for practical purposes - covers all main items)
  const itemListResponse = await fetchWithRetry(`${POKEAPI_BASE}/item?limit=1000`);
  const itemUrls = itemListResponse.results.map(r => r.url);
  
  console.log(`Found ${itemUrls.length} items to fetch...`);
  
  const items = {};
  let processed = 0;
  
  for (const url of itemUrls) {
    try {
      const item = await fetchWithRetry(url);
      processed++;
      
      if (processed % 50 === 0) {
        console.log(`Processed ${processed}/${itemUrls.length} items...`);
      }
      
      // Get English name and description
      const nameEntry = item.names?.find(n => n.language.name === 'en');
      const flavorEntry = item.flavor_text_entries?.find(e => e.language.name === 'en');
      const effectEntry = item.effect_entries?.find(e => e.language.name === 'en');
      
      if (!nameEntry) continue; // Skip if no English name
      
      const itemId = item.name;
      const category = categorizeItem(item);
      const { canUseInBattle, canUseInOverworld } = determineUsability(item);
      const isKeyItem = item.category?.name?.includes('key-items') || 
                       item.category?.name?.includes('plot-advancement') ||
                       item.category?.name?.includes('event-items');
      
      items[itemId] = {
        id: itemId,
        name: nameEntry.name,
        description: flavorEntry?.text?.replace(/\n/g, ' ').replace(/\s+/g, ' ').trim() || 
                    effectEntry?.short_effect?.replace(/\n/g, ' ').replace(/\s+/g, ' ').trim() || 
                    'No description available.',
        effect: effectEntry?.effect?.replace(/\n/g, ' ').replace(/\s+/g, ' ').trim() || 
               effectEntry?.short_effect?.replace(/\n/g, ' ').replace(/\s+/g, ' ').trim() || 
               'No effect information.',
        category: category,
        cost: item.cost || 0,
        sprite: item.sprites?.default || null,
        canUseInBattle: canUseInBattle,
        canUseInOverworld: canUseInOverworld,
        isKeyItem: isKeyItem,
        flingPower: item.fling_power || null,
        flingEffect: item.fling_effect?.name || null,
        attributes: item.attributes?.map(a => a.name) || []
      };
      
      // Small delay to avoid rate limiting
      await delay(50);
      
    } catch (error) {
      console.error(`Error fetching item ${url}:`, error.message);
    }
  }
  
  console.log(`\nSuccessfully processed ${Object.keys(items).length} items!`);
  
  // Write to file
  const outputDir = path.dirname(OUTPUT_FILE);
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
  
  fs.writeFileSync(OUTPUT_FILE, JSON.stringify(items, null, 2));
  console.log(`\nItems database written to: ${OUTPUT_FILE}`);
  console.log(`Total items: ${Object.keys(items).length}`);
  
  // Print category breakdown
  const categoryCount = {};
  Object.values(items).forEach(item => {
    categoryCount[item.category] = (categoryCount[item.category] || 0) + 1;
  });
  
  console.log('\nCategory breakdown:');
  Object.entries(categoryCount).forEach(([cat, count]) => {
    console.log(`  ${cat}: ${count} items`);
  });
}

// Run the script
fetchAllItems().catch(console.error);
