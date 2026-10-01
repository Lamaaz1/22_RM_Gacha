import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve('Redesign/Version3'), source=path.join(root,'backups/ui'), stage=path.join(root,'ready');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
await fs.mkdir(path.join(stage,'Onboarding'),{recursive:true});
const colors={'#43315f':'#29485b','#9c7be3':'#70cfc7','#f8a8d8':'#ffc1bc','#99e7f5':'#9fdcf6','#fff5fc':'#f7fffd','#fbc5e7':'#d6fff1','#c9b4ef':'#a8e6ed','#9ee8f4':'#b4d8ff','#ffd1ef':'#d9fff6','#c876c8':'#7bcaca','#625086':'#457386','#30233f':'#243f58','#fff1fb':'#fcfffd','#e9ddfc':'#e4f7f5','#d6f5fb':'#e0edff','#fff2fd':'#ffffff','#ebacff':'#b4f6dd','#93dbff':'#91d8ff','#b68aff':'#a2e5e3','#765293':'#4a8392','#b69ce8':'#92d7cf','#61437f':'#356478','#ebbded':'#baf4e6','#805693':'#428c95','#513764':'#2a5766','#fff4ff':'#f7fffe','#cebcf2':'#b2e3e7','#392947':'#243f58','#fff2fb':'#f3fffa','#74518f':'#528a98','#fff6ff':'#f9fffd','#756096':'#71a7b3','#be9fdf':'#93d7c9','#a2dcea':'#a6d4fa','#e4a9d7':'#f2b7b4','#76538e':'#498798','#fff7ff':'#f7fffd','#b399e5':'#8cd7cf','#6c488c':'#467688','#ccaff2':'#b6eee2'};
for(const file of (await fs.readdir('Redesign/vectors')).filter(f=>f.endsWith('.svg'))){
 let svg=await fs.readFile(path.join('Redesign/vectors',file),'utf8');
 svg=svg.replace(/#[0-9a-f]{6}/gi,c=>colors[c.toLowerCase()]||c);
 // Preserve original bounds while giving the button system a pearl/mint rim.
 if(file==='back button bg.svg') svg=svg.replace('rx="29"','rx="22"').replace('rx="23"','rx="17"');
 if(file==='get bundle home button.svg') svg=svg.replace('rx="70"','rx="42"').replace('rx="68"','rx="38"');
 if(file==='paper bg.svg') svg=svg.replace('rx="48"','rx="30"').replace('rx="40"','rx="24"');
 await fs.writeFile(path.join(root,'vectors',file),svg);
 const name=file.replace('.svg','.png'),m=await sharp(path.join(source,name)).metadata();
 await sharp(Buffer.from(svg),{density:288}).resize(m.width,m.height).png().toFile(path.join(stage,name));
}
async function bounds(file){const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});let l=info.width,t=info.height,r=-1,b=-1;for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>8){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y)}if(r<0)throw Error('Empty image '+file);return {left:l,top:t,width:r-l+1,height:b-t+1};}
const inventory=JSON.parse((await fs.readFile('Redesign/inventory.json','utf8')).replace(/^\uFEFF/,''));
const generated=JSON.parse(await fs.readFile(path.join(root,'prompts.json'),'utf8')).images;
const onboard=new Set(['character-nox-bunny.png','style-cute-pastel.png','style-dark-gothic.png','style-neon-pop.png','world-music-studio.png','activity-dressing.png']);
for(const item of generated){
 const master=path.join(root,'masters',item.file); await fs.copyFile(item.path,master);
 const relative=onboard.has(item.file)?'Onboarding/'+item.file:item.file;
 await fit(master,relative);
}
async function fit(master,relative){
 const old=path.join(source,relative),m=await sharp(old).metadata(),isBackground=/^(bg[12]|world-.*)\.png$/.test(path.basename(relative)),frame=/^img[1-7]\.png$/.test(relative);
 const entry=inventory.find(x=>x.file===relative),rect=entry?.rects.length===1?entry.rects[0]:null;
 let box=rect?{left:+rect.x,top:m.height-(+rect.y)-(+rect.h),width:+rect.w,height:+rect.h}:{left:0,top:0,width:m.width,height:m.height};
 let pipeline=sharp(master);
 if(frame){box=await bounds(old);pipeline=pipeline.extract(await bounds(master));}
 const bytes=await pipeline.resize(box.width,box.height,{fit:(isBackground||frame)?'fill':'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toBuffer();
 await sharp({create:{width:m.width,height:m.height,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:bytes,left:box.left,top:box.top}]).png().toFile(path.join(stage,relative));
}
for(const [file,master] of Object.entries({'character-lavender.png':'bear2.png','character-gacha-dj.png':'bear3.png','world-neon-city.png':'bg2.png','world-dream-galaxy.png':'bg1.png','activity-coloring.png':'coloring.png','activity-jigsaw.png':'jigsaw.png','activity-memory.png':'memory.png'}))await fit(path.join(root,'masters',master),'Onboarding/'+file);
async function files(dir,prefix=''){let out=[];for(const d of await fs.readdir(dir,{withFileTypes:true})){const rel=prefix+d.name;if(d.isDirectory())out.push(...await files(path.join(dir,d.name),rel+'/'));else out.push(rel)}return out;}
const rows=[];for(const file of (await files(stage)).filter(f=>f.endsWith('.png')).sort()){
 const a=await fs.readFile(path.join(source,file)),b=await fs.readFile(path.join(stage,file));const before=await sharp(a).metadata(),after=await sharp(b).metadata();
 if(before.width!==after.width||before.height!==after.height)throw Error('Wrong size: '+file);
 if(sha(a)===sha(b))throw Error('Unchanged: '+file);
 const stats=await sharp(b).stats();if(!/^(bg[12]\.png|Onboarding\/(world-|style-))/.test(file)&&stats.isOpaque)throw Error('Missing alpha: '+file);
 rows.push({file,width:after.width,height:after.height,sha256:sha(b),hasAlpha:after.hasAlpha,isOpaque:stats.isOpaque});
}
if(rows.length!==58)throw Error('Expected 58 updated images, got '+rows.length);
await fs.writeFile(path.join(root,'staged-validation.json'),JSON.stringify({images:rows.length,dimensionsMatch:true,assets:rows},null,2));
// A contact sheet for visual inspection before installation.
const cellW=240,cellH=190,cols=5,width=cols*cellW,height=Math.ceil(rows.length/cols)*cellH+70,layers=[];
layers.push({input:Buffer.from(`<svg width="${width}" height="70"><rect width="100%" height="100%" fill="#29485b"/><text x="24" y="30" fill="#ccfff0" font-size="25" font-family="Segoe UI" font-weight="bold">GACHA / VERSION 3</text><text x="24" y="53" fill="#b7dfec" font-size="15" font-family="Segoe UI">Pearl, aqua and peach · 58 images · original dimensions</text></svg>`),left:0,top:0});
for(let i=0;i<rows.length;i++){const r=rows[i],x=i%cols*cellW,y=Math.floor(i/cols)*cellH+70;layers.push({input:Buffer.from(`<svg width="240" height="190"><rect x="4" y="4" width="232" height="182" rx="10" fill="${i%2?'#edf8f5':'#e4eff5'}"/><text x="12" y="166" fill="#29485b" font-size="12" font-family="Segoe UI">${r.file.replace('Onboarding/','')}</text><text x="12" y="181" fill="#678696" font-size="10" font-family="Segoe UI">${r.width} × ${r.height}</text></svg>`),left:x,top:y});const thumb=await sharp(path.join(stage,r.file)).resize(218,144,{fit:'inside'}).png().toBuffer({resolveWithObject:true});layers.push({input:thumb.data,left:x+Math.floor((240-thumb.info.width)/2),top:y+10+Math.floor((144-thumb.info.height)/2)});}
await sharp({create:{width,height,channels:4,background:'#cddfe8'}}).composite(layers).png().toFile(path.join(root,'UI-preview.png'));
console.log(JSON.stringify({staged:rows.length,dimensionsMatch:true}));
