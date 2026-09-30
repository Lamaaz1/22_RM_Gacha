import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve('Redesign/Nebula'),original=path.join(root,'backups/ui'),target=path.resolve('Assets/Scenes/ui');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const rows=[];let unchanged=0,shapes=0;
async function walk(dir=''){
 for(const ent of await fs.readdir(path.join(original,dir),{withFileTypes:true})){
  const relative=path.join(dir,ent.name);if(ent.isDirectory()){await walk(relative);continue;}
  const a=await fs.readFile(path.join(original,relative)),b=await fs.readFile(path.join(target,relative));
  if(!ent.name.endsWith('.png')){if(sha(a)!==sha(b))throw Error('Non-image changed: '+relative);unchanged++;continue;}
  const old=await sharp(a).metadata(),now=await sharp(b).metadata();
  if(old.width!==now.width||old.height!==now.height)throw Error('Size mismatch: '+relative);
  if(/^ui-/.test(ent.name)){if(sha(a)!==sha(b))throw Error('Shape geometry changed: '+relative);shapes++;continue;}
  if(sha(a)===sha(b))throw Error('Unchanged artwork: '+relative);
  if(now.format!=='png')throw Error('Wrong format: '+relative);
  if(!/^(bg[12]|style-|world-)/.test(ent.name)){const s=await sharp(b).stats();if(!now.hasAlpha||s.isOpaque)throw Error('Missing transparency: '+relative);}
  rows.push({file:relative.replaceAll('\\','/'),width:now.width,height:now.height,alpha:now.hasAlpha,sha256:sha(b)});
 }
}
await walk();
const report={imagesReplaced:rows.length,originalUiImages:rows.filter(x=>!x.file.includes('/')).length,onboardingImages:rows.filter(x=>x.file.includes('/')).length,allDimensionsMatch:true,allNonImageFilesUnchanged:unchanged,untouchedTintableShapes:shapes,assets:rows};
await fs.writeFile(path.join(root,'validation.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({...report,assets:undefined},null,2));
const names=rows.filter(x=>!x.file.includes('/')).sort((a,b)=>a.file.localeCompare(b.file));
const cellW=260,cellH=205,cols=5,width=cellW*cols,height=Math.ceil(names.length/cols)*cellH+90,layers=[];
layers.push({input:Buffer.from(`<svg width="${width}" height="90"><rect width="100%" height="100%" fill="#140b23"/><text x="30" y="40" fill="#d5b6ff" font-size="27" font-family="Segoe UI" font-weight="bold">GACHA NEBULA · UI ASSETS</text><text x="30" y="69" fill="#83e8fa" font-size="16" font-family="Segoe UI">45 PNGs · original sizes and Unity references preserved</text></svg>`),left:0,top:0});
for(let i=0;i<names.length;i++){
 const r=names[i],x=(i%cols)*cellW,y=Math.floor(i/cols)*cellH+90;
 const tile=`<svg width="${cellW}" height="${cellH}"><rect x="4" y="4" width="252" height="197" rx="12" fill="${i%2?'#281c3e':'#35264a'}"/><text x="12" y="177" fill="#f3e9ff" font-size="13" font-family="Segoe UI" font-weight="bold">${r.file}</text><text x="12" y="194" fill="#bfa8dc" font-size="11" font-family="Segoe UI">${r.width} × ${r.height}</text></svg>`;
 layers.push({input:Buffer.from(tile),left:x,top:y});
 const resized=await sharp(path.join(target,r.file)).resize(236,154,{fit:'inside'}).png().toBuffer({resolveWithObject:true});
 layers.push({input:resized.data,left:x+Math.floor((cellW-resized.info.width)/2),top:y+10+Math.floor((154-resized.info.height)/2)});
}
await sharp({create:{width,height,channels:4,background:'#140b23'}}).composite(layers).png().toFile(path.join(root,'Gacha-Nebula-UI-preview.png'));
