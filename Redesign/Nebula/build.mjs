import fs from 'node:fs/promises';
import path from 'node:path';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve('Redesign/Nebula'), backup=path.join(root,'backups/ui'), stage=path.join(root,'ready');
await fs.mkdir(path.join(stage,'Onboarding'),{recursive:true});
await fs.mkdir(path.join(root,'masters'),{recursive:true});
await fs.mkdir(path.join(root,'vectors'),{recursive:true});
const inventory=JSON.parse((await fs.readFile('Redesign/inventory.json','utf8')).replace(/^\uFEFF/,''));
// These assets already have editable SVG sources; keep their geometry and typography.
const colors={
 '#43315f':'#f3e9ff','#9c7be3':'#9a69ec','#f8a8d8':'#bd80f8','#99e7f5':'#78e8f7','#fff5fc':'#e8daff',
 '#fbc5e7':'#713cbc','#c9b4ef':'#412071','#9ee8f4':'#315591',
 '#ffd1ef':'#9d62e7','#c876c8':'#4b247b','#625086':'#35204f','#30233f':'#11091f',
 '#fff1fb':'#2c1948','#e9ddfc':'#201235','#d6f5fb':'#18263d',
 '#fff2fd':'#dec6ff','#ebacff':'#ac62ff','#93dbff':'#64d6ff','#b68aff':'#8c44eb',
 '#765293':'#b68ce8','#b69ce8':'#925bcc','#805693':'#361d56','#513764':'#201031',
 '#fff4ff':'#d6beff','#cebcf2':'#9062c8','#392947':'#180e2b','#fff2fb':'#8c62bc',
 '#74518f':'#ad84df','#fff6ff':'#d8bfff','#756096':'#8059ae','#be9fdf':'#ac78ed',
 '#a2dcea':'#7fd9ed','#e4a9d7':'#a25fe1','#76538e':'#b18adb','#fff7ff':'#d8bfff',
 '#b399e5':'#8650c6','#61437f':'#b58bee','#ebbded':'#f1e4ff','#6c488c':'#b58bee','#ccaff2':'#eee2ff'
};
for(const file of (await fs.readdir('Redesign/vectors')).filter(x=>x.endsWith('.svg'))){
 let src=await fs.readFile(path.join('Redesign/vectors',file),'utf8');
 src=src.replace(/#[0-9a-fA-F]{6}/g,c=>colors[c.toLowerCase()]||c).replaceAll('GACHA NOX','GACHA NEBULA');
 const name=file.replace('.svg','.png'), meta=await sharp(path.join(backup,name)).metadata();
 if(name==='title.png')src=src.replace('<text ','<text textLength="'+(meta.width*.94)+'" lengthAdjust="spacingAndGlyphs" ');
 await fs.writeFile(path.join(root,'vectors',file),src);
 await sharp(Buffer.from(src),{density:288}).resize(meta.width,meta.height).png().toFile(path.join(stage,name));
}
async function bounds(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,top=info.height,right=-1,bottom=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>8){left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);}
 return {left,top,width:right-left+1,height:bottom-top+1};
}
const generated=JSON.parse(await fs.readFile(path.join(root,'prompts.json'),'utf8'));
if(generated.length!==26||generated.some(j=>!j.path))throw Error('Expected all 26 generated assets.');
for(const job of generated)await fs.copyFile(job.path,path.join(root,'masters',job.key+'.png'));
for(const item of generated.filter(j=>!j.onboarding)){
 const input=path.join(root,'masters',item.key+'.png'), old=path.join(backup,item.file),m=await sharp(old).metadata();
 const entry=inventory.find(x=>x.file===item.file),rect=entry.rects.length===1?entry.rects[0]:{x:0,y:0,w:m.width,h:m.height};
 let box={left:+rect.x,top:m.height-rect.y-rect.h,width:+rect.w,height:+rect.h};
 const bg=/^bg[12]\.png$/.test(item.file),frame=/^img[1-7]\.png$/.test(item.file);
 let pipeline=sharp(input);
 if(frame){box=await bounds(old);pipeline=pipeline.extract(await bounds(input));}
 const data=await pipeline.resize(box.width,box.height,{fit:(bg||frame)?'fill':'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toBuffer();
 await sharp({create:{width:m.width,height:m.height,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:data,left:box.left,top:box.top}]).png().toFile(path.join(stage,item.file));
}
const reuse={
 'character-lavender':'bear2','character-gacha-dj':'bear3','activity-coloring':'coloring','activity-jigsaw':'jigsaw','activity-memory':'memory',
 'world-dream-galaxy':'bg1','world-neon-city':'bg2'
};
for(const j of generated.filter(j=>j.onboarding))reuse[j.key]=j.key;
for(const [target,key] of Object.entries(reuse)){
 const m=await sharp(path.join(backup,'Onboarding',target+'.png')).metadata();
 const opaque=/^(style|world)-/.test(target);
 await sharp(path.join(root,'masters',key+'.png')).resize(m.width,m.height,{fit:opaque?'fill':'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toFile(path.join(stage,'Onboarding',target+'.png'));
}
console.log(JSON.stringify({ui:(await fs.readdir(stage)).filter(x=>x.endsWith('.png')).length,onboarding:Object.keys(reuse).length}));
// Install image content only. Unity .meta, animations and controllers stay untouched.
for(const file of (await fs.readdir(stage)).filter(x=>x.endsWith('.png')))await fs.copyFile(path.join(stage,file),path.join('Assets/Scenes/ui',file));
for(const target of Object.keys(reuse))await fs.copyFile(path.join(stage,'Onboarding',target+'.png'),path.join('Assets/Scenes/ui/Onboarding',target+'.png'));
