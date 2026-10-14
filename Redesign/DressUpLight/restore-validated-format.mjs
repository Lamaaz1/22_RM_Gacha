import fs from 'node:fs/promises';
import path from 'node:path';
const snapshot='Temp/LightThemeValidation';
const changes=JSON.parse(await fs.readFile('Redesign/DressUpLight/scene-changes.json','utf8'));
const map=JSON.parse(await fs.readFile('Redesign/DressUpLight/replacement-map.json','utf8'));
for(const file of [...changes.map(c=>c.file),...map.map(c=>c.file+'.meta')]){
 const data=await fs.readFile(path.join(snapshot,file));
 const stat=await fs.stat(file);
 if(stat.size===data.length){
  // Unity may have a read mapping open. Same-length writes preserve that mapping.
  const handle=await fs.open(file,'r+');
  try{await handle.write(data,0,data.length,0);}finally{await handle.close();}
 }else await fs.writeFile(file,data);
}
console.log('Restored the exact Unity-validated serialization.');
