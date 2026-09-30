import { createRequire } from 'node:module';
import path from 'node:path';
const require = createRequire('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json');
const sharp = require('sharp');
const directory = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1'));
const tiles = await Promise.all([1, 2, 3, 4].map(async (n, i) => ({
  input: await sharp(path.join(directory, `panel-${n}.png`)).resize(960, 540).png().toBuffer(),
  left: 16 + (i % 2) * 976,
  top: 16 + Math.floor(i / 2) * 556,
})));
await sharp({ create: { width: 1968, height: 1128, channels: 3, background: '#e6d8f2' } })
  .composite(tiles).png().toFile(path.join(directory, 'four-panels-preview.png'));
