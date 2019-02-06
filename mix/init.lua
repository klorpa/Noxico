print("Running init scripts...");
dofile("i18n.lua");
dofile("stats.lua");
dofile("defense.lua");
dofile("teambehavior.lua");

function PickOne(list)
	return list[math.random(#list)]
end

function GetWorldName()

	local x = {
		"Acid", "Advertisement", "Age", "Alloy", "Alternation", "Ambiance", "Amusement", "Angels", "Annoyance", "Apathy", "Art", "Ascent", "Ash", "Atoll", "Autumn",
		"Backstabbing", "Bamboo", "Bastille", "Beauty", "Blankness", "Blaze", "Boil", "Bone", "Books", "Braille", "Brains", "Brick", "Bridges", "Bronze", "Brooks", "Bubbles", "Bulge", "Burlap", "Butterflies",
		"Cacophony", "Cacti", "Cages", "Canopy", "Canyons", "Carbon", "Carpet", "Cathedrals", "Caves", "Chains", "Change", "Charge", "Chemicals", "Chocolate", "Circuitry", "Clay", "Cliffs", "Clockwork", "Clouds", "Cobalt", "Cobblestone", "Concrete", "Construction", "Contact", "Contrast", "Copper", "Coral", "Cotton", "Crossroads", "Crypts", "Crystal", "Cubes", "Curses",
		"Dance", "Dawn", "Death", "Decor", "Depth", "Descent", "Desolation", "Dew", "Diamonds", "Dirt", "Disease", "Dismay", "Dolls", "Dragons", "Drought", "Dungeons", "Dust", "Dye",
		"Ebony", "Echo", "Eldritch", "Electronics", "Elegance", "Emeralds", "Evergreens",
		"Falsehood", "Fatigue", "Fear", "Festivities", "Fire", "Flame", "Flow", "Flowers", "Fog", "Forest", "Fortification", "Fossils", "Freefall", "Frost", "Fungi", "Fur",
		"Gardens", "Germs", "Ghosts", "Glamour", "Glass", "Glitter", "Gloom", "Glow", "Glue", "Gold", "Graphite", "Grass", "Graves", "Gravity", "Gust",
		"Hail", "Hallucinations", "Harbors", "Hats", "Hay", "Haze", "Heat", "Hedges", "Helium", "Henge", "Hills", "Holes", "Holly", "Horror",
		"Ice", "Illusion", "Ink", "Insanity", "Insomnia", "Intimidation", "Iron", "Irradiation", "Irrigation", "Islands", "Ivory",
		"Jail", "Jazz", "Jokers", "Jolliness", "Jungle", "Junk", "Juxtaposition",
		"Karaoke", "Karate", "Knowledge", "Krypton",
		"Labyrinth", "Ladders", "Lakes", "Lasers", "Laugh", "Law", "Laze", "Leaves", "Levitation", "Light", "Loam", "Luck",
		"Magma", "Magnets", "Maps", "Marsh", "Meadow", "Melody", "Mercury", "Mirrors", "Mirth", "Misers", "Mist", "Moss", "Motion", "Mountains", "Mushrooms",
		"Nails", "Neon", "Nickel", "Night", "Noir", "Noise",
		"Oasis", "Obsidian", "Odors", "Oil", "Opposites", "Orchard", "Ore",
		"Paper", "Paths", "Pipes", "Pistons", "Plains", "Plastic", "Platforms", "Pluck", "Pollution", "Portals", "Powder", "Prairie", "Precipice", "Presence", "Presents", "Prisms", "Pulse", "Pumpkins", "Pyramids",
		"Quakes", "Quarry", "Quartz",
		"Rain", "Rainbows", "Ramps", "Rays", "Rebirth", "Reflection", "Regret", "Repetition", "Reversal", "Rhythm", "Rime", "Rip", "Rivers", "Rock", "Roses", "Roses", "Rot", "Rubber", "Rust",
		"Sadness", "Sand", "Savannah", "Scales", "Science", "Sentience", "Shade", "Shadow", "Shattering", "Shores", "Shrines", "Shrouds", "Silence", "Silhouette", "Silk", "Silt", "Silver", "Singe", "Sketch", "Skin", "Sky", "Slate", "Slime", "Sluice", "Slumber", "Snakes", "Snow", "Solace", "Song", "Spheres", "Spices", "Spikes", "Spires", "Sponge", "Springs", "Stability", "Stairs", "Static", "Steam", "Steel", "Steppe", "Storm", "Strings", "Stumps", "Subway", "Suction", "Sulfur", "Surprise", "Swamp", "Sweets",
		"Tea", "Teeth", "Temples", "Tents", "Terror", "Thorns", "Thought", "Thunder", "Tin", "Tinsel", "Tombs", "Topiary", "Trance", "Tranquility", "Transit", "Traps", "Travel", "Treasure", "Trove", "Tundra", "Tunnel", "Turmoil", "Twilight",
		"Ultraviolence", "Undead", "Urns",
		"Vacuum", "Vapor", "Variety", "Veil", "Velvet", "Vibration", "Vines",
		"Walls", "War", "Warmth", "Wasteland", "Water", "Waterfalls", "Waves", "Wax", "Wheat", "Wilderness", "Wind", "Windows", "Womb", "Wood", "Wrath",
		"Xenon",
		"Yarn", "Yore",
		"Zen", "Zephyr"
	}
	local a = PickOne(x)
	local b = PickOne(x)
	while (b == a) do
		b = PickOne(x)
	end
	return "Land of " .. a .. " and " .. b
end