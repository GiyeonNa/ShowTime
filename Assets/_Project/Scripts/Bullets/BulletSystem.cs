using UnityEngine;

namespace ShowTime
{
    /// <summary>
    /// M4: 탄막 시스템. 시뮬레이션(구조체 배열)과 렌더링(모드 선택)을 분리했다.
    ///
    /// - Instanced: Graphics.RenderMeshInstanced — 살아있는 탄환 전부를 드로우콜 1회로.
    /// - NaiveGameObjects: 탄환마다 GameObject+MeshRenderer — before/after 실측의 대조군.
    ///   (SRP Batcher가 셋패스는 줄여주지만 드로우콜 자체는 탄환 수만큼 발생한다)
    ///
    /// 시간은 scaled deltaTime 사용 — 히트스탑 때 탄막도 함께 멎는 것이 연출 의도.
    /// </summary>
    public sealed class BulletSystem : MonoBehaviour
    {
        public enum BulletRenderMode { Instanced, NaiveGameObjects }

        [Header("렌더 모드 (실측 비교용)")]
        public BulletRenderMode mode = BulletRenderMode.Instanced;

        [Header("리소스")]
        public Mesh mesh;
        public Material material;

        [Header("발사 원점 (선택 — 비었으면 자기 위치. M5: Spine 총구 본 추적)")]
        public Transform muzzle;

        [Header("탄막 파라미터")]
        [Min(1)] public int maxBullets = 600;
        public float emitPerSecond = 450f;
        public float speed = 9f;
        public float speedJitter = 3.5f;
        public float spreadDegrees = 26f;
        public float lifetime = 1.35f;
        public float size = 0.14f;

        struct Bullet
        {
            public Vector3 position;
            public Vector3 velocity;
            public float age;
        }

        Bullet[] _bullets;
        Matrix4x4[] _matrices;
        GameObject[] _pool; // Naive 모드 전용 (지연 생성)
        RenderParams _renderParams;
        Quaternion _facing;
        int _alive;
        float _emitAccum;
        float _burstRemaining;

        /// <summary>살아있는 탄환 수 — PerfProbe가 기록한다.</summary>
        public int AliveCount => _alive;

        /// <summary>duration초 동안 탄막 발사 (BulletBurstReceiver가 마커로 호출).</summary>
        public void Burst(float duration) => _burstRemaining = duration;

        void Awake()
        {
            _bullets = new Bullet[maxBullets];
            _matrices = new Matrix4x4[maxBullets];
            _renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f), // 컬링 생략 (한 화면 데모)
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
            };
            // 카메라 고정 데모 — 빌보드 회전은 한 번만 계산
            var cam = Camera.main;
            _facing = cam != null ? cam.transform.rotation : Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // 1) 발사
            if (_burstRemaining > 0f)
            {
                _burstRemaining -= dt;
                _emitAccum += emitPerSecond * dt;
                while (_emitAccum >= 1f && _alive < maxBullets)
                {
                    _emitAccum -= 1f;
                    Spawn();
                }
            }

            // 2) 적분 + 수명 (swap-remove — 순서 보존 불필요, 할당 0)
            for (int i = _alive - 1; i >= 0; i--)
            {
                ref var b = ref _bullets[i];
                b.age += dt;
                if (b.age >= lifetime)
                {
                    _bullets[i] = _bullets[--_alive];
                    continue;
                }
                b.position += b.velocity * dt;
            }

            // 3) 렌더
            if (mode == BulletRenderMode.Instanced) RenderInstanced();
            else RenderNaive();
        }

        void Spawn()
        {
            // 부채꼴 스프레드: 진행 방향(+X) 기준 yaw/pitch 랜덤
            float yaw = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees * 0.4f, spreadDegrees * 0.4f);
            Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * transform.right;

            _bullets[_alive++] = new Bullet
            {
                position = muzzle != null ? muzzle.position : transform.position,
                velocity = dir * (speed + Random.Range(-speedJitter, speedJitter)),
                age = 0f,
            };
        }

        void RenderInstanced()
        {
            if (_pool != null) SetPoolActive(0); // 모드 전환 직후 잔여 GO 정리
            if (_alive == 0 || mesh == null || material == null) return;

            var scale = new Vector3(size, size, size);
            for (int i = 0; i < _alive; i++)
                _matrices[i] = Matrix4x4.TRS(_bullets[i].position, _facing, scale);

            // 살아있는 탄환 전부 = 드로우콜 1회 (RenderMeshInstanced 호출당 최대 1023 인스턴스)
            Graphics.RenderMeshInstanced(_renderParams, mesh, 0, _matrices, _alive);
        }

        void RenderNaive()
        {
            if (_pool == null) _pool = new GameObject[maxBullets];

            var scale = new Vector3(size, size, size);
            for (int i = 0; i < _alive; i++)
            {
                if (_pool[i] == null) _pool[i] = CreatePooledBullet(i);
                var t = _pool[i].transform;
                t.SetPositionAndRotation(_bullets[i].position, _facing);
                t.localScale = scale;
            }
            SetPoolActive(_alive);
        }

        GameObject CreatePooledBullet(int index)
        {
            var go = new GameObject("Bullet_" + index);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void SetPoolActive(int count)
        {
            if (_pool == null) return;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] == null) continue; // 아직 안 만든(또는 파괴된) 슬롯은 건너뜀
                bool want = i < count;
                if (_pool[i].activeSelf != want) _pool[i].SetActive(want);
            }
        }
    }
}
