import fs from 'node:fs/promises';
import path from 'node:path';
const project=path.resolve('.utmp/Version3Preview');
await fs.mkdir(path.join(project,'Assets/Editor'),{recursive:true});
await fs.mkdir(path.join(project,'Assets/TMP_RM/Prefabs'),{recursive:true});
await fs.mkdir(path.join(project,'Assets/_Game/Scripts'),{recursive:true});
await fs.mkdir(path.join(project,'Packages'),{recursive:true});
await fs.mkdir(path.join(project,'ProjectSettings'),{recursive:true});
const text=await fs.readFile('Assets/TMP_RM/Prefabs/BaseScene.prefab','utf8');
const chunks=text.split(/(?=^--- !u!)/m),names=new Map(),transforms=new Map(),goTransforms=new Map();
for(const c of chunks){const id=c.match(/^--- !u!(\d+) &(\d+)/);if(!id)continue;if(id[1]==='1')names.set(id[2],c.match(/^  m_Name: (.*)$/m)?.[1]?.trim());if(['224','4'].includes(id[1])){const go=c.match(/m_GameObject: \{fileID: (\d+)\}/)?.[1],father=c.match(/m_Father: \{fileID: (\d+)\}/)?.[1];transforms.set(id[2],{go,father});goTransforms.set(go,id[2]);}}
const root=[...names].find(([id,n])=>n==='Gacha Onboarding')?.[0];if(!root)throw Error('Missing flow');
const keep=new Set([root]);let t=goTransforms.get(root);while(t&&transforms.has(t)){const item=transforms.get(t);keep.add(item.go);t=item.father;}
for(const [go,rt] of goTransforms){let x=rt;while(x&&transforms.has(x)){const item=transforms.get(x);if(item.go===root){keep.add(go);break;}x=item.father;}}
const retained=chunks.filter(c=>{const id=c.match(/^--- !u!(\d+) &(\d+)/);return !id||(id[1]==='1'?keep.has(id[2]):keep.has(c.match(/m_GameObject: \{fileID: (\d+)\}/)?.[1]));});
const keepIds=new Set(retained.map(c=>c.match(/^--- !u!\d+ &(\d+)/)?.[1]).filter(Boolean));
const filtered=retained.map(c=>c.replace(/  - \{fileID: (\d+)\}\r?\n/g,(s,id)=>keepIds.has(id)?s:'')).join('');
await fs.writeFile(path.join(project,'Assets/TMP_RM/Prefabs/BaseScene.prefab'),filtered);
await fs.copyFile('Assets/TMP_RM/Prefabs/BaseScene.prefab.meta',path.join(project,'Assets/TMP_RM/Prefabs/BaseScene.prefab.meta'));
await fs.cp('Assets/Scenes/ui',path.join(project,'Assets/Scenes/ui'),{recursive:true});
await fs.cp('Assets/TextMesh Pro',path.join(project,'Assets/TextMesh Pro'),{recursive:true});
for(const f of ['GachaOnboardingController.cs','GachaOnboardingController.cs.meta'])await fs.copyFile('Assets/_Game/Scripts/'+f,path.join(project,'Assets/_Game/Scripts',f));
let validation=await fs.readFile('Assets/Editor/GachaOnboardingValidation.cs','utf8');
validation=validation.replace('string directory = "Redesign/Onboarding";',`string directory = @"${path.resolve('Redesign/Version3').replaceAll('\\','/')}";`);
await fs.writeFile(path.join(project,'Assets/Editor/GachaOnboardingValidation.cs'),validation);
await fs.copyFile('ProjectSettings/ProjectVersion.txt',path.join(project,'ProjectSettings/ProjectVersion.txt'));
// Separate identity prevents this isolated check from touching the game's PlayerPrefs.
await fs.writeFile(path.join(project,'ProjectSettings/ProjectSettings.asset'),'%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n  companyName: CodexPreview\n  productName: GachaVersion3Preview\n  defaultScreenWidth: 1920\n  defaultScreenHeight: 1080\n  runInBackground: 1\n');
const cache=await fs.readdir('Library/PackageCache'),ugui=cache.find(f=>f.startsWith('com.unity.ugui@'));if(!ugui)throw Error('No cached UGUI');
await fs.writeFile(path.join(project,'Packages/manifest.json'),JSON.stringify({dependencies:{'com.unity.ugui':'file:'+path.resolve('Library/PackageCache',ugui).replaceAll('\\','/'),'com.unity.modules.ui':'1.0.0','com.unity.modules.imgui':'1.0.0','com.unity.modules.imageconversion':'1.0.0','com.unity.modules.jsonserialize':'1.0.0','com.unity.modules.physics':'1.0.0','com.unity.modules.physics2d':'1.0.0'}},null,2));
console.log(JSON.stringify({project,retainedObjects:keep.size,scripts:[...new Set(filtered.match(/m_Script:.*$/gm))]}));
