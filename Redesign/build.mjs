import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve('Redesign'), original=path.join(root,'originals/ui'), stage=path.join(root,'ready');
await fs.mkdir(stage,{recursive:true}); await fs.mkdir(path.join(root,'masters'),{recursive:true}); await fs.mkdir(path.join(root,'vectors'),{recursive:true});
const inventory=JSON.parse((await fs.readFile(path.join(root,'inventory.json'),'utf8')).replace(/^\uFEFF/,''));
const palette={ink:'#43315f',violet:'#9c7be3',pink:'#f8a8d8',cyan:'#99e7f5',white:'#fff5fc'};
const defs=`<defs><linearGradient id="pastel" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#fbc5e7"/><stop offset=".5" stop-color="#c9b4ef"/><stop offset="1" stop-color="#9ee8f4"/></linearGradient><linearGradient id="pink" x2="0" y2="1"><stop stop-color="#ffd1ef"/><stop offset="1" stop-color="#c876c8"/></linearGradient><linearGradient id="dark" x2="0" y2="1"><stop stop-color="#625086"/><stop offset="1" stop-color="#30233f"/></linearGradient><linearGradient id="paper" x2="1" y2="1"><stop stop-color="#fff1fb"/><stop offset=".55" stop-color="#e9ddfc"/><stop offset="1" stop-color="#d6f5fb"/></linearGradient><radialGradient id="glow"><stop stop-color="#fff2fd" stop-opacity=".94"/><stop offset=".3" stop-color="#ebacff" stop-opacity=".67"/><stop offset=".7" stop-color="#93dbff" stop-opacity=".23"/><stop offset="1" stop-color="#b68aff" stop-opacity="0"/></radialGradient></defs>`;
const svg=(w,h,body)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${body}</svg>`;
function star(cx,cy,r,fill=palette.white,points=4){let p=[];for(let i=0;i<points*2;i++){let a=-Math.PI/2+i*Math.PI/points,rr=i%2?r*.35:r;p.push(`${cx+Math.cos(a)*rr},${cy+Math.sin(a)*rr}`)}return `<polygon points="${p.join(' ')}" fill="${fill}"/>`}
const heart=(x,y,s,c)=>`<path transform="translate(${x} ${y}) scale(${s/24})" d="M12 22C7 18 0 13 0 7C0-1 10-2 12 5C15-2 24-1 24 7C24 13 17 18 12 22Z" fill="${c}"/>`;
const text=(t,x,y,size,color=palette.ink,extra='')=>`<text x="${x}" y="${y}" text-anchor="middle" dominant-baseline="central" font-family="Arial Rounded MT Bold,Segoe UI,Arial" font-weight="800" font-size="${size}" fill="${color}" ${extra}>${t}</text>`;
async function writeVector(file,body){let m=await sharp(path.join(original,file)).metadata();let src=svg(m.width,m.height,body(m.width,m.height));await fs.writeFile(path.join(root,'vectors',file.replace('.png','.svg')),src);await sharp(Buffer.from(src),{density:288}).resize(m.width,m.height).png().toFile(path.join(stage,file));}
const rr=(x,y,w,h,r,fill,stroke=palette.ink,sw=3)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const titles={'BACK.png':'BACK','GET BUNDLE.png':'GET BUNDLE','Calculator Game.png':'Calculator Game · Solve Fun Sums','Coloring Book.png':'Coloring Book · Relax and Create','Jigsaw Puzzle.png':'Jigsaw Puzzle · Fit the Pieces','Maze Game.png':'Maze Game · Find Your Way','Memory Match.png':'Memory Match · Train Your Mind','title.png':'GACHA NOX · MINI GAME BUNDLE'};
for(const [file,label] of Object.entries(titles))await writeVector(file,(w,h)=>text(label,w/2,h/2,Math.min(h*.78,w/(label.length*.56)),palette.ink));
await writeVector('LIMITED OFFER.png',(w,h)=>text('LIMITED',w/2,h*.25,29)+text('OFFER',w/2,h*.75,31));
await writeVector('30%off.png',(w,h)=>`<path d="M149 16L191 73L266 91L231 156L244 230L171 234L111 273L73 210L19 169L63 108L71 35L149 16Z" fill="url(#pink)" stroke="#fff5fc" stroke-width="12"/><path d="M149 16L191 73L266 91L231 156L244 230L171 234L111 273L73 210L19 169L63 108L71 35L149 16Z" fill="none" stroke="#765293" stroke-width="4"/>${text('30% OFF',149,130,43)}${text('ALL 5 GAMES',149,175,24)}${star(109,58,10,palette.white)}${star(206,210,12,palette.cyan)}`);
await writeVector('back button bg.png',(w,h)=>rr(3,5,w-6,h-9,29,'url(#dark)','#b69ce8',4)+rr(10,12,w-20,h-25,23,'url(#pastel)','#fff5fc',2));
await writeVector('get bundle home button.png',(w,h)=>rr(4,8,w-8,h-13,70,'#805693','#513764',4)+rr(5,3,w-10,h-17,68,'url(#pink)','#fff4ff',4)+`<path d="M40 25Q220 8 490 25" fill="none" stroke="#fff5fc" stroke-width="5" stroke-linecap="round"/>`+text('Get Bundle',w/2,h*.46,56)+star(49,76,18,palette.white)+star(w-49,76,18,palette.cyan));
await writeVector('loading bar bg.png',(w,h)=>rr(2,2,w-4,h-4,h/2-2,'url(#dark)','#cebcf2',4)+rr(14,13,w-28,h-26,h/2-13,'#392947','#fff2fb',2));
await writeVector('loading bar fill.png',(w,h)=>rr(0,0,w,h,h/2,'url(#pastel)','none',0)+`<path d="M30 13H${w-30}" stroke="#ffffff" stroke-opacity=".72" stroke-width="7" stroke-linecap="round"/>`);
await writeVector('offer.png',(w,h)=>`<path d="M5 5H${w-5}V${h-7}L${w/2} ${h-42}L5 ${h-7}Z" fill="url(#pastel)" stroke="#74518f" stroke-width="5"/><path d="M13 12H${w-13}" stroke="#fff6ff" stroke-width="4"/>`);
await writeVector('paper bg.png',(w,h)=>rr(10,12,w-28,h-28,48,'url(#dark)','#756096',8)+rr(22,20,w-52,h-52,40,'url(#paper)','#fff5fc',5)+`<path d="M75 46H${w-85}" stroke="#ffffff" stroke-width="5" stroke-linecap="round"/>`+star(67,69,23,'#be9fdf')+star(w-80,h-88,28,'#a2dcea')+heart(w-102,65,24,'#e4a9d7'));
await writeVector('title bg.png',(w,h)=>`<path d="M8 26Q${w/2} -14 ${w-8} 26L${w-32} 73L${w-8} 120Q${w/2} 94 8 120L32 73Z" fill="url(#pastel)" stroke="#76538e" stroke-width="4"/><path d="M48 33Q${w/2} 5 ${w-48} 33" fill="none" stroke="#fff7ff" stroke-width="4"/>`+star(57,72,18,palette.white)+star(w-57,72,18,palette.white));
await writeVector('Star bg.png',(w,h)=>`<ellipse cx="${w/2}" cy="${h/2}" rx="${w*.455}" ry="${h*.5}" fill="url(#glow)"/>`);
await writeVector('confetti.png',(w,h)=>{let out='',seed=8247;const rand=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/4294967296};for(let i=0;i<66;i++){let x=18+rand()*(w-36),y=18+rand()*(h-36),s=5+rand()*8,c=[palette.pink,palette.cyan,'#b399e5',palette.white][i%4];out+=i%3?star(x,y,s,c,i%2?4:5):heart(x,y,s*1.35,c)}return out});
await writeVector('back arrow.png',(w,h)=>`<path d="M19 5L5 20L19 35" fill="none" stroke="#61437f" stroke-width="8" stroke-linejoin="round" stroke-linecap="round"/><path d="M19 5L5 20L19 35" fill="none" stroke="#ebbded" stroke-width="4" stroke-linejoin="round" stroke-linecap="round"/>`);
for(const [file,flip] of [['calculator arrow.png',false],['coloring arrow.png',true],['jigsaw arrow.png',false],['maze arrow.png',false],['memory arrow.png',true]])await writeVector(file,(w,h)=>{let d=flip?`M${w*.2} ${h*.92}C${-w*.18} ${h*.3} ${w*.98} ${h*.63} ${w*.82} ${h*.12}`:`M${w*.22} ${h*.07}C${w*1.4} ${h*.5} ${-w*.23} ${h*.75} ${w*.28} ${h*.9}`;let ax=flip?w*.82:w*.28,ay=flip?h*.12:h*.9,sgn=flip?1:-1;return `<path d="${d}" fill="none" stroke="#6c488c" stroke-width="${Math.max(4,w*.08)}" stroke-linecap="round"/><path d="${d}" fill="none" stroke="#ccaff2" stroke-width="${Math.max(2,w*.038)}" stroke-linecap="round"/><path d="M${ax-w*.17} ${ay+sgn*h*.08}L${ax} ${ay}L${ax+w*.19} ${ay+sgn*h*.065}" fill="none" stroke="#6c488c" stroke-width="${Math.max(4,w*.08)}" stroke-linecap="round" stroke-linejoin="round"/>`});
await writeVector('coloring arrow.png',(w,h)=>`<path d="M65 6C2 37 -12 105 66 103M54 88L70 103L54 114" fill="none" stroke="#6c488c" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/><path d="M65 6C2 37 -12 105 66 103" fill="none" stroke="#ccaff2" stroke-width="2" stroke-linecap="round"/>`);
// Generated artwork is only resized and padded to the original Unity sprite rect.
// No metadata, pivot, sprite ID, controller, prefab or animation is changed.
let generated=[];try{generated=JSON.parse(await fs.readFile(path.join(root,'generated.json'),'utf8'))}catch{}
async function alphaBounds(file){const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});let x0=info.width,y0=info.height,x1=-1,y1=-1;for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>8){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y)}return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1}}
for(const item of generated){
 const input=path.join(root,'masters',item.file);await fs.copyFile(item.path,input);
 const old=path.join(original,item.file),m=await sharp(old).metadata(),entry=inventory.find(x=>x.file===item.file);
 const rect=entry.rects.length===1?entry.rects[0]:{x:0,y:0,w:m.width,h:m.height};
 let box={left:+rect.x,top:m.height-(+rect.y)-(+rect.h),width:+rect.w,height:+rect.h};
 const bg=/^bg[12]\.png$/.test(item.file),frame=/^img[1-7]\.png$/.test(item.file);
 let pipeline=sharp(input);
 if(frame){box=await alphaBounds(old);pipeline=pipeline.extract(await alphaBounds(input));}
 const data=await pipeline.resize(box.width,box.height,{fit:(bg||frame)?'fill':'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toBuffer();
 await sharp({create:{width:m.width,height:m.height,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:data,left:box.left,top:box.top}]).png().toFile(path.join(stage,item.file));
}
const made=(await fs.readdir(stage)).filter(x=>x.endsWith('.png'));console.log(JSON.stringify({ready:made.length,files:made},null,2));
