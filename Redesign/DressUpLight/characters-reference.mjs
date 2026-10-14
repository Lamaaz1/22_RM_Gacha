import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',kit='Assets/_Game/UI/LightTheme';
const manifest=JSON.parse(await fs.readFile(root+'/assets.json','utf8'));
async function emit(name,w,h,body,border=0){
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><defs><linearGradient id="paper" x2="0" y2="1"><stop stop-color="#FCF8FF"/><stop offset="1" stop-color="#F1E8FF"/></linearGradient><linearGradient id="tile" x2="0" y2="1"><stop stop-color="#EEE4FB"/><stop offset="1" stop-color="#DED1EF"/></linearGradient></defs>${body}</svg>`;
 const file=kit+'/'+name+'.png';await fs.writeFile(root+'/vectors/'+name+'.svg',svg);
 await sharp(Buffer.from(svg),{density:144}).resize(w,h).png({compressionLevel:9,palette:true,colours:256,dither:.2}).toFile(file);
 let guid;try{guid=(await fs.readFile(file+'.meta','utf8')).match(/^guid: (\w+)/m)[1];}catch{
  guid=crypto.randomBytes(16).toString('hex');
  let meta=await fs.readFile(kit+'/panel.png.meta','utf8');
  meta=meta.replace(/^guid: \w+/m,'guid: '+guid).replace(/spriteBorder: \{[^}]*\}/,`spriteBorder: {x: ${border}, y: ${border}, z: ${border}, w: ${border}}`).replace(/spriteID: \w+/,'spriteID: '+crypto.randomBytes(16).toString('hex'));
  await fs.writeFile(file+'.meta',meta);
 }
 const item={name,file,width:w,height:h,border,bytes:(await fs.stat(file)).size,guid};
 const index=manifest.findIndex(a=>a.name===name);if(index<0)manifest.push(item);else manifest[index]=item;
}
await emit('characters-panel',128,128,'<rect x="2" y="4" width="124" height="122" rx="22" fill="#B9A3DF"/><rect x="2" y="2" width="124" height="122" rx="22" fill="url(#paper)" stroke="#C3ACEA" stroke-width="2"/><rect x="6" y="6" width="116" height="114" rx="18" fill="none" stroke="#FFFFFF" stroke-width="2"/><rect x="10" y="10" width="108" height="106" rx="15" fill="none" stroke="#E1D2F5" stroke-width="1"/>',28);
await emit('character-tile',128,128,'<rect x="2" y="3" width="124" height="123" rx="22" fill="url(#tile)" stroke="#C9B8E8" stroke-width="2"/><rect x="5" y="5" width="118" height="117" rx="19" fill="none" stroke="#FFFFFF" stroke-width="2"/>',28);
await emit('locked-avatar',96,112,'<ellipse cx="48" cy="43" rx="25" ry="33" fill="#B0A0C9"/><path d="M21 108Q20 72 48 72Q76 72 75 108Z" fill="#B0A0C9"/>');
await emit('character-lock',64,64,'<path d="M20 29V21C20 5 44 5 44 21V29" fill="none" stroke="#F5EEFF" stroke-width="10"/><path d="M20 29V21C20 5 44 5 44 21V29" fill="none" stroke="#8270A8" stroke-width="5"/><rect x="12" y="27" width="40" height="30" rx="7" fill="#8572AD" stroke="#F5EEFF" stroke-width="3"/><circle cx="32" cy="39" r="4" fill="#FFFFFF"/><path d="M32 41V47" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round"/>');
await emit('character-selection-badge',32,32,'<g fill="none" stroke-linecap="round"><path d="M11 22L13 7M18 24L26 11M22 29L30 24" stroke="#FFFFFF" stroke-width="9"/><path d="M11 22L13 7M18 24L26 11M22 29L30 24" stroke="#E897D4" stroke-width="5"/></g>');
await fs.writeFile(root+'/assets.json',JSON.stringify(manifest,null,2));
const script='Assets/_Game/Scripts/LightCharacterPanel.cs';
try{await fs.access(script+'.meta');}catch{
 const template=await fs.readFile('Assets/_Game/Scripts/ColorPicker.cs.meta','utf8');
 await fs.writeFile(script+'.meta',template.replace(/^guid: \w+/m,'guid: '+crypto.randomBytes(16).toString('hex')));
}
console.log('Character panel sprites and stable Unity metadata ready.');
