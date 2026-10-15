import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',out='Assets/_Game/Art/Backgrounds/EmptyFloor';
const manifest=process.argv[2] || 'backgrounds-generation.json';
const sheet=process.argv[3] || 'backgrounds-contact-sheet.png';
const data=JSON.parse(await fs.readFile(root+'/'+manifest,'utf8'));
await fs.mkdir(out,{recursive:true});await fs.mkdir(root+'/backgrounds-originals',{recursive:true});
const tiles=[];
for(let i=0;i<data.assets.length;i++){
 const a=data.assets[i];
 await fs.copyFile(a.source,root+'/backgrounds-originals/'+a.file.replace('.jpg','.png'));
 await sharp(a.source).resize(1280,720,{fit:'fill'}).jpeg({quality:87,mozjpeg:true}).toFile(out+'/'+a.file);
 const thumbnail=await sharp(out+'/'+a.file).resize(320,180).extend({top:0,bottom:30,left:0,right:0,background:'#f3eeff'}).toBuffer();
 const label=Buffer.from(`<svg width="320" height="30"><text x="12" y="21" font-family="Arial" font-size="15" fill="#35284f">${i+1}. ${a.name.replaceAll('-',' ')}</text></svg>`);
 tiles.push({input:await sharp(thumbnail).composite([{input:label,top:180,left:0}]).png().toBuffer(),left:(i%2)*332+12,top:Math.floor(i/2)*222+12});
 a.bytes=(await fs.stat(out+'/'+a.file)).size;
}
await sharp({create:{width:676,height:Math.ceil(data.assets.length/2)*222+12,channels:3,background:'#e8ddfa'}}).composite(tiles).png().toFile(root+'/'+sheet);
await fs.writeFile(root+'/'+manifest,JSON.stringify(data,null,2));
console.log(JSON.stringify({count:data.assets.length,totalBytes:data.assets.reduce((n,a)=>n+a.bytes,0),size:'1280x720'},null,2));
