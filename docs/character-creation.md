# Character creation

From the launch screen select **Create Footballer** or press **C**. Enter a name, set age, height and weight, and cycle preferred foot, four appearance presets and all fourteen starting positions. Appearance presets change the placeholder head colour on the pitch. F3 cycles position and F4 cycles appearance.

Football and personality have independent point budgets. Each attribute starts at its content minimum; each point buys one level above that minimum. Plus/minus controls respect bounds and available points. Balanced reset (F1 for football, F2 for personality) spends that budget evenly; clear returns that pool to its minimum. Spend both budgets exactly before joining (Enter). Escape cancels without changing the current career. Starred attributes are the selected role's recommendations, never restrictions.

`Assets/FootballRpg/Runtime/Content/Resources/CharacterCreation.json` configures the starting budgets (1200 football, 300 personality) and age range (16–21). Attribute bounds remain content-defined. Physical bounds follow existing player validation: 120–230 cm and 35–160 kg. These are tuning defaults, not promised balance. Personality remains distinct from temporary confidence, morale and fatigue.

Joining creates a career at Harbour Vale FC and opens the authored week calendar. Sunday attendance leads to the fixture with that player. F5/F9 save and load the career. The goalkeeper and bench shortcuts on the original launch screen remain independent sample-match demos.

The creator replaces a home starter with the chosen role where one exists; otherwise it uses an outfield starter. This preserves squad IDs and set-piece references, provides immediate playing time, keeps a goalkeeper in the XI, and supports unconventional formations. No youth assessment is run. The draft serializes explicit stable IDs and values, and career construction copies them into owned runtime state without mutating the prototype definitions. Serializable drafts are persistence-ready data, not a save system.

The physical profile flows through existing `PassActor` snapshots. Height affects aerial reach, heading and goalkeeper chances; weight affects dribbling and goalkeeper mobility. No probability formulas or action unlock rules were changed.

Verification covers every position, first-team entry, profile JSON round-trip, independent/exact budgets, zero/full configured budgets, malformed profiles and arrays, source/runtime ownership, unconventional goalkeeper allocation and an actual heading-probability comparison. Desktop verification covers the creation screen and entry into a match; the existing packaged football smoke suite remains required.
