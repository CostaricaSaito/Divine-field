import re
from pathlib import Path

CARD_DATA_GUID = "adbad1f68826ca9438ce5c7cfd5854ba"
root = Path(r"c:\Users\コスタリカ斎藤\Divine2\Assets\Resources\Cards")
count = 0
for path in root.rglob("*.asset"):
    text = path.read_text(encoding="utf-8")
    if CARD_DATA_GUID not in text:
        continue
    if re.search(r"^\s*rarity:", text, re.M):
        continue
    if "isRare: 1" in text:
        text = text.replace("isRare: 1", "rarity: 3")
    elif "isRare: 0" in text:
        text = text.replace("isRare: 0", "rarity: 0")
    else:
        continue
    path.write_text(text, encoding="utf-8")
    count += 1
    print(path.name)
print(f"Migrated: {count}")
