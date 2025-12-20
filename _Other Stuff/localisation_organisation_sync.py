# Sync organisation of localisation between languages (based on en-us.cfg) to arrange entries similarly and list mismatching entries at the end.
# Note: This overwrites the other localisation files.
#
# Run this from the BDArmory/BDArmory folder as: "python3 ../_Other\ Stuff/localisation_organisation_sync.py"
# Then edit the other localisation files for entries that have values of ??? (and are commented out) or unknown keys (at the end).

from pathlib import Path

# Get dictionaries of LOC keys with line numbers, localisations, indentation and other (comments/structure).
locPath = Path('Distribution/GameData/BDArmory/Localization/UI')
with open(locPath / "en-us.cfg", 'r') as f:
  en = [l.strip('\n') for l in f.readlines()]
enkv = {line: (key.strip(), loc.strip()) for key, loc, line in ((*l.split('=', 1), i) for i, l in enumerate(l.strip() for l in en) if l.startswith("#LOC"))}
enk = {key for _, (key, _) in enkv.items()}
other = {line: text for line, text in enumerate(l.strip() for l in en) if not text.startswith("#LOC")}
indents = {line: len(text) - len(text.lstrip()) for line, text in enumerate(en)}
lang_id_line = next(i for i, v in other.items() if v.strip() == "en-us")

for lang in ("de-de", "ja", "ru", "zh-cn"):
  file = f"{lang}.cfg"
  with open(locPath / file, 'r') as f:
    l = [l.strip('\n') for l in f.readlines()]
  kv = {key.strip(): loc.strip() for key, loc in (l.split('=', 1) for l in (l.strip() for l in l) if l.startswith("#LOC"))}
  new = []
  for line in range(len(en)):
    if line == lang_id_line:
      new.append(f"{' ' * indents[line]}{lang}")
    elif line in other:  # line is either in other or enkv.
      new.append(f"{' ' * indents[line]}{other[line]}")
    elif enkv[line][0] in kv:
      new.append(f"{' ' * indents[line]}{enkv[line][0]} = {kv[enkv[line][0]]}")
    else:
      new.append(f"{' ' * indents[line]}//{enkv[line][0]} = ???   {enkv[line][1]}")
  unknown = [f"{' ' * 4}//{key} = {kv[key]}" for key in kv if key not in enk]
  index = next((i for i, text in enumerate(l) if text == '// Unknown keys:'), -1)  # Copy old unknown keys too.
  if index > -1:
    unknown.extend(l[index + 1:])
  if len(unknown) > 0:
    new.append("// Unknown keys:")
    new.extend(unknown)

  with open(locPath / file, 'w') as f:
    f.writelines(l + '\n' for l in new)

# Do the same for the weapon localisation files in Distribution/GameData/BDArmory/Localization
locPath = Path('Distribution/GameData/BDArmory/Localization')
with open(locPath / "localization-en-us.cfg", 'r') as f:
  en = [l.strip('\n') for l in f.readlines()]
enkv = {line: (key.strip(), loc.strip()) for key, loc, line in ((*l.split('=', 1), i) for i, l in enumerate(l.strip() for l in en) if l.startswith("#loc"))}
enk = {key for _, (key, _) in enkv.items()}
other = {line: text for line, text in enumerate(l.strip() for l in en) if not text.startswith("#loc")}
indents = {line: len(text) - len(text.lstrip()) for line, text in enumerate(en)}
lang_id_line = next(i for i, v in other.items() if v.strip() == "en-us")

for lang in ("de-de", "ja", "ru", "zh-cn"):
  file = f"localization-{lang}.cfg"
  with open(locPath / file, 'r') as f:
    l = [l.strip('\n') for l in f.readlines()]
  kv = {key.strip(): loc.strip() for key, loc in (l.split('=', 1) for l in (l.strip() for l in l) if l.startswith("#loc"))}
  new = []
  for line in range(len(en)):
    if line == lang_id_line:
      new.append(f"{'\t' * indents[line]}{lang}")
    elif line in other:  # line is either in other or enkv.
      new.append(f"{'\t' * indents[line]}{other[line]}")
    elif enkv[line][0] in kv:
      new.append(f"{'\t' * indents[line]}{enkv[line][0]} = {kv[enkv[line][0]]}")
    else:
      new.append(f"{'\t' * indents[line]}//{enkv[line][0]} = ???   {enkv[line][1]}")
  unknown = [f"{'\t' * 4}//{key} = {kv[key]}" for key in kv if key not in enk]
  index = next((i for i, text in enumerate(l) if text == '// Unknown keys:'), -1)  # Copy old unknown keys too.
  if index > -1:
    unknown.extend(l[index + 1:])
  if len(unknown) > 0:
    new.append("// Unknown keys:")
    new.extend(unknown)

  with open(locPath / file, 'w') as f:
    f.writelines(l + '\n' for l in new)