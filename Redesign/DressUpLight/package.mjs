import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',kit='Assets/_Game/UI/LightTheme';
const assets=JSON.parse(await fs.readFile(root+'/assets.json','utf8'));
const palette=JSON.parse(await fs.readFile(root+'/palette.json','utf8'));
const sprites=assets.filter(a=>a.name!=='courtyard-background');
const columns=6,cellW=190,cellH=142,rows=Math.ceil(sprites.length/columns),layers=[];
for(let i=0;i<sprites.length;i++){
 const item=sprites[i],x=i%columns*cellW,y=Math.floor(i/columns)*cellH;
 layers.push({input:await sharp(item.file).resize(128,94,{fit:'contain',background:'#F3EEFF'}).png().toBuffer(),left:x+31,top:y+5});
 const label=`<svg width="190" height="38"><text x="95" y="16" text-anchor="middle" font-family="Arial" font-size="12" fill="#35284F">${item.name}</text><text x="95" y="32" text-anchor="middle" font-family="Arial" font-size="11" fill="#84749F">${item.width} × ${item.height} · ${Math.ceil(item.bytes/1024)} KB${item.border?' · 9-slice':''}</text></svg>`;
 layers.push({input:Buffer.from(label),left:x,top:y+101});
}
await sharp({create:{width:columns*cellW,height:rows*cellH,channels:4,background:'#F3EEFF'}}).composite(layers).png().toFile(root+'/asset-sheet.png');
const theme={name:'Gacha Dress-Up Light',palette,referenceResolution:[1920,1080],font:{family:'Unity LegacyRuntime / LiberationSans',color:palette.ink,headings:38,labels:25},layout:{characterPanel:{anchors:[0.01,0.16,0.19,0.98]},customizePanel:{anchors:[0.71,0.16,0.99,0.98]},navigation:{anchors:[0,0,1,0.14]}},textures:{icons:64,panels:128,phone:[320,576],background:[1024,576],mipmaps:false,readable:false,android:'ASTC 6x6',nineSliceBorder:28},labels:['Characters','Dress Up','Customize','Face','Clothes','Hair','Hat','Pants','Props','Color','View','Studio','Background','Settings'],assets:assets.map(({guid,...a})=>a)};
await fs.writeFile(kit+'/theme.json',JSON.stringify(theme,null,2));
await fs.writeFile(kit+'/README.txt',`GACHA DRESS-UP LIGHT UI KIT\n\n${assets.length} individual assets, ${Math.round(assets.reduce((n,a)=>n+a.bytes,0)/1024)} KB of image files in total.\n\nIcons: 64 x 64, transparent. Panels: 128 x 128, border 28; use Image.Type = Sliced. Phone: 320 x 576, use Simple. Courtyard: 1024 x 576 JPG, use Simple. No mipmaps; not CPU-readable; ASTC 6x6 on Android.\n\nKeep labels as Unity Text or TMP rather than enlarging text baked into images. Category and nav combined sprites are compatibility versions for this project's existing UI; separate icon assets and panel textures are supplied for fully editable UI.\n\nCharacter portraits, outfits, and hair thumbnails should come from the game's existing character sprites. They are gameplay content, not static UI assets, and were not replaced with the example character.\n\nThe current scene and prefabs are themed while preserving original texture GUIDs and button events. High resolution source SVGs, original texture backups, replacement-map.json, the design reference, background master, and preview are under Redesign/DressUpLight, outside Assets.\n\nIf using the kit elsewhere, import GachaDressUpLight.unitypackage or copy this folder, read theme.json, and place labels separately.\n`);
console.log(JSON.stringify({assets:assets.length,imageBytes:assets.reduce((n,a)=>n+a.bytes,0),sheet:root+'/asset-sheet.png'}));
