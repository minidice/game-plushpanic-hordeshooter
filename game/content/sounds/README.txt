이 폴더에 <id>.ogg / .wav / .mp3 를 넣으면 같은 ID의 합성 효과음 대신 사용됩니다.
ID 목록: doc/sound-spec.md

현재 9종 + 발성 변형 3개는 prototypes/audio-lab/tools/release.py로 재현합니다.
변형은 SoundDef.Variants에 지정한 개수만 <id>_2.wav부터 읽습니다.
단발음은 직전 변형을 연속 선택하지 않습니다. Stream(id)/Studio는 대표 음원을 조회합니다.
WAV 가져오기: PCM16, compress/mode=0, normalize=false. 제작된 음량을 유지합니다.
credits.json에 배포 파일 해시, 사용 원본, 제작자, 출처, CC0 라이선스가 있습니다.
게임 오버/클리어는 프로젝트에서 직접 만든 합성 악구입니다.
