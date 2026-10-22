// Catalog records for the generated model roster.
import fs from 'node:fs/promises';
const game=new URL('../../../game/',import.meta.url);
const players=['goose','duck','raccoon','redpanda'];
for(const [index,kind] of players.entries()) {
  const hand={goose:'0.95, 0.94, 0.9',duck:'0.73, 0.47, 0.2',raccoon:'0.19, 0.22, 0.23',redpanda:'0.22, 0.18, 0.16'}[kind];
  const text=`[gd_resource type="Resource" script_class="CharacterDefinition" load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/Characters/CharacterDefinition.cs" id="1_def"]
[ext_resource type="PackedScene" path="res://assets/characters/${kind}/${kind}-fitted.glb" id="2_model"]

[resource]
script = ExtResource("1_def")
Id = "${kind}"
DisplayName = "name_character_${kind}"
Description = "desc_character_${kind}"
Role = 0
SortOrder = ${index}
Model = ExtResource("2_model")
ModelFacesPositiveZ = true
ModelScale = 1.0
BodyNode = "${kind}_body"
DeformNode = "${kind}_deform"
HeadNode = "${kind}_head"
LeftFootNode = "${kind}_leftFoot"
RightFootNode = "${kind}_rightFoot"
WeaponNode = "${kind}_weapon"
MuzzleNode = "Muzzle"
GearNodes = PackedStringArray("${kind}_helmet", "${kind}_beret", "${kind}_vest", "${kind}_pack", "${kind}_goggles", "${kind}_headset", "${kind}_vestPadded")
BuiltInWeaponNode = "${kind}_weapon_mesh"
HandColor = Color(${hand}, 1)
ColliderRadius = 0.48
ColliderHeight = ${kind==='goose'?'2.18':'2.06'}
MoveSpeed = 4.4
MaxHealth = 100
StepRate = 13.0
WaddleDegrees = 6.0
StuffingColor = Color(1, 0.97, 0.88, 1)
`;
  await fs.writeFile(new URL(`content/characters/${kind}.tres`,game),text);
}
// Monster speed and health come from game/content/data/monsters.csv, so enemy looks carry no stats.
for(const [kind,radius,height] of [['humanoid',.40,1.8],['crawler',.43,.9],['tether',.40,1.8],['leaper',.48,1.5],['spitter',.52,1.8]]) {
  await fs.writeFile(new URL(`content/enemies/${kind}.tres`,game),`[gd_resource type="Resource" script_class="CharacterDefinition" load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/Characters/CharacterDefinition.cs" id="1_def"]
[ext_resource type="PackedScene" path="res://assets/enemies/${kind}/${kind}.glb" id="2_model"]

[resource]
script = ExtResource("1_def")
Id = "${kind}"
DisplayName = "name_monster_${kind}"
Description = "Authored infected silhouette with runtime object pivots."
Role = 1
Model = ExtResource("2_model")
ModelFacesPositiveZ = true
BodyNode = "Body"
DeformNode = "Deform"
HeadNode = "Head"
LeftFootNode = "LeftFoot"
RightFootNode = "RightFoot"
WeaponNode = "Weapon"
MuzzleNode = "Muzzle"
ColliderRadius = ${radius}
ColliderHeight = ${height}
StepRate = ${kind==='crawler'?17:13}.0
WaddleDegrees = ${kind==='crawler'?4:7}.0
StuffingColor = Color(0.77, 0.85, 0.60, 1)
`);
}
const localizations={eng:{goose:['Goose','A tall pale bird with a narrow bill and removable field gear.'],duck:['Duck','A warm brown bird with a broad bill and webbed feet.'],raccoon:['Raccoon','A masked woodland scout with a striped tail and dark paws.'],redpanda:['Red Panda','A red forest scout with cream face markings and a long ringed tail.']},kor:{goose:['거위','길고 좁은 부리와 밝은 깃털을 가진 거위. 장비를 자유롭게 바꿀 수 있습니다.'],duck:['오리','넓적한 부리와 물갈퀴 발을 가진 갈색 오리.'],raccoon:['라쿤','눈가의 검은 무늬와 줄무늬 꼬리를 가진 숲속 정찰병.'],redpanda:['랫서판다','밝은 얼굴 무늬와 풍성한 줄무늬 꼬리를 가진 붉은 정찰병.']}};
for(const [lang,entries] of Object.entries(localizations)) {
  const url=new URL(`content/lan/${lang}/character.csv`,game);
  let lines=(await fs.readFile(url,'utf8')).trimEnd().split(/\r?\n/);
  for(const [kind,[name,description]] of Object.entries(entries)) {
    for(const [key,value] of [[`name_character_${kind}`,name],[`desc_character_${kind}`,description]]) {
      const i=lines.findIndex(l=>l.startsWith(`${key},`)); const line=`${key},${value}`;
      if(i<0)lines.push(line);else lines[i]=line;
    }
  }
  await fs.writeFile(url,lines.join('\n')+'\n');
}
console.log('Wrote four playable definitions, five enemy definitions, and character localization.');
