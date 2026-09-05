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

        // ---- 노드 대당 부하(조립 시스템 문서「노드 종류」 부하 열, 260829_V03 §판정①) ----
        //
        // ⚠️ **대당 값이 원천이고 네트워크 합계는 파생값이다.** 종전에는 balance_v4의
        // 합계(pw 66 / heat 8 / heatc 12)를 노드 **한 대**에 통째로 얹어 두었는데,
        // 그러면 노드를 늘려도 부하가 안 늘어 「비용을 내고 놓는다」가 성립하지 않는다.
        // 원점 구성(코어1·가공2·군수1·에너지1)의 합은 2×1 + 1×2 = 4/초다 — 66은 어디서도 안 나온다.
        // 노드 대당 전력 7종 — 밸런스 문서「노드 대당 값과 모듈 부하」확정(260901_V02 §2층).
        // ⚠️ 원천은 **밸런스 문서 하나다.** 조립 시스템 문서의 부하 열은 낡았고 정정 후에 넘어온다.
        private const float CorePowerDraw = 0f;    // 코어 — 소비처지 생산자가 아니다
        private const float ProcPowerDraw = 1f;    // 가공 — 물질을 바꾸는 자리

        // 가공 대당 산출(개/초). 밸런스 문서「노드 생산력과 레시피」의 「노드 1대 산출 1개/초」다.
        // ⚠️ 2026-09-04 레시피 개정으로 **필요 생산치가 재산출 대상**이 됐다(`260904_W02` 6장).
        // 확정될 때까지 문서에 있던 값을 그대로 쓰되 이름으로 미확정임을 남긴다.
        private const float ProcOutputPerSecTbd = 1f;
        private const float MuniPowerDraw = 2f;    // 군수 — 만들기도 하고 나르기도 한다
        private const float EnergyPowerDraw = 1f;  // 에너지 — 내는 쪽도 자기 몫을 먹는다
        private const float StoragePowerDraw = 2f; // 저장 — 쌓아둘 뿐 아무것도 바꾸지 않는다
        private const float BoosterPowerDraw = 2f; // 부스터 — 스택이 차면 멈춘다(일감률 0)
        private const float ShieldPowerDraw = 1f;  // 쉴드 발생 — 일곱 종 중 유일하게 발열이 공백

        /// <summary>
        /// 에너지 노드 **대당** 발전량 10/초 — 확정(260901_V02 §2층, 구 잠정치 5 폐기).
        ///
        /// ⚠️ 종전에는 `params.pwc`(발전 용량 **합**)를 대당 공급으로 쓰고 있었다.
        /// 한 대가 80을 공급하니 전력이 모자랄 일이 없었고, **전력 축이 한 번도 작동한 적이 없다.**
        /// </summary>
        private const float EnergyPowerSupply = 10f;

        // ⚠️ **발열은 코드에 넣지 않는다**(260901_V02 §2층 「적용 경계」). 확정치는 7종 다 있으나
        // 냉각 수단이 코드에 없는 상태에서 발열만 올리면 대응할 방법이 없는 벌이 된다.
        // 모듈 시스템 구현과 함께 가며 그것은 영상 이후다. 아래 1은 **종전 값 그대로**이고
        // 확정치(에너지 4)가 아니다 — 확정치는 문서에만 있다.
        private const float EnergyHeat = 1f;

        /// <summary>
        /// 추진제 **아이템**의 최대 스택 3 — 확정치. 마운트 한 칸에 3개까지 쌓인다.
        /// ⚠️ 회피 스택 상한과 **다른 축**이다(260829_V02): 그쪽은 부스터 대수 × 2다.
        /// 숫자가 3 근처라 섞이기 쉬워 이름을 갈라 둔다.
        /// </summary>
        private const float PropellantItemStack = 3f;

        /// <summary>추진제 산출 주기 — 15초에 1개. **선언치**이며 시뮬 실측 후 확정된다.</summary>
        private const float PropellantPerSec = 1f / 15f;

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
                      $"({json.meta.exportedAt}). BalanceConfig + 노드 7종(구현 6 + 쉴드 스텁, 대당 부하 반영).");
        }

        // ---- BalanceConfig: json 앵커의 단일 원천 미러 ----
        private static BalanceConfig BuildConfig(BalanceJson json)
        {
            BalanceConfig c = LoadOrCreate<BalanceConfig>(ConfigPath);

            c.schemaVersion = json.meta.schemaVersion;
            c.exportedAt = json.meta.exportedAt;

            c.origin = json.Param("origin");
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
            // ⚠️ 발전 **용량 합**이다. 에너지 노드 **대당** 발전량은 아직 원천에 없어
            // (260829_V03 미확정 5건 #1) 합계를 한 대에 얹어 둔 상태 그대로 둔다 —
            // 여기에 0 센티넬을 넣으면 전력 효율이 0이 되어 보드 전체가 멈춘다.
            // 대당 값이 오면 이 줄이 사라지고 상수 하나로 바뀐다.
            // ⚠️ `pwc`는 발전 용량 **합**이다. 대당 공급으로 쓰면 안 된다(260901_V02 판정 4).
            // 대당은 EnergyPowerSupply 확정치를 쓴다.
            float pwc = json.Param("pwc");    // 발전 용량 합 — 진단·문서 대조용
            // 군수 노드 1개당 생산(발/초) — 확정치 1 (2026-08-25).
            // ⚠️ 여기에 capA(마운트 소비 상한 6)를 넣던 시기가 있었다. capA는 **소비 천장**이라
            // 노드 하나가 상한을 다 채워 두 번째 노드부터 출력 영향이 0이 됐다(CLAUDE.md §7 등재).
            float muniPerNode = json.Param("muniPerNode");
            // 드론 유입(기/초). params pB = 1.0 확정치 — 드론 몸체 조합표의 산출 속도.
            float droneInflow = config.droneInflow;

            // 코어 — 물류 허브(탄약·전력 소비, 물류 산출). 고정비 0(확정).
            WriteNode(config, "core", "코어", NodeType.Core, true,
                new NodeResourceProfile { powerDraw = CorePowerDraw, confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    // ⚠️ **탄약 입력을 폐기했다** (2026-09-05 · `260904_W03` 1장).
                    // 코드에만 있던 것이다 — 조립 문서는 처음부터 「코어 = 입력 없음 · 모든
                    // 라인의 시작」이라 적고 있었고, 「코어가 탄약을 받는다」는 근거가 어느
                    // 문서에도 없었다. 도착지는 마운트 고정 포트다(W01 2-2).
                    new NodePort(PortFace.South, PortIO.Input, FlowKind.Power),
                    new NodePort(PortFace.North, PortIO.Output, FlowKind.CoreEnergy),
                },
                BuildRecipes(NodeType.Core, ProcOutputPerSecTbd));

            // 가공 — 물류 품목 처리. 전력 1/초(확정).
            // ⚠️ 가공의 **발열**은 부하 열에 없다. 표에 있는 발열원은 에너지 하나뿐이라
            // 0으로 두고 보고한다 — 차원이 비슷하다고 옛 합계(8)를 되넣지 않는다(§7 08-24).
            // 가공 — 조합표 셋 (2026-09-04 · `260904_W01` 3장).
            //
            // ⚠️ **어제까지 이 노드의 조합표가 0개였다.** 밸런스 문서「노드 생산력과 레시피」가
            // 「기초재료·부품」을 확정치로 갖고 있는데 코드에는 없었고, 그래서 탄약 체인의
            // 앞단이 통째로 비어 있었다(`260903_W04` 3장). 이 줄이 그 자리를 채운다.
            //
            // 입력면은 하나다. 세 조합표가 전부 1종을 먹으므로 면이 남지 않는다.
            WriteNode(config, "proc", "가공", NodeType.Processing, true,
                new NodeResourceProfile { powerDraw = ProcPowerDraw, heatGenerate = 0f,
                    confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.CoreEnergy),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.BasicParts),
                },
                BuildRecipes(NodeType.Processing, ProcOutputPerSecTbd));

            // 기초 군수 — 조합표 넷 중 **하나**를 돌린다 (2026-09-05 · `260904_W01` 3-2).
            //
            // 갈래를 늘리는 방법은 노드를 더 놓는 것이지 노드 하나를 넓히는 것이 아니므로
            // 출력 포트는 단일이고 산출 종류는 선택된 조합표가 정한다.
            //
            // 바뀐 것: 탄약 → **표준탄**(특수탄의 재료가 된다) · 쉴드 재료 → **방어 재료** ·
            // 드론 몸체 → **드론 몸체 부품**. 추진제만 발전재료를 먹고 나머지 셋은 부품을 먹는다.
            WriteNode(config, "muni", "기초 군수", NodeType.MunitionsBasic, true,
                new NodeResourceProfile { ammoProduce = muniPerNode, powerDraw = MuniPowerDraw,
                    confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.BasicParts),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.StandardAmmo),
                },
                BuildRecipes(NodeType.MunitionsBasic, muniPerNode));

            // 복합 군수 — 입력면 **둘** (2026-09-04 신설 · `260904_W01` 3장).
            //
            // 기초 군수와 갈린 이유는 입력면 수가 노드에 고정되기 때문이다. 레시피를 바꿔도
            // 면이 늘거나 줄지 않으므로, 1종을 먹는 조합표와 2종을 먹는 조합표는 한 노드에
            // 같이 못 산다.
            //
            // 조합표는 `RecipeCatalog`에서 읽는다 — 표를 두 곳에 적으면 갈린다.
            // ⚠️ 산출 속도와 개당 소비량은 **아직 미확정**이다. 값을 만들지 않고 카탈로그의
            // 센티넬을 그대로 쓰며, 밸런스가 정하면 그쪽만 고치면 된다.
            WriteNode(config, "munix", "복합 군수", NodeType.MunitionsComplex, true,
                // ⚠️ **대당 전력이 없다.** 밸런스가 확정한 것은 「대당 전력 7종」이고 복합 군수는
                // 여덟째라 그 표에 없다. 기초 군수 값을 가져다 쓰면 값을 발명하는 것이므로
                // 0(미설정 센티넬)으로 두고 Tbd로 표기한다.
                new NodeResourceProfile { confirm = ConfirmState.Tbd },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.StandardAmmo),
                    new NodePort(PortFace.South, PortIO.Input, FlowKind.BasicParts),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.PierceAmmo),
                },
                BuildRecipes(NodeType.MunitionsComplex, muniPerNode));

            // 에너지 — 발전(전력 공급). 고정비 0 · 발열 1/초는 확정,
            // **대당 발전량은 미확정**이라 프로필 전체는 Tbd다.
            WriteNode(config, "ener", "에너지", NodeType.Energy, true,
                new NodeResourceProfile { powerSupply = EnergyPowerSupply, powerDraw = EnergyPowerDraw,
                    heatGenerate = EnergyHeat, confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.Power),
                });

            // 저장 — 창고(버퍼). 군수 → 벨트 → 저장 → 벨트 → 마운트 소비.
            // ⚠️ 2026-08-21 정정: 포트 kind가 Material이라 군수(Ammo) 출력과 FlowKind가 안 맞아
            // 벨트 연결 자체가 성립하지 않았다. 조립 시스템 문서「노드 종류」표의 저장노드 행이
            // 변환 노드 틀에 맞춰져 있어 "입력 없음"으로 읽힌 데서 온 결함이다.
            WriteNode(config, "stor", "저장", NodeType.Storage, true,
                new NodeResourceProfile { powerDraw = StoragePowerDraw, confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    // 2026-09-05: 구 `FlowKind.Ammo`가 있던 자리를 표준탄이 잇는다(W01 3-2 품목 개정).
                    // 기초 군수의 산출이 표준탄으로 바뀌었으므로 저장이 Ammo를 물고 있으면
                    // 군수→저장 링크가 통째로 안 선다 — 2026-08-21에 Material로 겪었던 것과 같은 결함이다.
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.StandardAmmo),
                    new NodePort(PortFace.East, PortIO.Output, FlowKind.StandardAmmo),
                });

            // 부스터 — 추진제를 받아 회피 스택을 공급(2026-08-29 신설, 노드 7종).
            // 쉴드 발생 노드와 같은 **무형 자원 공급 계열**이라 마운트 같은 별도 소비 장치가 없다 —
            // 노드가 입력을 받아 그 자리에서 소비하고 보드 위 노드라 자기 버퍼를 이미 갖는다.
            // **한 대 = 회피 스택 2칸**이고 상한은 대수의 파생값이다(260829_V02) —
            // 그래서 회피를 늘리는 방법은 부스터를 더 놓는 것뿐이다.
            // 그릇만 키워도 안 세진다: 채우는 것은 군수 노드이고 15초에 하나다.
            WriteNode(config, "boost", "부스터", NodeType.Booster, true,
                new NodeResourceProfile { powerDraw = BoosterPowerDraw, confirm = ConfirmState.Confirmed },
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, FlowKind.Propellant),
                });

            // 쉴드 발생 — 스키마 자리만(구현 보류, §4). implemented=false, 포트 없음.
            WriteNode(config, "shield", "쉴드 발생", NodeType.Shield, false,
                new NodeResourceProfile { powerDraw = ShieldPowerDraw, confirm = ConfirmState.Tbd },
                new List<NodePort>());
        }

        /// <summary>
        /// <see cref="RecipeCatalog"/>의 행을 자산용 조합표로 옮긴다 (2026-09-04).
        ///
        /// **표를 두 곳에 적지 않기 위한 자리다.** 산출 속도는 확정치가 없어 군수 대당
        /// 산출을 그대로 쓰고, 개당 소비량은 카탈로그의 미확정 센티넬을 쓴다 —
        /// 둘 다 밸런스가 정하면 그쪽만 고친다.
        /// </summary>
        private static List<NodeRecipe> BuildRecipes(NodeType type, float outputPerSec)
        {
            var list = new List<NodeRecipe>();
            foreach (RecipeCatalog.Row row in RecipeCatalog.For(type))
            {
                var inputs = new List<RecipeInput>();
                foreach (FlowKind need in row.inputs)
                    inputs.Add(new RecipeInput { kind = need, perOutput = RecipeCatalog.PerOutputTbd });

                // ⚠️ **추진제는 주기도 스택도 다르다.** 15초에 1개와 스택 3은 둘 다
                // 자산에 있던 **선언치**이며, 표를 돌며 전부 같은 값을 넣으면 그것을 잃는다 —
                // 실제로 한 번 잃었다(둘 다 0이 됐다). 회피 1회분이라 탄약과 같은 속도로
                // 나오면 회피가 무제한이 되고, 스택이 0이면 부스터가 영영 안 찬다.
                //
                // 나머지 조합표의 스택은 **미설정 센티넬 0**이다 — 값을 발명하지 않는다.
                bool isPropellant = row.kind == RecipeKind.Propellant;
                float rate = isPropellant ? PropellantPerSec : outputPerSec;
                float stack = isPropellant ? PropellantItemStack : 0f;

                list.Add(new NodeRecipe
                {
                    kind = row.kind,
                    displayName = row.displayName,
                    inputs = inputs,
                    output = row.output,
                    outputPerSec = rate,
                    stackLimitTbd = stack,
                    implemented = true,
                });
            }
            return list;
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
