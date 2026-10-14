import fs from 'node:fs';
import path from 'node:path';
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const assets=walk('Assets');
const guidMap=new Map(assets.filter(f=>f.endsWith('.meta')).map(f=>[fs.readFileSync(f,'utf8').match(/^guid: (\w+)/m)?.[1],f.slice(0,-5)]));
let report=[];
for(const file of assets.filter(f=>/\.(unity|prefab)$/.test(f)&&f.startsWith('Assets'+path.sep+'_Game'))){
 const src=fs.readFileSync(file,'utf8'); const blocks=src.split(/(?=^--- !u!)/m);
 const names=new Map(blocks.filter(b=>b.startsWith('--- !u!1 &')).map(b=>[b.match(/^--- !u!1 &(\d+)/)?.[1],b.match(/^  m_Name: (.*)/m)?.[1]]));
 for(const b of blocks.filter(b=>b.includes('m_Sprite:'))){
  const guid=b.match(/m_Sprite: \{fileID: (-?\d+), guid: (\w+)/);
  if(!guid) continue;
  report.push({file,component:b.match(/^--- !u!\d+ &(-?\d+)/)?.[1],name:names.get(b.match(/m_GameObject: \{fileID: (\d+)/)?.[1]),sprite:guidMap.get(guid[2]),guid:guid[2],spriteId:guid[1],color:b.match(/m_Color: (.*)/)?.[1]});
 }
}
fs.mkdirSync('Redesign/DressUpLight',{recursive:true});
fs.writeFileSync('Redesign/DressUpLight/source-inventory.json',JSON.stringify(report,null,2));
const groups=new Map(); for(const r of report){ if(/Resources[\\/]Sprites/.test(r.sprite??'')) continue;let a=groups.get(r.sprite)||[];a.push(r.name);groups.set(r.sprite,a); }
for(const [sprite,names] of groups) console.log(sprite, [...new Set(names)].join(', '));
