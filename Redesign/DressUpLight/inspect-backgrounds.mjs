import fs from 'node:fs';
for (const [file,root] of [['Assets/_Game/Prefabs/tst/PlayPanel.prefab','6644438631481190680'],['Assets/_Game/Scenes/_Game.unity','1407826322']]) {
 const blocks=fs.readFileSync(file,'utf8').split(/(?=^--- !u!)/m);
 const byId=new Map(blocks.map(b=>[b.match(/^--- !u!\d+ &(\d+)/)?.[1],b]));
 const transforms=blocks.filter(b=>/^--- !u!224 /.test(b));
 const byGo=new Map(transforms.map(b=>[b.match(/m_GameObject: \{fileID: (\d+)/)?.[1],b]));
 function show(go,depth=0){const obj=byId.get(go)||'',t=byGo.get(go)||'';console.log(' '.repeat(depth)+go+' '+obj.match(/m_Name: (.*)/)?.[1]);
  for(const c of [...obj.matchAll(/component: \{fileID: (\d+)/g)].map(m=>byId.get(m[1])||'')) {
   const script=c.match(/m_Script: .*guid: (\w+)/)?.[1];const methods=[...c.matchAll(/m_MethodName: (.*)/g)].map(m=>m[1]);
   if(script)console.log(' '.repeat(depth+1)+script+' '+methods.join(','));
  }
  const kids=t.split('m_Children:')[1]?.split('m_Father:')[0]||'';
  for(const m of kids.matchAll(/fileID: (\d+)/g)){const child=byId.get(m[1])?.match(/m_GameObject: \{fileID: (\d+)/)?.[1];if(child)show(child,depth+2);}
 }
 console.log(file);show(root);
}
