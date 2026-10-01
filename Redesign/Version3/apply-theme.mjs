import fs from 'node:fs/promises';
const prefab='Assets/TMP_RM/Prefabs/BaseScene.prefab';
const before=await fs.readFile('Redesign/Version3/backups/BaseScene.prefab','utf8');
const chunks=before.split(/(?=^--- !u!)/m),names=new Map(),transforms=new Map(),goTransforms=new Map();
for(const chunk of chunks){const id=chunk.match(/^--- !u!(\d+) &(\d+)/);if(!id)continue;if(id[1]==='1')names.set(id[2],chunk.match(/^  m_Name: (.*)$/m)?.[1]?.trim());if(['224','4'].includes(id[1])){const go=chunk.match(/m_GameObject: \{fileID: (\d+)\}/)?.[1],father=chunk.match(/m_Father: \{fileID: (\d+)\}/)?.[1];transforms.set(id[2],{go,father});goTransforms.set(go,id[2]);}}
const root=[...names].find(([id,n])=>n==='Gacha Onboarding')?.[0];if(!root)throw Error('Onboarding root missing');
function belongs(go){let t=goTransforms.get(go);while(t&&transforms.has(t)){const item=transforms.get(t);if(item.go===root)return true;t=item.father;}return false;}
const rgb=(hex,a=1)=>{const v=hex.replace('#','');return {r:parseInt(v.slice(0,2),16)/255,g:parseInt(v.slice(2,4),16)/255,b:parseInt(v.slice(4,6),16)/255,a};};
const yaml=c=>`{r: ${c.r}, g: ${c.g}, b: ${c.b}, a: ${c.a}}`;
const imageColors={'Frame':'#8EC8CD','Face':'#F5FFFC','Artwork Well':'#DEF1F3','Selected Edge':'#27969C','Check Badge':'#27969C','Next':'#79D5C6','Back':'#D8EEF0','Progress Track':'#B0D6DF','Lock Badge':'#506F83'};
let changed=0;
const output=chunks.map(chunk=>{
 const go=chunk.match(/m_GameObject: \{fileID: (\d+)\}/)?.[1];if(!belongs(go))return chunk;
 const name=names.get(go);let next=chunk;
 if(chunk.includes('m_fontColor:')){
  const color=rgb(['Choice Description','Description'].includes(name)?'#607F90':'#29485B');
  next=next.replace(/m_fontColor: \{[^}]+\}/,`m_fontColor: ${yaml(color)}`);
  const packed=((255<<24)|(Math.round(color.b*255)<<16)|(Math.round(color.g*255)<<8)|Math.round(color.r*255))>>>0;
  next=next.replace(/(m_fontColor32:\r?\n\s+rgba:) \d+/,`$1 ${packed}`);
  if(name==='Gacha Nox Logo')next=next.replace('#D95CA8','#208D94').replace('#668DC9','#638DC9');
 }else if(chunk.includes('m_Color:')){
  let color=imageColors[name]?rgb(imageColors[name]):null;
  if(name==='Shadow')color=rgb('#29485B',.12);
  if(name==='Pastel Wash')color=rgb('#E9F8F5',.70);
  if(name==='Artwork Dim')color=rgb('#B9CDD8',.70);
  if(/^Step [1-4]$/.test(name))color=rgb(name==='Step 1'?'#27969C':'#B0D6DF');
  if(color)next=next.replace(/m_Color: \{[^}]+\}/,`m_Color: ${yaml(color)}`);
 }
 if(chunk.includes('m_Colors:')){
  next=next.replace(/m_HighlightedColor: \{[^}]+\}/,`m_HighlightedColor: ${yaml(rgb('#EBFFF8'))}`)
   .replace(/m_PressedColor: \{[^}]+\}/,`m_PressedColor: ${yaml(rgb('#C4EDE8'))}`)
   .replace(/m_DisabledColor: \{[^}]+\}/,`m_DisabledColor: ${yaml(rgb('#B1C6D1',.70))}`);
 }
 if(next!==chunk)changed++;return next;
}).join('');
// Only visual color fields may differ. Authored layout and event/sprite IDs stay byte-identical.
const normalize=s=>s.replace(/(?:m_Color|m_fontColor|m_(?:Highlighted|Pressed|Disabled)Color): \{[^}]+\}/g,'COLOR').replace(/(m_fontColor32:\r?\n\s+rgba:) \d+/g,'$1 COLOR').replace(/#[A-Fa-f0-9]{6}/g,'#COLOR');
if(normalize(before)!==normalize(output))throw Error('Unexpected non-color prefab modification');
await fs.writeFile(prefab,output);
await fs.writeFile('Redesign/Version3/layout-validation.json',JSON.stringify({onboardingComponentsUpdated:changed,onlyColorsChanged:true,layoutAndReferencesPreserved:true},null,2));
console.log(JSON.stringify({onboardingComponentsUpdated:changed,onlyColorsChanged:true}));
