import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight', out='Assets/_Game/UI/LightTheme';
await fs.mkdir(out,{recursive:true});await fs.mkdir(root+'/vectors',{recursive:true});await fs.mkdir(root+'/originals',{recursive:true});
const palette={background:'#F3EEFF',panel:'#F6F0FF',surface:'#EAE0FA',border:'#CBB9EE',violet:'#8B6BE8',pink:'#F0A6CA',mint:'#A8DCCB',ink:'#35284F',muted:'#84749F',white:'#FFFFFF'};
const defs=`<defs><linearGradient id="panel" x2="0" y2="1"><stop stop-color="#FCF9FF"/><stop offset="1" stop-color="#EEE5FC"/></linearGradient><linearGradient id="button" x2="0" y2="1"><stop stop-color="#F6EFFF"/><stop offset="1" stop-color="#E7DBF7"/></linearGradient><linearGradient id="selected" x2="0" y2="1"><stop stop-color="#A68AF3"/><stop offset="1" stop-color="#8561DE"/></linearGradient></defs>`;
const svg=(w,h,b)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${b}</svg>`;
const rr=(x,y,w,h,r,fill,stroke='none',sw=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const txt=(t,x,y,s=20,c=palette.ink)=>`<text x="${x}" y="${y}" text-anchor="middle" dominant-baseline="central" font-family="Arial, sans-serif" font-weight="700" font-size="${s}" fill="${c}">${t}</text>`;
const star=(x,y,s,c=palette.violet)=>`<path d="M${x} ${y-s}Q${x+s*.2} ${y-s*.2} ${x+s} ${y}Q${x+s*.2} ${y+s*.2} ${x} ${y+s}Q${x-s*.2} ${y+s*.2} ${x-s} ${y}Q${x-s*.2} ${y-s*.2} ${x} ${y-s}Z" fill="${c}"/>`;
function surface(w,h,selected=false,kind='button'){
 const r=Math.min(24,w*.2,h*.2),fill=selected?'url(#selected)':kind==='panel'?'url(#panel)':'url(#button)';
 return rr(2,4,w-4,h-6,r,'#BDA4E7')+rr(2,2,w-4,h-6,r,fill,selected?'#AC8CF7':palette.border,2)+rr(6,6,w-12,h-14,r-4,'none','#FFFFFF',1.5);
}
function gearOutline(){
 const points=[];
 for(let tooth=0;tooth<8;tooth++)
  for(const [offset,radius] of [[-22.5,21],[-14,21],[-10,27],[10,27],[14,21],[22.5,21]]){
   const angle=(-90+tooth*45+offset)*Math.PI/180;
   points.push(`${(32+Math.cos(angle)*radius).toFixed(3)} ${(32+Math.sin(angle)*radius).toFixed(3)}`);
  }
 return `<path d="M${points.join('L')}Z"/><circle cx="32" cy="32" r="10"/>`;
}
const paths={
 face:'<path d="M19 39C12 37 12 30 18 29C17 10 47 10 46 29C53 30 52 37 45 39C41 53 22 53 19 39Z"/><circle cx="25" cy="33" r="1.7" fill="currentColor"/><circle cx="39" cy="33" r="1.7" fill="currentColor"/>',
 clothes:'<path d="M24 12L13 16L6 27L16 34L22 27V53H42V27L48 34L58 27L51 16L40 12Q32 23 24 12Z"/>',
 hair:'<path d="M16 44Q7 39 13 26Q14 9 32 9Q50 9 51 26Q58 40 48 46M16 45Q16 27 23 20Q24 33 32 34Q30 25 34 20Q38 31 48 32Q49 48 43 53M18 47Q32 57 44 47"/>',
 hat:'<path d="M9 39Q9 16 31 16Q49 16 49 37L57 46Q28 56 8 44ZM11 38Q29 44 48 37M29 17Q19 26 21 41M39 19Q46 28 44 38"/>',
 pants:'<path d="M17 11H47L51 54H35L32 31L29 54H13ZM18 20H46M32 12V24"/>',
 props:'<path d="M34 31L12 53Q9 56 6 52L28 28M36 7L41 18L53 20L44 29L46 42L35 36L23 42L25 29L16 20L29 18Z"/>',
 view:'<path d="M4 32Q17 13 32 13Q47 13 60 32Q47 51 32 51Q17 51 4 32Z"/><circle cx="32" cy="32" r="9"/>',
 studio:'<path d="M10 24H54V54H10Z M10 24L7 13L49 4L52 15Z M16 12L24 20M29 9L37 17M42 6L50 14"/><path d="M26 32L41 40L26 48Z" fill="currentColor" stroke="none"/>',
 background:'<rect x="10" y="10" width="44" height="44" rx="5"/><circle cx="24" cy="24" r="4"/><path d="M13 48L27 34L35 41L44 29L53 40"/>',
 settings:gearOutline(),
 lock:'<path d="M19 29V21C19 4 45 4 45 21V29"/><rect x="12" y="28" width="40" height="30" rx="8" fill="currentColor" stroke="none"/><circle cx="32" cy="40" r="4" fill="#FFFFFF" stroke="none"/><path d="M32 42V48" stroke="#FFFFFF"/>',
 close:'<path d="M15 15L49 49M49 15L15 49"/>',
 check:'<path d="M9 32L25 47L55 15"/>',
 next:'<path d="M23 12L43 32L23 52"/>',
 previous:'<path d="M41 12L21 32L41 52"/>',
 undo:'<path d="M26 12L10 27L26 41M11 27H39Q58 27 54 44Q50 54 37 51"/>',
 redo:'<path d="M38 12L54 27L38 41M53 27H25Q6 27 10 44Q14 54 27 51"/>',
 camera:'<path d="M9 20H21L25 12H40L44 20H55V51H9Z"/><circle cx="32" cy="35" r="10"/>',
 wifi:'<path d="M7 20Q32 1 57 20M15 30Q32 16 49 30M24 40Q32 33 40 40"/><circle cx="32" cy="49" r="3" fill="currentColor" stroke="none"/>',
 battery:'<rect x="7" y="20" width="47" height="25" rx="4"/><path d="M58 27V38"/><rect x="13" y="25" width="33" height="15" rx="1" fill="currentColor" stroke="none"/>',
 noads:'<rect x="11" y="15" width="42" height="34" rx="6"/><path d="M8 56L57 8"/>',
 color:'<circle cx="32" cy="32" r="21" stroke="#B18CEF" stroke-width="13"/><path d="M32 11A21 21 0 0 1 53 32" stroke="#F0A6CA" stroke-width="13"/><path d="M53 32A21 21 0 0 1 32 53" stroke="#F8D899" stroke-width="13"/><path d="M32 53A21 21 0 0 1 11 32" stroke="#A8DCCB" stroke-width="13"/>'
};
const icon=(name,x=0,y=0,size=64,color=palette.ink)=>`<g transform="translate(${x} ${y}) scale(${size/64})" fill="none" stroke="${color}" color="${color}" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round">${paths[name]}</g>`;
let manifest=[];
async function emit(name,w,h,body,border=0){
 const source=svg(w,h,body),file=out+'/'+name+'.png';
 await fs.writeFile(root+'/vectors/'+name+'.svg',source);
 await sharp(Buffer.from(source),{density:144}).resize(w,h).png({compressionLevel:9,palette:true,colours:256,dither:0.2}).toFile(file);
 let guid;try{guid=(await fs.readFile(file+'.meta','utf8')).match(/^guid: (\w+)/m)[1];}catch{guid=crypto.randomBytes(16).toString('hex');await fs.writeFile(file+'.meta',meta(guid,w,h,border));}
 manifest.push({name,file,width:w,height:h,border,bytes:(await fs.stat(file)).size,guid});return file;
}
function meta(guid,w,h,border=0){return `fileFormatVersion: 2\nguid: ${guid}\nTextureImporter:\n  serializedVersion: 13\n  internalIDToNameTable: []\n  externalObjects: {}\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  textureFormat: 1\n  maxTextureSize: ${Math.max(64,2**Math.ceil(Math.log2(Math.max(w,h))))}\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  spriteMode: 1\n  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  spriteBorder: {x: ${border}, y: ${border}, z: ${border}, w: ${border}}\n  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  compressionQuality: 70\n  platformSettings:\n  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: ${Math.max(64,2**Math.ceil(Math.log2(Math.max(w,h))))}\n    textureFormat: -1\n    textureCompression: 1\n    compressionQuality: 70\n    overridden: 0\n  - serializedVersion: 3\n    buildTarget: Android\n    maxTextureSize: ${Math.max(64,2**Math.ceil(Math.log2(Math.max(w,h))))}\n    textureFormat: 50\n    textureCompression: 1\n    compressionQuality: 70\n    overridden: 1\n  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n    spriteID: ${crypto.randomBytes(16).toString('hex')}\n    internalID: 0\n    vertices: []\n    indices:\n    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable: {}\n  userData: GachaLightTheme\n  assetBundleName:\n  assetBundleVariant:\n`;}
for(const [name,selected,kind] of [['panel',false,'panel'],['button',false,'button'],['button-selected',true,'button'],['character-tile',false,'button'],['character-tile-selected',true,'button'],['navigation',false,'panel'],['wardrobe-tile',false,'button']])await emit(name,128,128,surface(128,128,selected,kind),28);
await emit('divider',128,16,`<path d="M2 8H52M76 8H126" stroke="${palette.border}" stroke-width="2"/>${star(64,8,7)}`);
await emit('sparkle',32,32,star(16,16,14));
await emit('heart',32,32,'<path d="M16 28C12 24 3 18 3 10C3 2 13 2 16 9C19 2 29 2 29 10C29 18 20 24 16 28Z" fill="#F0A6CA"/>');
await emit('selection-dot',32,32,'<circle cx="16" cy="16" r="13" fill="#F0A6CA" stroke="#FFFFFF" stroke-width="3"/>');
await emit('character-outline',128,128,rr(3,3,122,122,22,'none','#8B6BE8',5)+rr(7,7,114,114,18,'none','#FFFFFF',2),28);
await emit('page-dot',16,16,'<circle cx="8" cy="8" r="5" fill="#B49DE7"/>');
await emit('avatar-silhouette',96,96,'<circle cx="48" cy="36" r="23" fill="#B7A9CF"/><path d="M12 91Q12 58 48 58Q84 58 84 91Z" fill="#B7A9CF"/>');
await emit('character-shadow',128,32,'<ellipse cx="64" cy="16" rx="62" ry="14" fill="#9B83CF" fill-opacity=".28"/>');
await emit('phone-frame',320,576,rr(2,2,316,572,32,'#65518C','#BBA3E8',3)+rr(10,31,300,535,26,'url(#panel)','#DCCBF6',2)+rr(128,13,64,6,3,'#AD96D2')+icon('battery',268,7,18,'#F6EFFF')+icon('wifi',290,7,18,'#F6EFFF'));
await emit('title-plaque',320,80,rr(2,9,316,62,28,'url(#panel)',palette.border,2)+star(32,40,10)+star(288,40,10));
for(const name of Object.keys(paths))await emit('icon-'+name,64,64,icon(name));
const labels={face:'Face',clothes:'Clothes',hair:'Hair',hat:'Hat',pants:'Pants',props:'Props'};
for(const [name,label]of Object.entries(labels))await emit('category-'+name,128,156,surface(128,125)+icon(name,28,18,72)+txt(label,64,140,18));
for(const name of ['view','studio','background','settings','camera','noads'])await emit('nav-'+name,256,128,surface(256,128,name==='view')+icon(name,103,36,50,name==='view'?'#FFFFFF':palette.ink));
await emit('arrow-next-button',96,96,surface(96,96)+icon('next',16,16,64));
await emit('arrow-previous-button',96,96,surface(96,96)+icon('previous',16,16,64));
await emit('close-button',64,64,surface(64,64)+icon('close',10,10,44));
const bgSource='C:/Users/desig/.codex/generated_images/01a13a3b-201d-7360-8894-00b2c8dc7a89/exec-c49d5487-12bf-4354-aa92-4258c0f47c62.png';
await sharp(bgSource).resize(1024,576).jpeg({quality:84,mozjpeg:true}).toFile(out+'/courtyard-background.jpg');
let bgGuid;try{bgGuid=(await fs.readFile(out+'/courtyard-background.jpg.meta','utf8')).match(/^guid: (\w+)/m)[1];}catch{bgGuid=crypto.randomBytes(16).toString('hex');await fs.writeFile(out+'/courtyard-background.jpg.meta',meta(bgGuid,1024,576));}
manifest.push({name:'courtyard-background',file:out+'/courtyard-background.jpg',width:1024,height:576,border:0,bytes:(await fs.stat(out+'/courtyard-background.jpg')).size,guid:bgGuid});
await fs.copyFile(bgSource,root+'/background-master.png');
await fs.copyFile('C:/Users/desig/.codex/generated_images/01a13a3b-201d-7360-8894-00b2c8dc7a89/exec-9e4d7d4e-12a5-40c9-a2bf-cff7b024e1ae.png',root+'/reference.png');
await fs.writeFile(root+'/palette.json',JSON.stringify(palette,null,2));
await fs.writeFile(root+'/assets.json',JSON.stringify(manifest,null,2));
// Compatibly replace the existing single-sprite textures. Keep GUIDs and sprite IDs.
const d1='Assets/drive-download-20251021T154352Z-1-001/',d2='Assets/drive-download-20251022T085704Z-1-001/';
const replacementMap={
 [d1+'Asset 3.png']:'category-hair',[d1+'Asset 4.png']:'category-props',[d1+'Asset 5.png']:'category-pants',
 [d1+'Asset 6.png']:'category-hat',[d1+'Asset 7.png']:'category-clothes',[d1+'Asset 8.png']:'category-face',
 [d1+'Asset 10.png']:'arrow-next-button',[d1+'Asset 11.png']:'arrow-previous-button',[d1+'Asset 12.png']:'close-button',
 [d1+'Asset 15.png']:'icon-next',[d1+'Asset 16.png']:'icon-previous',
 [d1+'Asset 58.png']:'icon-noads',[d1+'Asset 59.png']:'nav-noads',[d1+'gacha club bouton (1).png']:'icon-color',
 [d2+'Asset 22.png']:'nav-studio',[d2+'Asset 23.png']:'nav-background',[d2+'Asset 24.png']:'nav-view',[d2+'Asset 25.png']:'nav-settings',
 [d2+'Asset 26.png']:'navigation',[d2+'Asset 27.png']:'panel',[d2+'Asset 29.png']:'character-tile',[d2+'Asset 30.png']:'panel',[d2+'Asset 31.png']:'navigation',
 [d2+'cadre.png']:'character-outline',[d2+'lock icon.png']:'icon-lock',[d2+'check icon.png']:'icon-check',[d2+'x icon.png']:'icon-close',[d2+'screenshot bouton.png']:'nav-camera',
 [d2+'gacha phone.png']:'phone-frame',[d2+'gacha phone ui.png']:'phone-frame'
};
let replacements=[];
for(const [file,name]of Object.entries(replacementMap)){
 const entry=manifest.find(e=>e.name===name),m=await fs.readFile(file+'.meta','utf8');
 if(!/^  spriteMode: 1$/m.test(m))throw new Error('Expected single sprite: '+file);
 const backup=root+'/originals/'+file.replaceAll('/','__');
 try{await fs.access(backup);}catch{await fs.copyFile(file,backup);await fs.copyFile(file+'.meta',backup+'.meta');}
 const before=await sharp(backup).metadata();
 await fs.copyFile(entry.file,file);
 let updated=m.replace(/spriteBorder: \{[^}]+\}/,`spriteBorder: {x: ${entry.border}, y: ${entry.border}, z: ${entry.border}, w: ${entry.border}}`).replace(/maxTextureSize: \d+/g,`maxTextureSize: ${2**Math.ceil(Math.log2(Math.max(entry.width,entry.height)))}`).replace(/spriteGenerateFallbackPhysicsShape: 1/,'spriteGenerateFallbackPhysicsShape: 0');
 await fs.writeFile(file+'.meta',updated);
 replacements.push({file,asset:name,guid:m.match(/^guid: (\w+)/m)[1],border:entry.border,originalSize:[before.width,before.height],size:[entry.width,entry.height],backup});
}
// The old starfield is reused as a panel texture as well as the stage. Keep it a
// small light panel; redirect only actual Background images to the new scenery.
for(const n of ['Asset 13.png','Asset 14.png','Asset 2.png','Asset 20.png','Asset 21.png']){
 const file=d1+n,m=await fs.readFile(file+'.meta','utf8'),entry=manifest.find(e=>e.name==='panel'),backup=root+'/originals/'+file.replaceAll('/','__');
 try{await fs.access(backup);}catch{await fs.copyFile(file,backup);await fs.copyFile(file+'.meta',backup+'.meta');}
 const before=await sharp(backup).metadata();await fs.copyFile(entry.file,file);
 await fs.writeFile(file+'.meta',m.replace(/spriteBorder: \{[^}]+\}/,'spriteBorder: {x: 28, y: 28, z: 28, w: 28}').replace(/maxTextureSize: \d+/g,'maxTextureSize: 128'));
 replacements.push({file,asset:'panel',guid:m.match(/^guid: (\w+)/m)[1],border:28,originalSize:[before.width,before.height],size:[128,128],backup});
}
await fs.writeFile(root+'/replacement-map.json',JSON.stringify(replacements,null,2));
await import('./refine-reference.mjs');
await import('./characters-reference.mjs');
await import('./customize-masks.mjs');
console.log(JSON.stringify({assets:manifest.length,assetBytes:manifest.reduce((n,e)=>n+e.bytes,0),replacedTextures:replacements.length},null,2));
