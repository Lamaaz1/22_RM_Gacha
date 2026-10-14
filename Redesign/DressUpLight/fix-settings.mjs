import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',kit='Assets/_Game/UI/LightTheme';
const points=[];
for(let tooth=0;tooth<8;tooth++)
 for(const [offset,radius] of [[-22.5,21],[-14,21],[-10,27],[10,27],[14,21],[22.5,21]]){
  const angle=(-90+tooth*45+offset)*Math.PI/180;
  points.push(`${(32+Math.cos(angle)*radius).toFixed(3)} ${(32+Math.sin(angle)*radius).toFixed(3)}`);
 }
const gear=`<path d="M${points.join('L')}Z"/><circle cx="32" cy="32" r="10"/>`;
const old='<path d="M25 7L23 15L16 19L8 17L4 27L11 32L12 40L6 47L14 55L21 50L29 52L33 60L44 56L43 48L49 42L58 42L60 31L52 27L49 19L52 11L42 6L36 13L29 13Z"/><circle cx="32" cy="33" r="10"/>';
const manifest=JSON.parse(await fs.readFile(root+'/assets.json','utf8'));
for(const name of ['icon-settings','nav-settings']){
 const vector=root+'/vectors/'+name+'.svg';
 const svg=(await fs.readFile(vector,'utf8')).replace(old,gear);
 await fs.writeFile(vector,svg);
 const item=manifest.find(a=>a.name===name);
 await sharp(Buffer.from(svg),{density:144}).resize(item.width,item.height).png({compressionLevel:9,palette:true,colours:256,dither:0.2}).toFile(item.file);
 item.bytes=(await fs.stat(item.file)).size;
}
await fs.copyFile(kit+'/nav-settings.png','Assets/drive-download-20251022T085704Z-1-001/Asset 25.png');
await fs.writeFile(root+'/assets.json',JSON.stringify(manifest,null,2));
console.log('Corrected the eight symmetrical teeth in both settings sprites and the existing game texture.');
