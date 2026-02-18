# BaeSenX

Tool set that allows for certain procedures in BISHOP released games, using their engine called BSXX internally. At the moment is still a work in progress tool, since the inital scope of this project was to just release a script recompiler and decompiler, but considering the length of this task given the nature of the engine, a WIP tool was released to let users know of the current progress and if possible, get some PRs.

## Functions
### Savegame: Update its checksum
To ensure that a savegame won't be used in a game not designed for it, BISHOP includes a 32 byte header for a checksum. During boot time, the game calculates the **MD5 checksum value of the opcode region of the loaded bsxx.dat file**, and then compares it with the value given at the savegame. If both values do not coincide, the game automatically removes the save as it deems it not compatible.

#### ❔ What's the point of this feature?
ℹ️ At this moment, not much, since there is not a single script recompiler for BISHOP games in existence. This was merely created to avoid having to replay the entire game to test any script changes.

#### ❔ Is this not compatible with anything?
ℹ️ This is compatible from all games starting from version 3.0 up to 3.3, the currently known latest version of their script. Granted this system is not inherently based on how their scripts work, but this is a system that they have not touched since decades ago.

### Script: Decompile
To make the script editable, a complete decompilation is necessary. As it stands, one can extract the entire game script onto a JSON format, with each opcode separated. The problem comes from filling each opcode with the accurate metadata obtained from the rest of the script, and worse, most of the opcodes are still not understood.
Keep in mind that their script pretty much deals with really low-level stuff, so even while having a complete decompilation, making any complex edits would prove to be difficult.

#### ❔ Is this not compatible with anything?
ℹ️ The byte length of all opcodes seems to stay the same from version 3.0 to 3.3. The problem comes with the metadata inside each instruction, which obviously there are some discrepancies between each version. Currently the game used to study the game engine is [Kutsujoku](https://vndb.org/v21769), which uses version 3.2.

#### ❔ How is the game file structured?
ℹ️ The game is divided into 3 sections: Magic signature, the opcodes and sizes for each of its lists, and each list itself.
