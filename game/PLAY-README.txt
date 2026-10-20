Horde Shooter (prototype) — Windows

Run HordeShooter.exe. Keep every file in this folder together (the .pck and the data_ folder are the game).

Two players
- Any PC with this build: Multiplayer → Create Room puts the room on the room list; others pick it there and press Join.
  Rooms from another version are shown as "Other version" and cannot be joined: use the same build.
  From another home the host router must let UDP 24680 in (the game tries to open it; otherwise forward it once).
- Same router, no list: one PC opens Multiplayer → Create Room. The screen shows the address (192.168.x.x).
  The other PC opens Multiplayer, types that address, presses Join.
  Addresses starting with 127. always mean "this PC": use the 192.168.x.x shown on the host.
- In the room the member presses Ready, then the host presses Start. Enter in the match (host) takes everyone back to the room.
- If Windows Firewall asks on the hosting PC, allow it on private networks (UDP 24680).
- Both PCs need the same version of this folder.

두 명이서 하기
- 이 빌드를 가진 PC끼리: 멀티 → 방 만들기를 하면 방 목록에 올라갑니다. 다른 사람은 목록에서 골라 [참가].
  다른 버전의 방은 "다른 버전"으로 보이고 들어갈 수 없습니다. 같은 빌드를 쓰세요.
  다른 집에서 들어오려면 방장 공유기의 UDP 24680이 열려야 합니다(게임이 자동으로 열기를 시도, 안 되면 포트포워딩 한 번).
- 목록 없이 같은 공유기: 한 PC에서 멀티 → 방 만들기. 화면에 주소(192.168.x.x)가 나옵니다.
  다른 PC에서 멀티 → 그 주소 입력 → 참가.
  127.로 시작하는 주소는 언제나 "이 PC"입니다. 방장 화면의 192.168.x.x를 넣으세요.
- 방에서 파티원이 [준비] → 방장이 [시작]. 게임 중 방장이 Enter(재시작)를 누르면 모두 방으로 돌아갑니다.
- 방을 만든 PC에서 Windows 방화벽이 물으면 개인 네트워크 허용 (UDP 24680).
- 두 PC 모두 같은 버전의 이 폴더가 필요합니다.
