import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/Version3';
const report=JSON.parse(await fs.readFile(root+'/validation.json','utf8'));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const row of report.assets){const b=await fs.readFile('Assets/Scenes/ui/'+row.file);if(sha(b)!==row.sha256)throw Error('Installed image changed: '+row.file);}
const result=await fs.readFile(root+'/validation.txt','utf8');if(!result.startsWith('PASS:'))throw Error('Unity did not validate');
report.unityPreviewVerified=true;
report.unityValidationScope='Isolated Unity 6000.0.64f1 editor: actual onboarding prefab, controller and sprites; selection gating, Next/Back, saved preferences, locked activities and single completion.';
report.unityPlayModeTested=false;
report.previewResolution='1920x1080';
await fs.writeFile(root+'/validation.json',JSON.stringify(report,null,2));
const tiles=await Promise.all([1,2,3,4].map(async(n,i)=>({input:await sharp(`${root}/panel-${n}.png`).resize(960,540).png().toBuffer(),left:16+i%2*976,top:16+Math.floor(i/2)*556})));
await sharp({create:{width:1968,height:1128,channels:3,background:'#d3e9eb'}}).composite(tiles).png().toFile(root+'/four-panels-preview.png');
console.log(JSON.stringify({images:report.images,unityPreviewVerified:true,nonImageFilesUnchanged:report.nonImageFilesUnchanged}));
