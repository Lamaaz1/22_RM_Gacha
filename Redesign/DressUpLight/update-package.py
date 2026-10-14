import io
import re
import tarfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
kit = root / 'Assets/_Game/UI/LightTheme'
package = root / 'Redesign/DressUpLight/GachaDressUpLight.unitypackage'
replacements = {}
for name in ('icon-settings.png', 'nav-settings.png', 'README.txt', 'theme.json'):
    asset = kit / name
    guid = re.search(r'^guid: (\w+)', Path(str(asset) + '.meta').read_text(), re.M)[1]
    replacements[guid + '/asset'] = asset.read_bytes()
    if name.endswith('.png'):
        replacements[guid + '/preview.png'] = asset.read_bytes()

entries = []
with tarfile.open(package, 'r:gz') as source:
    for member in source:
        data = source.extractfile(member).read() if member.isfile() else None
        key = member.name.lstrip('./')
        if key in replacements:
            data = replacements[key]
            member.size = len(data)
        entries.append((member, data))
with tarfile.open(package, 'w:gz') as target:
    for member, data in entries:
        target.addfile(member, io.BytesIO(data) if data is not None else None)
print('Updated the Unity import package with the corrected settings sprites.')
