using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mophia.UI
{
    /// <summary>
    /// 全シーン共通の背景演出: 画面下から上へゆっくり昇っていく小さな黒いパーティクル。
    /// ゲーム起動時に一度だけ生成し、DontDestroyOnLoadでシーンをまたいで居続ける。
    ///
    /// レターボック導入前はカメラの背景として直接見せていたが、レターボック化で各シーンの
    /// 「白背景」が不透明なUI画像(LetterboxRoot)になったため、カメラ描画のままだと完全に
    /// 隠れてしまう。そのため専用カメラでパーティクルだけを透過RenderTextureに描画し、
    /// 各シーンのCanvas内にある"ParticleDisplay"という名前のRawImage(UIBuild.ApplyLetterboxが
    /// 白背景の直後・実コンテンツの手前に自動で用意する)に映し出す方式にしている。
    ///
    /// 常時ライブに動かす軽量演出のため、パーティクル数は控えめに抑えている。
    /// GamePlay画面はプレイの妨げになるため非表示にする。
    /// </summary>
    public static class BackgroundParticles
    {
        private const string HiddenInSceneName = "GamePlay";
        private const string DisplayObjectName = "ParticleDisplay";
        private const string ParticleLayerName  = "BackgroundParticles"; // ProjectSettings > Tags and Layers に登録済み

        private static bool           _created;
        private static GameObject     _particleGo;
        private static RenderTexture  _renderTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created) return;
            _created = true;

            // depth bitsを0にすると(Unity 6のレンダーグラフAPI上で)カメラの出力先として正しく
            // 扱われず警告が出て描画されないため、深度バッファ付きで作成する
            _renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { name = "BackgroundParticlesRT" };

            CreateParticleSystem();
            CreateRenderCamera();

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToScene(SceneManager.GetActiveScene());
        }

        private static void CreateParticleSystem()
        {
            var go = new GameObject("BackgroundParticles") { layer = LayerMask.NameToLayer(ParticleLayerName) };
            _particleGo = go;
            Object.DontDestroyOnLoad(go);
            // 専用カメラを(0,0,0)・forward=+Zに置くので、その前方に配置する
            go.transform.position = new Vector3(0f, -6f, 10f);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop              = true;
            main.playOnAwake       = true;
            main.simulationSpace   = ParticleSystemSimulationSpace.World;
            main.startLifetime     = 7f;
            main.startSpeed        = 0f; // 上昇はvelocityOverLifetimeで与える
            main.startSize         = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor        = new Color(0f, 0f, 0f, 1.0f);
            main.maxParticles      = 60;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(24f, 0.1f, 0.1f);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space   = ParticleSystemSimulationSpace.World;
            // x/y/zは全て同じモード(TwoConstants)で揃える必要がある(片方だけ既定値のままだと
            // "Particle Velocity curves must all be in the same mode" エラーになる)
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f,   0f),
                    new GradientAlphaKey(0.8f, 0.15f),
                    new GradientAlphaKey(0.8f, 0.7f),
                    new GradientAlphaKey(0f,   1f),
                });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // このプロジェクトはURP(2Dレンダラー)。Particles/Standard UnlitはBuilt-in RP用のシェーダーで
            // 2Dレンダラーでは正しく描画されないことがあるため、URPの2Dレンダラーで確実に使えるSprites/Defaultを使う
            Shader shader = Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);

            ps.Play();
        }

        private static void CreateRenderCamera()
        {
            var camGo = new GameObject("BackgroundParticlesCamera");
            Object.DontDestroyOnLoad(camGo);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane    = 0.3f;
            cam.farClipPlane     = 20f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0f, 0f, 0f, 0f); // 透過(RawImageの上に白背景が透けて見えるように)
            cam.cullingMask      = LayerMask.GetMask(ParticleLayerName);
            cam.targetTexture    = _renderTexture;
            cam.depth            = -100; // どの画面にも直接は描画しない(RenderTexture行き専用)
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToScene(scene);
        }

        private static void ApplyToScene(Scene scene)
        {
            bool visible = scene.name != HiddenInSceneName;
            if (_particleGo != null) _particleGo.SetActive(visible);
            if (!visible) return;

            GameObject displayGo = GameObject.Find(DisplayObjectName);
            if (displayGo == null)
            {
                Debug.LogWarning($"[BackgroundParticles] シーン'{scene.name}'に{DisplayObjectName}が見つかりません(レターボック未対応、または未再生成)");
                return;
            }

            RawImage display = displayGo.GetComponent<RawImage>();
            if (display == null)
            {
                Debug.LogWarning($"[BackgroundParticles] {DisplayObjectName}にRawImageがありません");
                return;
            }

            display.texture = _renderTexture;
            Debug.Log($"[BackgroundParticles] シーン'{scene.name}'の{DisplayObjectName}にRenderTextureを割り当てました");
        }
    }
}
