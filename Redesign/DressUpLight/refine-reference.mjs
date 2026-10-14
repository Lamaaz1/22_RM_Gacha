import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/desig/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Redesign/DressUpLight',kit='Assets/_Game/UI/LightTheme';
const manifest=JSON.parse(await fs.readFile(root+'/assets.json','utf8'));
const defs='<defs><linearGradient id="normal" x2="0" y2="1"><stop stop-color="#FAF5FF"/><stop offset="1" stop-color="#EDE3FA"/></linearGradient><linearGradient id="active" x2="0" y2="1"><stop stop-color="#A487EF"/><stop offset="1" stop-color="#8D6CE5"/></linearGradient></defs>';
const rect=(x,y,w,h,r,fill,stroke='none',sw=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const star=(x,y,s,color)=>`<path d="M${x} ${y-s}Q${x+s*.2} ${y-s*.2} ${x+s} ${y}Q${x+s*.2} ${y+s*.2} ${x} ${y+s}Q${x-s*.2} ${y+s*.2} ${x-s} ${y}Q${x-s*.2} ${y-s*.2} ${x} ${y-s}Z" fill="${color}"/>`;
const frame=(w,h,selected=false)=>rect(2,4,w-4,h-6,22,'#C3AFE9')+rect(2,2,w-4,h-6,22,selected?'url(#active)':'url(#normal)',selected?'#AB8BF4':'#D1BFEF',2)+rect(6,6,w-12,h-14,18,'none',selected?'#CBB6FC':'#FFFFFF',1.5);
async function emit(name,w,h,body){
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${body}</svg>`;
 await fs.writeFile(root+'/vectors/'+name+'.svg',svg);
 const item=manifest.find(a=>a.name===name);
 await sharp(Buffer.from(svg),{density:144}).resize(w,h).png({compressionLevel:9,palette:true,colours:256,dither:0.2}).toFile(item.file);
 Object.assign(item,{width:w,height:h,bytes:(await fs.stat(item.file)).size});
}
let gearPoints=[];
for(let t=0;t<8;t++)for(const [o,r]of [[-22.5,21],[-14,21],[-10,27],[10,27],[14,21],[22.5,21]]){
 const a=(-90+t*45+o)*Math.PI/180;gearPoints.push(`${(32+Math.cos(a)*r).toFixed(3)} ${(32+Math.sin(a)*r).toFixed(3)}`);
}
const shapes={
 view:'<path d="M3 32Q17 13 32 13Q47 13 61 32Q47 51 32 51Q17 51 3 32Z"/><circle cx="32" cy="32" r="10" fill="#9675E9"/>',
 studio:'<path d="M9 24H55V56H9Z M8 24L5 12L50 3L53 15Z"/><path d="M14 11L21 20M28 8L35 17M42 5L49 14" fill="none" stroke="#F5EFFF" stroke-width="5"/><path d="M27 32L42 40L27 49Z" fill="#F5EFFF"/>',
 background:'<rect x="6" y="15" width="43" height="43" rx="4" transform="rotate(-8 27 36)" opacity=".4"/><rect x="13" y="6" width="46" height="46" rx="5"/><circle cx="27" cy="20" r="5" fill="#F5EFFF"/><path d="M18 44L31 30L39 37L48 23L54 32V46H18Z" fill="#F5EFFF"/>',
 settings:`<path d="M${gearPoints.join('L')}Z"/><circle cx="32" cy="32" r="10" fill="#F5EFFF"/>`
};
for(const name of ['view','studio','background','settings']){
 const selected=name==='view',color=selected?'#FFFFFF':'#6B559E';
 // Separate filled icons; navigation backgrounds stay text-free for Unity labels.
 await emit('icon-'+name,64,64,`<g fill="${color}">${shapes[name]}</g>`);
 let body=frame(320,80,selected);
 if(selected)body+=star(25,40,9,'#EEE6FF')+star(296,40,9,'#D7C6FC');
 if(name==='settings')body+=star(291,40,7,'#BDA3EA');
 body+=`<g transform="translate(${name==='background'?49:55} 17) scale(.72)" fill="${color}">${shapes[name]}</g>`;
 await emit('nav-'+name,320,80,body);
 const oldName={view:'24',studio:'22',background:'23',settings:'25'}[name];
 await fs.copyFile(kit+'/nav-'+name+'.png','Assets/drive-download-20251022T085704Z-1-001/Asset '+oldName+'.png');
}
for(const name of ['face','clothes','hair','hat','pants','props']){
 const original=await fs.readFile(root+'/vectors/category-'+name+'.svg','utf8');
 const icon=original.match(/<g transform="translate\(28 18\) scale\(1.125\)"[\s\S]*?<\/g>/)?.[0];
 if(!icon)throw new Error('Category icon not found: '+name);
 const label={face:'Face',clothes:'Clothes',hair:'Hair',hat:'Hat',pants:'Pants',props:'Props'}[name];
 await emit('category-'+name,128,128,frame(128,128)+icon.replace('translate(28 18) scale(1.125)','translate(29 13) scale(1.1)')+`<text x="64" y="108" text-anchor="middle" dominant-baseline="central" font-family="Arial" font-weight="700" font-size="18" fill="#35284F">${label}</text>`);
 const oldName={face:'8',clothes:'7',hair:'3',hat:'6',pants:'5',props:'4'}[name];
 await fs.copyFile(kit+'/category-'+name+'.png','Assets/drive-download-20251021T154352Z-1-001/Asset '+oldName+'.png');
}
await fs.writeFile(root+'/assets.json',JSON.stringify(manifest,null,2));
// A labelled visual proof at the actual 4:1 button proportions.
const labels=['View','Studio','Background','Settings'];const layers=[];
for(let i=0;i<4;i++){
 const name=labels[i].toLowerCase();
 layers.push({input:await sharp(kit+'/nav-'+name+'.png').toBuffer(),left:14+i*334,top:12});
 const label=Buffer.from(`<svg width="320" height="80"><text x="184" y="42" text-anchor="middle" dominant-baseline="central" font-family="Arial" font-weight="700" font-size="25" fill="${i===0?'#FFFFFF':'#6B559E'}">${labels[i]}</text></svg>`);
 layers.push({input:label,left:14+i*334,top:12});
}
await sharp({create:{width:1350,height:104,channels:4,background:'#F3EEFF'}}).composite(layers).png().toFile(root+'/navigation-refined.png');
console.log('Refined navigation and category sprites to follow the original reference.');
