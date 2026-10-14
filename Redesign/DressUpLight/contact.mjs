import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dirs=fs.readdirSync('Assets',{withFileTypes:true}).filter(f=>f.isDirectory()&&f.name.startsWith('drive-download')).map(f=>f.name);
let files=dirs.flatMap(d=>fs.readdirSync('Assets/'+d).filter(f=>f.endsWith('.png')).map(f=>'Assets/'+d+'/'+f));
const cells=[];let info=[];
for(let i=0;i<files.length;i++){
 let m=await sharp(files[i]).metadata();info.push({file:files[i],width:m.width,height:m.height});
 const thumb=await sharp(files[i]).resize(142,92,{fit:'contain',background:'#e3daf0'}).png().toBuffer();
 const label=Buffer.from(`<svg width="160" height="35"><text x="6" y="14" font-size="11" font-family="Arial" fill="#35284f">${path.basename(files[i]).replace(/&/g,'&amp;')}</text><text x="6" y="29" font-size="10" font-family="Arial" fill="#62577e">${i}: ${m.width}×${m.height}</text></svg>`);
 cells.push({input:thumb,left:(i%7)*160+9,top:Math.floor(i/7)*132+3},{input:label,left:(i%7)*160,top:Math.floor(i/7)*132+96});
}
await sharp({create:{width:1120,height:Math.ceil(files.length/7)*132,channels:4,background:'#f3eeff'}}).composite(cells).png().toFile('Redesign/DressUpLight/original-contact.png');
fs.writeFileSync('Redesign/DressUpLight/image-sizes.json',JSON.stringify(info,null,2));
