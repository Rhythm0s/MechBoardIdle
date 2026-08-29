using System.Collections.Generic;
using System.IO;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// balance_v4.json(1차 계약)을 읽어 BalanceConfig.asset + 노드 SO 7종을 생성/갱신한다.
    /// 메뉴: MBI/Generate Balance + Nodes.
    ///
    /// - 수치 원천은 json(§9). 코드에 밸런스 리터럴을 두지 않는다(§3) — 앵커는 json에서 복사.
    /// - 노드별 전력/탄약/발열 실측치는 아직 없음(balance.json은 합계 병목치만, 전부 TBD).
    ///   → 단일 소유가 자연스러운 항목만 placeholder로 주입하고 전부 confirm=Tbd로 마킹(§7 오표기 방지).
    /// - 재실행 시 같은 경로 자산을 덮어써 GUID를 보존(참조 안정).
    /// </summary>
    public static class BalanceAssetGenerator
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string NodesDir = SoRoot + "/Nodes";
        private const string ConfigPath = SoRoot + "/BalanceConfig.asset";

        /// <summary>회피 스택 상한 3 — 확정치(전투 문서 11-9장). MBI.Core와 같은 값이라야 한다.</summary>
        private const float DodgeStackLimit = 3f;

        [MenuItem("MBI/Generate Balance + Nodes")]
        public static void Generate()
        {
            BalanceJson json = BalanceJsonLoader.Load();

            EnsureDir(SoRoot);
            EnsureDir(NodesDir);

            BalanceConfig config = BuildConfig(json);
            BuildNodes(config, json);

            // 미확정치 SO는 만들기만 하고 값은 덮어쓰지 않는다 — 생성기 재실행이 조정값을 지우면 안 된다.
            LoadOrCreate<EconomyConfig>(SoRoot + "/EconomyConfig.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MBI] 밸런스/노드 자산 생성 완료 — 원천 {json.meta.schemaVersion} " +
                      $"({json.meta.exportedAt}). BalanceConfig + 노드 7종(구현 6 + 쉴드 스텁).");
        }

        // ---- BalanceConfig: json 앵커의 단일 원천 미러 ----
        private static BalanceConfig BuildConfig(BalanceJson json)
        {
            BalanceConfig c = LoadOrCreate<BalanceConfig>(ConfigPath);

            c.schemaVersion = json.meta.schemaVersion;
            c.exportedAt = json.meta.exportedAt;

            c.origin = json.Param("origin");
            c.ceil = json.Param("ceil");
            c.enh = json.Param("enh");

            c.enhBand = new Vector2(json.enhance.enhBand[0], json.enhance.enhBand[1]);
            c.snapBand = json.enhance.snapBand;
            c.s3Break = json.enhance.s3Break;
            c.s4Band = new Vector2(json.enhance.s4Band[0], json.enhance.s4Band[1]);
            c.s4Cost = json.enhance.s4Cost;

            c.challengeTime = json.Stage("S1").challengeTime;

            // 경제 항목 중 확정치는 상한 하나뿐. 계수·기본 시급은 confirmed:false라 여기 두지 않는다
            // (생성기가 덮어써서 인스펙터 조정이 날아가는 것을 막는다 — EconomyConfig 참조).
            if (json.economy != null && json.economy.offline != null && json.economy.offline.capHours > 0f)
                c.offlineCapHours = json.economy.offline.capHours;

            c.storeCapacity = json.Param("store"); // 확정치 40 — 재고 단일 층의 용량

            // 탄종별 생산(V02 §1 확정). 노드당 생산과 라인 스펙은 별개 축이다 —
            // 소비 상한 capA는 여기 오지 않는다.
            c.muniPerNode = json.Param("muniPerNode");
            c.lineSpecShots = new Vector3(json.Param("specA0"), json.Param("specA1"), json.Param("specA2"));

            // 드론(로봇 B) 확정치. 등가선이 여기서 닫힌다 — pB × dB = 1.0 × 100 = 100.
            c.droneSlots = Mathf.RoundToInt(json.Param("slot"));
            c.droneReleaseRate = json.Param("r");
            c.droneCharge = json.Param("dB");
            c.droneInflow = json.Param("pB");

            EditorUtility.SetDirty(c);
            return c;
        }

        // ---- 노드 7종 ----
        private static void BuildNodes(BalanceConfig config, BalanceJson json)
        {
            // 단일 소유가 자연스러운 병목 항목만 placeholder로 끌어온다(전부 Tbd).
            float pwc = json.Param("pwc");    // 발전 용량 → 에너지
            // 군수 노드 1개당 생산(발/초) — 확정치 1 (2026-08-25).
            // ⚠️ 여기에 capA(마운트 소비 상한 6)를 넣던 시기가 있었다. capA는 **소비 천장**이라
            // 노드 하나가 상한을 다 채워 두 번째 노드부터 출력 영향이 0이 됐다(CLAUDE.md §7 등재).
            float muniPerNode = json.Param("muniPerNode");
            // 드론 유입(기/초). params pB = 1.0 확정치 — 드론 몸체 조합표의 산출 속도.
            float droneInflow = config.droneInflow;
            float heat = json.Param("heat");  // 발열 합 → 가공
            float heatc = json.Param("heatc"); // 냉각 임계 → 가공
            float pw = json.Param("pw");      // 전력 소비 합(집계) → 코어에 lumped placeholder

            // 코어 — 물류 허브(탄약·전력 소비, 물류 산출)
            WriteNode(config, "core", "코어", NodeType.Core, true,
                new NodeResourceProfile { powerDraw = pw, confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo),
                    new NodePort(PortFace.South, PortIO.Input, FlowKind.Power),
                    new NodePort(PortFace.North, PortIO.Output, FlowKind.Material),
                });

            // 가공 — 물류 품목 처리(발열 발생원)
            WriteNode(config, "proc", "가공", NodeType.Processing, true,
                new NodeResourceProfile { heatGenerate = heat, heatDissipate = heatc, confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Material),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.Material),
                });

            // 군수 — 조합표 4종 중 **하나**를 돌린다(260827_V01 §3).
            // 갈래를 늘리는 방법은 노드를 더 놓는 것이지 노드 하나를 넓히는 것이 아니므로,
            // 출력 포트는 단일이고 산출 종류는 선택된 조합표가 정한다.
            // 스택 상한은 미확정(조립 「품목과 재고」 신설 중) — 0 = 미설정 센티넬로 두고 하드코딩하지 않는다.
            WriteNode(config, "muni", "군수", NodeType.Munitions, true,
                new NodeResourceProfile { ammoProduce = muniPerNode, confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Material),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
                },
                new List<NodeRecipe>
                {
                    new NodeRecipe { kind = RecipeKind.Ammo, displayName = "탄약",
                        output = FlowKind.Ammo, outputPerSec = muniPerNode,
                        stackLimitTbd = 0f, implemented = true },
                    new NodeRecipe { kind = RecipeKind.DroneBody, displayName = "드론 몸체",
                        output = FlowKind.Drone, outputPerSec = droneInflow,
                        stackLimitTbd = 0f, implemented = true },
                    // 쉴드 재료는 자리만 — 쉴드 발생 노드가 범위 밖이다(§4).
                    new NodeRecipe { kind = RecipeKind.ShieldMaterial, displayName = "쉴드 재료",
                        output = FlowKind.Material, outputPerSec = 0f,
                        stackLimitTbd = 0f, implemented = false },
                    // 추진제 — 회피 1회분. 주기 15초/1개는 **선언치**(시뮬 실측 후 확정)이고
                    // 스택 상한 3은 확정치다(회피 스택 상한과 같은 값 — 부스터가 그 이상 못 든다).
                    new NodeRecipe { kind = RecipeKind.Propellant, displayName = "추진제",
                        output = FlowKind.Propellant, outputPerSec = 1f / 15f,
                        stackLimitTbd = DodgeStackLimit, implemented = true },
                });

            // 에너지 — 발전(전력 공급)
            WriteNode(config, "ener", "에너지", NodeType.Energy, true,
                new NodeResourceProfile { powerSupply = pwc, confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.Power),
                });

            // 저장 — 창고(버퍼). 군수 → 벨트 → 저장 → 벨트 → 마운트 소비.
            // ⚠️ 2026-08-21 정정: 포트 kind가 Material이라 군수(Ammo) 출력과 FlowKind가 안 맞아
            // 벨트 연결 자체가 성립하지 않았다. 조립 시스템 문서「노드 종류」표의 저장노드 행이
            // 변환 노드 틀에 맞춰져 있어 "입력 없음"으로 읽힌 데서 온 결함이다.
            WriteNode(config, "stor", "저장", NodeType.Storage, true,
                new NodeResourceProfile { confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
                });

            // 부스터 — 추진제를 받아 회피 스택을 공급(2026-08-29 신설, 노드 7종).
            // 쉴드 발생 노드와 같은 **무형 자원 공급 계열**이라 마운트 같은 별도 소비 장치가 없다 —
            // 노드가 입력을 받아 그 자리에서 소비하고 보드 위 노드라 자기 버퍼를 이미 갖는다.
            // 회피를 늘리는 방법은 **부스터를 더 놓는 것**이다 — 한 대가 드는 것은 3회에서 멈춘다.
            WriteNode(config, "boost", "부스터", NodeType.Booster, true,
                new NodeResourceProfile { powerDraw = 0f, confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Propellant),
                });

            // 쉴드 발생 — 스키마 자리만(구현 보류, §4). implemented=false, 포트 없음.
            WriteNode(config, "shield", "쉴드 발생", NodeType.Shield, false,
                new NodeResourceProfile { confirm = ConfirmState.Tbd },
                new List<NodePort>());
        }

        private static void WriteNode(BalanceConfig config, string id, string display, NodeType type,
            bool implemented, NodeResourceProfile resources, List<NodePort> ports,
            List<NodeRecipe> recipes = null)
        {
            string path = $"{NodesDir}/Node_{id}.asset";
            NodeDefinition n = LoadOrCreate<NodeDefinition>(path);
            n.nodeId = id;
            n.displayName = display;
            n.type = type;
            n.implemented = implemented;
            n.resources = resources;
            n.ports = ports;
            // 조합표를 안 주는 노드는 빈 목록 — 「레시피 선택」이 없는 노드도 있다(코어·에너지·저장).
            n.recipes = recipes ?? new List<NodeRecipe>();
            n.balanceRef = config;
            EditorUtility.SetDirty(n);
        }

        // ---- 유틸 ----
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureDir(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
