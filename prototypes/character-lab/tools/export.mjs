import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { createCharacter, species, defaults, makeClips } from '../src/model.mjs';
globalThis.FileReader=class{readAsArrayBuffer(blob){blob.arrayBuffer().then(b=>{this.result=b;this.onloadend?.();});}readAsDataURL(blob){blob.arrayBuffer().then(b=>{this.result=`data:${blob.type};base64,${Buffer.from(b).toString('base64')}`;this.onloadend?.();});}};
const dir=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../output');await fs.mkdir(dir,{recursive:true});
for(const equipped of [false,true]){
  const scene=new THREE.Scene();const characters=species.map((s,i)=>{const c=createCharacter(s,defaults[s]);c.root.position.x=(i-1.5)*2.6;scene.add(c.root);return c;});
  const clips=makeClips(characters,equipped);scene.userData={version:2,characters:defaults};
  const binary=await new GLTFExporter().parseAsync(scene,{binary:true,animations:clips,onlyVisible:true});
  const filename=`characters-${equipped?'equipped':'base'}.glb`;await fs.writeFile(path.join(dir,filename),Buffer.from(binary));console.log(`${filename}: ${binary.byteLength} bytes, ${clips.length} clips`);
}
await fs.writeFile(path.join(dir,'character-proportions.json'),JSON.stringify({version:2,characters:defaults},null,2));
