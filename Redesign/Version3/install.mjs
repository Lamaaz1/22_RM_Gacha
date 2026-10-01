import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
const root='Redesign/Version3',target='Assets/Scenes/ui';
const report=JSON.parse(await fs.readFile(root+'/staged-validation.json','utf8'));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const row of report.assets){const current=await fs.readFile(path.join(target,row.file)),backup=await fs.readFile(path.join(root,'backups/ui',row.file));if(sha(current)!==sha(backup)&&sha(current)!==row.sha256)throw Error('Image changed since backup: '+row.file);}
for(const row of report.assets)await fs.copyFile(path.join(root,'ready',row.file),path.join(target,row.file));
async function files(dir,prefix=''){let out=[];for(const d of await fs.readdir(dir,{withFileTypes:true})){const rel=prefix+d.name;if(d.isDirectory())out.push(...await files(path.join(dir,d.name),rel+'/'));else out.push(rel);}return out;}
let unchanged=0,shapes=0;
for(const file of await files(root+'/backups/ui')){
 const a=await fs.readFile(path.join(root,'backups/ui',file)),b=await fs.readFile(path.join(target,file));
 if(!file.endsWith('.png')){if(sha(a)!==sha(b))throw Error('Metadata or animation changed: '+file);unchanged++;}
 if(file.startsWith('Onboarding/ui-')&&file.endsWith('.png')){if(sha(a)!==sha(b))throw Error('Tintable shape modified: '+file);shapes++;}
}
const scene='Assets/_Game/Scenes/StartScene.unity';if(sha(await fs.readFile(scene))!==sha(await fs.readFile(root+'/backups/StartScene.unity')))throw Error('Scene changed since snapshot');
const controller=await fs.readFile('Assets/_Game/Scripts/GachaOnboardingController.cs','utf8'),prior=await fs.readFile(root+'/backups/GachaOnboardingController.cs','utf8');
if(controller.slice(controller.indexOf('    private void LateUpdate()'))!==prior.slice(prior.indexOf('    private void LateUpdate()')))throw Error('Motion or safe-area implementation changed');
await fs.writeFile(root+'/validation.json',JSON.stringify({...report,installedFolder:path.resolve(target),nonImageFilesUnchanged:unchanged,tintableShapesUnchanged:shapes,startSceneUnchanged:true,motionCodeUnchanged:true,unityPreviewVerified:false},null,2));
console.log(JSON.stringify({installed:report.images,nonImageFilesUnchanged:unchanged,tintableShapesUnchanged:shapes,startSceneUnchanged:true,motionCodeUnchanged:true}));
