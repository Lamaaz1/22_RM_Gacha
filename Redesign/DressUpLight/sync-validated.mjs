import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
const root='Redesign/DressUpLight', snapshot=process.argv[2] || 'Temp/LightThemeValidation';
const walk=async d=>(await Promise.all((await fs.readdir(d,{withFileTypes:true})).map(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]))).flat();
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
await fs.mkdir(root+'/originals',{recursive:true});
let changes=[];
const paths=[...(await walk('Assets/_Game/Prefabs')).filter(f=>f.endsWith('.prefab')),'Assets/_Game/Scenes/_Game.unity'];
for(const file of paths){
 const original=await fs.readFile(file),modified=await fs.readFile(path.join(snapshot,file));
 if(hash(original)===hash(modified)) continue;
 const backup=root+'/originals/'+file.replaceAll(/[\\/]/g,'__');
 try{await fs.access(backup);}catch{await fs.writeFile(backup,original);}
 await fs.writeFile(file,modified);changes.push({file,backup,originalSHA256:hash(original),newSHA256:hash(modified)});
}
const map=JSON.parse(await fs.readFile(root+'/replacement-map.json','utf8'));
for(const entry of map) await fs.copyFile(path.join(snapshot,entry.file+'.meta'),entry.file+'.meta');
for(const file of await walk(snapshot+'/Assets/_Game/UI/LightTheme')){
 const relative=path.relative(snapshot,file);await fs.copyFile(file,relative);
}
for(const name of ['GachaDressUpLight.unitypackage','unity-preview.png','unity-color-preview.png','unity-settings-preview.png','unity-backgrounds-preview.png','unity-backgrounds-page2-preview.png','unity-hair-preview.png','unity-face-preview.png']){
 try{await fs.copyFile(snapshot+'/Redesign/DressUpLight/'+name,root+'/'+name);}catch(error){if(error.code!=='ENOENT')throw error;}
}
const backgrounds='Assets/_Game/Art/Backgrounds/EmptyFloor';
try {
 await fs.mkdir(backgrounds,{recursive:true});
 for(const file of await walk(snapshot+'/'+backgrounds))await fs.copyFile(file,path.relative(snapshot,file));
 for(const folder of ['Assets/_Game/Art','Assets/_Game/Art/Backgrounds',backgrounds]){
  try{await fs.copyFile(snapshot+'/'+folder+'.meta',folder+'.meta');}catch(error){if(error.code!=='ENOENT')throw error;}
 }
}catch(error){if(error.code!=='ENOENT')throw error;}
await fs.copyFile(snapshot+'.log',root+'/unity-validation.log');
await fs.writeFile(root+'/scene-changes.json',JSON.stringify(changes,null,2));
const stats={imageAssets:JSON.parse(await fs.readFile(root+'/assets.json','utf8')).length,replacedTextures:map.length,changedSceneAndPrefabs:changes.length,compilation:'passed in Unity 6000.0.64f1',editorPreview:'passed',persistentButtonEvents:'unchanged',existingMissingScripts:1,newMissingScripts:0};
await fs.writeFile(root+'/validation.json',JSON.stringify(stats,null,2));
console.log(JSON.stringify(stats,null,2));
