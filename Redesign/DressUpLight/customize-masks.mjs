import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',kit='Assets/_Game/UI/LightTheme';
const assets=JSON.parse(await fs.readFile(root+'/assets.json','utf8'));
for(const key of ['face','clothes','hair','hat','pants','props','next','previous','dot','sparkle']){
 const source=key==='dot'?'page-dot':key==='sparkle'?'sparkle':'icon-'+key;
 const original=assets.find(item=>item.name===source),name='mask-'+key,file=kit+'/'+name+'.png';
 const vector=(await fs.readFile(root+'/vectors/'+source+'.svg','utf8')).replace(/#[0-9A-Fa-f]{6}/g,'#FFFFFF');
 await fs.writeFile(root+'/vectors/'+name+'.svg',vector);
 await sharp(Buffer.from(vector),{density:144}).resize(original.width,original.height).png({compressionLevel:9,palette:true,colours:64}).toFile(file);
 let guid;try{guid=(await fs.readFile(file+'.meta','utf8')).match(/^guid: (\w+)/m)[1];}catch{
  guid=crypto.randomBytes(16).toString('hex');
  let meta=await fs.readFile(kit+'/'+source+'.png.meta','utf8');
  await fs.writeFile(file+'.meta',meta.replace(/^guid: \w+/m,'guid: '+guid).replace(/spriteID: \w+/,'spriteID: '+crypto.randomBytes(16).toString('hex')));
 }
 const item={name,file,width:original.width,height:original.height,border:0,bytes:(await fs.stat(file)).size,guid};
 const index=assets.findIndex(a=>a.name===name);if(index<0)assets.push(item);else assets[index]=item;
}
await fs.writeFile(root+'/assets.json',JSON.stringify(assets,null,2));
console.log('Tintable category, arrow, dot and sparkle sprites ready.');
