import fs from 'node:fs/promises';
import path from 'node:path';
const out='Temp/BackgroundsValidation';
await fs.mkdir(out,{recursive:true});
await fs.cp('Assets',out+'/Assets',{recursive:true});
await fs.cp('ProjectSettings',out+'/ProjectSettings',{recursive:true});
await fs.mkdir(out+'/Packages',{recursive:true});
const original=JSON.parse(await fs.readFile('Packages/manifest.json','utf8'));
const dependencies=Object.fromEntries(Object.entries(original.dependencies).filter(([name])=>name.startsWith('com.unity.modules.')));
for(const entry of await fs.readdir('Library/PackageCache',{withFileTypes:true})){
 if(!entry.isDirectory())continue;
 const dir=path.resolve('Library/PackageCache',entry.name);
 try {const data=JSON.parse(await fs.readFile(path.join(dir,'package.json'),'utf8'));dependencies[data.name]='file:'+dir.replaceAll('\\','/');}catch{}
}
await fs.writeFile(out+'/Packages/manifest.json',JSON.stringify({dependencies},null,2));
console.log(out);
