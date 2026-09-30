import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const shapes={
 'ui-rounded.png':'<rect x="2" y="2" width="124" height="124" rx="30" fill="white"/>',
 'ui-circle.png':'<circle cx="64" cy="64" r="62" fill="white"/>',
 'ui-check.png':'<path d="M28 66L53 90L101 37" fill="none" stroke="white" stroke-width="14" stroke-linecap="round" stroke-linejoin="round"/>',
 'ui-outline.png':'<rect x="4" y="4" width="120" height="120" rx="28" fill="none" stroke="white" stroke-width="8"/>'
};
for(const [name,shape] of Object.entries(shapes))await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128">${shape}</svg>`)).png().toFile('Assets/Scenes/ui/Onboarding/'+name);
