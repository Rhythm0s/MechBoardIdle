using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using MBI.UI;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 전투 화면 HUD — **시안 3**(2026-09-18 사용자 확정 · 플랜 §85-8 · 쿠키런 크럼블 문법).
    ///
    /// 🗑️ **폐기 — 「헤더 + 아래 블록」**(11f2ef9 · 같은 날 오전). 그 꼴은 **글자 블록**이었다.
    ///    줄이 열둘이면 어느 줄도 안 읽히고, 줄이 늘 때마다 잘릴 자리를 다시 재야 했다
    ///    (09-14 에 세 줄을 그렇게 잃었고 09-15·09-18 에 두 번 고쳤다).
    ///
    /// 📌 **바뀐 잣대는 하나다 — 글자를 줄이고 자리를 준다.**
    ///    · 판이 무엇인가 → **배지 한 줄**
    ///    · 얼마나 남았나 → **남은 몬스터 · 리젠 타이머**
    ///    · 로봇이 어떤가 → **몸에 붙은 막대**(글자로 안 적는다)
    ///    · 수치 아홉 줄 → **개발 빌드 전용 접이식 「i」 패널**
    ///
    /// ⚠️⚠️ **여기 값은 거의 다 가정이다**(설계 사후 역기입 자리) — 자리·크기·색·보상액.
    ///    확정된 것은 **무엇이 어디에 오는가**뿐이다.
    ///
    /// ⚠️ **판정에 손대지 않는다.** 이 파일은 시뮬이 낸 수를 옮겨 그린다.
    /// </summary>
    public sealed partial class StageRunner
    {
        // ── 고정 문구 (2026-09-18 설계 지시 · 닉네임은 고정) ─────────────────
        private const string PilotName = "MechPilot01";

        /// <summary>진단 패널이 펼쳐져 있는가 — **개발 빌드에서만 보인다.**</summary>
        private bool _devPanelOpen;

        /// <summary>마일스톤 카드의 「받았는가」 — 판정은 <see cref="MilestoneCard"/> 가 쥔다.</summary>
        private readonly MilestoneCard _milestone = new MilestoneCard();

        /// <summary>
        /// 떠오르는 「+12 고철」 한 줄 (2026-09-18 설계 지시 ①).
        /// ⚠️ **월드 자리를 든다** — 화면 자리를 들면 카메라가 로봇을 따라갈 때 글자만 안 따라간다.
        /// </summary>
        private struct Pop
        {
            public Vector2 world;
            public string text;
            public Color color;
            public float bornAt;
        }

        private readonly List<Pop> _pops = new List<Pop>(16);
        private readonly List<Vector2> _killSpots = new List<Vector2>(16);

        /// <summary>마지막으로 적이 죽은 자리 — 골드가 떨어질 곳이다(골드는 매 처치가 아니다).</summary>
        private Vector2 _lastKillSpot;
        private bool _hasKillSpot;

        private static readonly Color ScrapColor = new Color(0.78f, 0.80f, 0.84f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);

        // ─────────────────────────── 드롭·마그넷 ───────────────────────────

        /// <summary>
        /// 이번 프레임에 죽은 자리마다 재화를 떨어뜨린다 (설계 지시 ①).
        ///
        /// ⚠️⚠️ **적립은 여기서 안 한다.** 고철은 <see cref="KillRewardRule"/>, 골드는
        /// <see cref="GoldRewardRule"/> 이 방치 런타임 안에서 이미 처리했다 — 이 메서드는
        /// **그 일이 일어났다는 것을 보이게** 할 뿐이다. 값도 <see cref="IdleSignals"/> 가
        /// 게시한 것을 읽는다(같은 값을 두 자산 경로로 읽지 않는다 · 지침 §7).
        ///
        /// ⚠️ 방치 런타임이 없는 씬(격리 전투)에서는 마리당 고철이 0 이라 **아무것도 안
        /// 떨어진다** — 없는 수입을 그려 보이지 않는다.
        /// </summary>
        private void TickDrops()
        {
            if (_sim == null || tuning == null) return;

            _killSpots.Clear();
            _sim.ConsumeKillPositions(_killSpots);

            if (_killSpots.Count > 0)
            {
                _lastKillSpot = _killSpots[_killSpots.Count - 1];
                _hasKillSpot = true;
            }

            double perKill = IdleSignals.ScrapPerKill;
            if (perKill > 0d)
            {
                double amount = KillRewardRule.Scrap(1, perKill);
                for (int i = 0; i < _killSpots.Count; i++)
                    SpawnDrop(_killSpots[i], ScrapColor, "+" + amount.ToString("0.##") + " 고철");
            }

            // 골드는 **스무 마리에 한 번**이라 처치마다 안 떨어진다 —
            // 지급 사건을 가져와 그때만 하나 띄운다.
            int gold = IdleSignals.DrainGoldAwarded();
            if (gold > 0 && _hasKillSpot)
                SpawnDrop(_lastKillSpot, GoldColor, "+" + gold + " 골드");
        }

        private void SpawnDrop(Vector2 world, Color color, string label)
        {
            Transform target = _robotView != null ? _robotView.transform : null;

            // ⚠️ **자산이 오기 전에는 원형 폴백**(설계 지시 ① · 「코인 스프라이트는 아트」).
            //    색 사각을 안 쓰는 까닭은 보드의 색 축과 섞이기 때문이다.
            DropMagnet.Play(transform, new Vector3(world.x, world.y, 0f), target,
                PlaceholderSprite.SoftDisc(), color,
                tuning.dropViewUnitsTbd > 0f ? tuning.dropViewUnitsTbd : 0.3f,
                tuning.dropRestSecondsTbd, tuning.dropMagnetSecondsTbd,
                SortingLayers.Actor + 3);

            if (_pops.Count < 24)
                _pops.Add(new Pop { world = world, text = label, color = color, bornAt = Time.time });
        }

        /// <summary>떠오르는 글자들. **월드 → 화면**은 그릴 때 한 번만 푼다.</summary>
        private void DrawPops()
        {
            if (_pops.Count == 0 || tuning == null) return;

            Camera cam = Camera.main;
            if (cam == null) { _pops.Clear(); return; }

            float life = tuning.dropPopSecondsTbd > 0f ? tuning.dropPopSecondsTbd : 0.9f;
            float rise = tuning.dropPopRiseUnitsTbd;
            float sc = UiLayout.Scale(Screen.height);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(30f * sc))),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
            };

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                Pop p = _pops[i];
                float u = (Time.time - p.bornAt) / life;
                if (u >= 1f) { _pops.RemoveAt(i); continue; }

                var w = new Vector3(p.world.x, p.world.y + rise * u, 0f);
                Vector3 sp = cam.WorldToScreenPoint(w);
                if (sp.z < 0f) continue;

                style.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, 1f - u);
                GUI.Label(new Rect(sp.x - 90f * sc, Screen.height - sp.y - 20f * sc,
                                   180f * sc, 40f * sc), p.text, style);
            }
        }

        // ─────────────────────────── 상단 칩 줄 ────────────────────────────

        /// <summary>
        /// 상단 얇은 칩 줄 (설계 지시 · **헤더는 없다**).
        /// 좌: 썸네일 + 닉네임 + 전투력 / 우: 골드 칩 · 소리 · 설정.
        ///
        /// ⚠️ **썸네일은 아트가 준다**(로봇 A 얼굴 크롭). 오기 전에는 **원형 폴백**이며
        ///    색 사각으로 대신하지 않는다 — 없는 자산을 있는 척하지 않는다.
        /// ⚠️ 소리·설정 아이콘은 **글자 한 자**다(아이콘 자산 없음 · 가정).
        /// </summary>
        private void DrawChipBar()
        {
            float sc = UiLayout.Scale(Screen.height);
            Rect bar = UiLayout.ChipBarRect(Screen.width, Screen.height);
            float h = bar.height;

            int px = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(h * 0.30f)));
            var text = new GUIStyle(GUI.skin.label)
            {
                fontSize = px,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
            };
            text.normal.textColor = Color.white;

            // ── 왼쪽 — 썸네일 · 닉네임 · 전투력 ──
            // ⚠️ 판은 **무리마다** 깐다. 줄 전체를 덮으면 오른쪽 버튼이 판 밑으로 들어간다.
            Rect left = UiLayout.ChipLeftRect(Screen.width, Screen.height);
            UiPlate.Draw(left);
            UiBlockers.Add(left);

            var face = new Rect(left.x + 6f * sc, left.y + 4f, h - 8f, h - 8f);

            // ⚠️ **둥근 테두리를 두른다**(2026-09-18 사용자 육안 · 시안 4 ①).
            //    맨 그림 하나만 놓으면 판 위에 **붙여 놓은 조각**으로 보인다.
            //    자산이 없으므로 이미 있는 고리 그림(`PlaceholderSprite.Ring`)을 쓴다.
            Color ringPrev = GUI.color;
            GUI.color = new Color(0.95f, 0.80f, 0.45f, 0.95f);
            GUI.DrawTexture(Grow(face, 3f * sc), PlaceholderSprite.Ring().texture,
                            ScaleMode.StretchToFill);
            GUI.color = ringPrev;

            Sprite portrait = robot != null ? robot.sprite : null;
            if (portrait != null && portrait.texture != null)
                GUI.DrawTextureWithTexCoords(face, portrait.texture, SpriteUv(portrait), true);
            else
                GUI.DrawTexture(face, PlaceholderSprite.SoftDisc().texture, ScaleMode.ScaleToFit);

            float nameX = face.xMax + 10f * sc;
            float nameW = Mathf.Max(0f, left.xMax - nameX - 6f * sc);
            GUI.Label(new Rect(nameX, left.y, nameW, h * 0.5f), PilotName, text);

            // ⚠️ **전투력은 `CombatPower` 한 곳이 낸다** — 화면에서 직접 곱하지 않는다(지침 §7).
            MountLoad mount = _sim != null ? _sim.ActiveMount : null;
            int power = CombatPower.Of(_output,
                mount != null ? mount.Total : 0f,
                mount != null ? mount.Capacity : 0f,
                tuning != null ? tuning.combatPowerMountFactorTbd : CombatPower.DefaultMountFactor);
            var powerStyle = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            powerStyle.normal.textColor = new Color(1f, 0.88f, 0.55f);
            GUI.Label(new Rect(nameX, left.y + h * 0.5f, nameW, h * 0.5f),
                      "전투력 " + power.ToString("N0"), powerStyle);

            // ── 골드 칩 — 수 + 「+n/초」 ──
            Rect chip = UiLayout.ChipGoldRect(Screen.width, Screen.height);
            UiPlate.Draw(chip);
            UiBlockers.Add(chip);

            var goldStyle = new GUIStyle(text)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            goldStyle.normal.textColor = GoldColor;
            // 동전 자리 — ⚠️ **코인 스프라이트는 아트 몫**이고 오기 전에는 **원형 폴백**이다.
            float coin = chip.height * 0.42f;
            var coinRect = new Rect(chip.x + 10f * sc, chip.y + (chip.height * 0.58f - coin) * 0.5f,
                                    coin, coin);
            Color coinPrev = GUI.color;
            GUI.color = GoldColor;
            GUI.DrawTexture(coinRect, PlaceholderSprite.SoftDisc().texture, ScaleMode.ScaleToFit);
            GUI.color = coinPrev;

            GUI.Label(new Rect(coinRect.xMax + 6f * sc, chip.y,
                               chip.width - coinRect.width - 20f * sc, chip.height * 0.58f),
                      IdleSignals.WalletGold.ToString("N0"), goldStyle);

            var rateStyle = new GUIStyle(text)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(px * 0.72f))),
            };
            rateStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(chip.x, chip.y + chip.height * 0.52f, chip.width, chip.height * 0.46f),
                      "+" + GoldPerSecond().ToString("F2") + "/초", rateStyle);

            // ── 설정 ──
            // ⚠️⚠️ **소리 아이콘은 여기서 안 그린다.** 자리(`ChipSoundRect`)만 비워 두고
            //    `AudioOptionsPanel` 이 제 버튼을 거기에 그린다 — 같은 일을 하는 자리를
            //    둘로 만들지 않기 위해서다(되풀이되는 결함 종류).
            //
            // ✅ **되살렸다**(2026-09-18 사용자 확정) — 누르면 **설정 패널**이 열린다.
            //    담는 것은 **심사자용 바로가기**와 **「메인 메뉴로」** 둘이고,
            //    **소리는 안 담는다**(제 버튼이 이미 있다 · 중복 금지).
            //
            // 🗑️🗑️ **폐기 — 설정 버튼이 메인 메뉴를 열던 것**(2026-09-18 사용자 육안 · 결함).
            //
            // ⚠️⚠️ **누르면 HUD 가 통째로 사라졌다.** `MainMenuGate.IsOpen` 은 `OnGUI` 일곱을
            //    **억제하는 깃발**이고(§66-16), 그 일곱에 이 HUD 가 전부 들어 있다 —
            //    칩 줄 · 배지 · 원형 둘 · 상태 띠 · 조립 막대. 메뉴는 **게임을 시작하기 전**의
            //    화면이라 그 억제가 맞는데, **전투 중에 다시 열면** 억제만 남고 볼륨 패널
            //    하나가 빈 화면에 뜬다. 사용자가 본 것이 그 자리다.
            //
            // 📌 **그래서 빗장을 따로 둔다**(`SettingsGate`) — **얹기만 하고 억제하지 않는다.**
            //    설정이 열려 있어도 칩 줄·배지·원형·상태 띠는 그대로 산다.
            Rect settings = UiLayout.ChipSettingsRect(Screen.width, Screen.height);
            UiBlockers.Add(settings);
            var icon = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.Min(
                    Mathf.RoundToInt(settings.height * 0.28f),
                    Mathf.RoundToInt(settings.width / 3.2f)))),
                clipping = TextClipping.Overflow,
            };
            if (UiSkin.Button(settings, SettingsGate.IsOpen ? "닫기" : "설정", icon))
                SettingsGate.Toggle();
        }

        /// <summary>
        /// 초당 골드 — **실제로 지급된 누계를 경과로 나눈다**(⚠️ 표시 전용).
        ///
        /// ⚠️ 적립이 아니다. 적립은 <see cref="GoldRewardRule"/> 이 처치 사건으로만 한다 —
        /// 이 수를 재화로 바꾸면 <see cref="ScrapFarmingRate"/> 주석이 경고하는 **이중 적립**이 된다.
        ///
        /// ⚠️ **비(20마리당 5골드)를 여기 적지 않는다.** 그 값은 자산 한 곳에 살고,
        /// 화면이 다시 적으면 자산을 고쳤을 때 답이 둘이 된다(지침 §7).
        /// </summary>
        private float GoldPerSecond()
        {
            if (_sim == null || _sim.Elapsed <= 0f) return 0f;
            return (float)(IdleSignals.WalletGold / _sim.Elapsed);
        }

        // ──────────────────────── 스테이지 배지 줄 ─────────────────────────

        /// <summary>
        /// S1 배지 + 목표 한 줄 + 「남은 몬스터 n · 리젠 mm:ss」(설계 지시).
        ///
        /// 🗑️ **처치 막대 폐기** — 남은 수를 **수로** 적는다. 막대는 「얼마나 남았나」를
        ///    눈금으로 말하는데, 이 판에서 알고 싶은 것은 **몇 마리**와 **언제 또 오나**다.
        ///
        /// ⚠️ 리젠은 <see cref="WaveSpawnRule"/> 가 낸 수를 그대로 옮긴다 — 화면이 안 센다.
        /// </summary>
        private void DrawStageBadge()
        {
            if (_sim == null || stage == null) return;

            float sc = UiLayout.Scale(Screen.height);
            Rect r = UiLayout.StageBadgeRect(Screen.width, Screen.height);
            UiPlate.Draw(r);

            int px = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(32f * sc)));
            var badge = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(13, Mathf.RoundToInt(42f * sc))),
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
            };
            badge.normal.textColor = new Color(1f, 0.86f, 0.45f);

            var line = new GUIStyle(GUI.skin.label)
            {
                fontSize = px,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            line.normal.textColor = Color.white;

            float pad = 10f * sc;
            float rowH = r.height / 3f;

            // ⚠️ **배지는 알약이다**(2026-09-18 사용자 육안 · 시안 4 ① — 「S1 주황 배지」).
            //    글자만 두면 판 위의 다른 줄과 무게가 같아 **어느 판인지가 안 튄다.**
            //    ⚠️ 색·크기는 가정(설계 역기입 자리).
            var pill = new Rect(r.x + pad, r.y + 4f * sc,
                                Mathf.Min(220f * sc, r.width * 0.42f), rowH - 8f * sc);
            UiPlate.DrawTinted(pill, BadgeTint);
            var pillText = new GUIStyle(badge) { alignment = TextAnchor.MiddleCenter };
            pillText.normal.textColor = Color.white;

            // ⚠️ **알약 안에서 줄이 바뀌면 안 된다**(2026-09-19 사용자 육안 ⑤ · 스크린샷 1).
            //
            // 「스테이지 S1」이 알약 폭(220)을 넘어 **「스테이지 / S1」** 두 줄이 됐고,
            // 알약 높이는 한 줄치라 **둘째 줄이 판 밖으로 흘렀다.** 글자를 줄이는 대신
            // **드는 크기로 내린다** — 무엇인지(「스테이지」)를 지우지 않는 쪽이다.
            // 사다리 위의 값 하나로만 내려간다(아틀라스 예산 · `KoreanFont.Ladder`).
            UiText.FitOneLine(pillText, StageTitle(), pill.width - 12f * sc);
            GUI.Label(pill, StageTitle(), pillText);

            // 목표 한 줄 — 값은 `balance_v4.json` 의 topic·req 다(지어내지 않는다).
            string goal = HasRequirement
                ? "목표: " + stage.topic + "  ·  출력 " + stage.req.ToString("F0") + " 넘기기"
                : "목표: " + stage.topic;
            GUI.Label(new Rect(r.x + pad, r.y + rowH, r.width - pad * 2f, rowH), goal, line);

            // 남은 몬스터 · 리젠.
            // ⚠️ **「남은」은 살아 있는 것 + 아직 안 나온 것이다** — 화면에 보이는 수만 적으면
            //    묶음 사이에 0 이 떠서 「다 잡았다」로 읽힌다.
            int alive = _sim.Remaining;
            int unspawned = Mathf.Max(0, _sim.TotalEnemies - _sim.TotalKills - alive);
            float next = _sim.SecondsToNextWave;
            string regen = next < 0f ? "리젠 없음(마지막 묶음)" : "리젠 " + WaveSpawnRule.Clock(next);
            GUI.Label(new Rect(r.x + pad, r.y + rowH * 2f, r.width - pad * 2f, rowH),
                      "남은 몬스터 " + (alive + unspawned) + "  ·  " + regen, line);
        }

        // ──────────────────────── 로봇 몸 밑 계기 ──────────────────────────

        /// <summary>
        /// 로봇 **몸 밑**의 수치와 회피 눈금 (설계 지시).
        ///
        /// ⚠️⚠️ **막대 자체는 이미 월드에 있다**(<see cref="CombatEntityView"/> 의 HP·쉴드 바).
        ///    여기서 그리는 것은 **수치와 회피 눈금**뿐이다 — 막대를 다시 그리면
        ///    같은 것을 두 곳이 그리게 되고, 둘이 어긋나는 날이 온다(지침 §7).
        ///
        /// ⚠️ 쉴드가 없는 판에서는 그 줄을 **안 적는다**(0/0 은 고장으로 읽힌다).
        /// </summary>
        private void DrawRobotGauges()
        {
            if (_sim == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 body = _sim.Robot.position;
            // ⚠️ **월드 막대 밑에서 시작한다.** HP·보호막 막대가 발밑 −0.62·−0.86(뷰 크기 기준)에
            //    서므로, 수치가 그 위에 앉으면 막대를 가린다. ⚠️ 0.9 는 가정이다.
            Vector3 sp = cam.WorldToScreenPoint(
                new Vector3(body.x, body.y - _sim.Robot.radius - 0.9f, 0f));
            if (sp.z < 0f) return;

            float sc = UiLayout.Scale(Screen.height);
            float w = 420f * sc;
            float x = sp.x - w * 0.5f;
            float y = Screen.height - sp.y;

            // ⚠️ **「돌아왔다」 창을 피해 앉는다**(2026-09-18 사용자 육안 · 결함 ③).
            //    창과 로봇이 둘 다 화면 한가운데라 자리를 서로 모르면 반드시 겹친다.
            //    감추지 않고 **창 아래로 민다** — 둘 다 읽혀야 한다.
            if (IdleSignals.OfflinePopupOpen)
            {
                Rect popup = IdleSignals.OfflinePopupRect;
                float blockH = 28f * sc * 3f;
                if (popup.Overlaps(new Rect(x, y, w, blockH)))
                    y = popup.yMax + 8f * sc;
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(26f * sc))),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.white;

            float rowH = 28f * sc;
            GUI.Label(new Rect(x, y, w, rowH),
                      _sim.Robot.hp.ToString("F0") + " / " + _sim.Robot.maxHp.ToString("F0"), style);
            y += rowH;

            float shieldMax = _sim.ShieldMax, shieldNow = _sim.Shield.Value;
            if (IsMerged && _sim.HasTagPartner)
            {
                shieldMax += _sim.StandbyShieldMax;
                shieldNow += _sim.StandbyShield.Value;
            }
            if (shieldMax > 0f)
            {
                var s = new GUIStyle(style);
                s.normal.textColor = new Color(0.55f, 0.82f, 1f);
                GUI.Label(new Rect(x, y, w, rowH),
                          "보호막 " + shieldNow.ToString("F0") + " / " + shieldMax.ToString("F0"), s);
                y += rowH;
            }

            // 회피 눈금 — 글자가 아니라 눈금이다(설계 지시 「회피 눈금 8」).
            DodgeSystem d = _sim.Dodge;
            float tickW = Mathf.Min(300f * sc, w);
            HudBars.Ticks(new Rect(sp.x - tickW * 0.5f, y + 4f * sc, tickW,
                                   Mathf.Max(HudBars.BarHeight, HudBars.BarHeight * sc)),
                          d.Stacks, d.Capacity, d.IsInvincible);
        }

        // ────────────────────────── 마일스톤 카드 ──────────────────────────

        /// <summary>
        /// 우하단 마일스톤 카드 (설계 지시 ④) — 튜토리얼 목표 둘과 보상 받기.
        ///
        /// ⚠️ **판정은 <see cref="Stage0Goal"/> 이 이미 갖고 있다** — 여기서 다시 안 센다.
        ///    신호로 건너온 두 걸쇠를 그대로 읽는다(<see cref="TutorialSignals"/>).
        /// ⚠️ **보상액은 가정이다**(<see cref="MilestoneCard"/> 상수 · 설계 역기입 자리).
        /// ⚠️ **지급은 방치 런타임이 한다** — 전투가 지갑을 직접 만지지 않는다.
        /// </summary>
        private void DrawMilestoneCard()
        {
            if (!TutorialSignals.GoalActive) return;

            float sc = UiLayout.Scale(Screen.height);
            Rect r = UiLayout.MilestoneCardRect(Screen.width, Screen.height);
            if (r.height <= 20f || r.width <= 20f) return;
            UiPlate.Draw(r);
            UiBlockers.Add(r);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(30f * sc))),
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
            };
            title.normal.textColor = new Color(1f, 0.86f, 0.45f);

            var line = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(26f * sc))),
                clipping = TextClipping.Overflow,
            };
            line.normal.textColor = Color.white;

            float pad = 12f * sc;
            float rowH = 40f * sc;
            float y = r.y + 6f * sc;
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2f, rowH), "마일스톤", title);
            y += rowH;
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2f, rowH),
                      Mark(TutorialSignals.GoalSlotFilled) + " 끊긴 자리를 잇는다", line);
            y += rowH;
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2f, rowH),
                      Mark(TutorialSignals.GoalMountFilled) + " 마운트가 가득 찬다", line);
            y += rowH + 4f * sc;

            var btn = new Rect(r.x + pad, y, r.width - pad * 2f,
                               Mathf.Max(0f, r.yMax - y - 8f * sc));
            if (btn.height <= 6f) return;

            if (_milestone.Claimed)
            {
                var done = new GUIStyle(line) { alignment = TextAnchor.MiddleCenter };
                done.normal.textColor = new Color(0.70f, 0.90f, 0.70f);
                GUI.Label(btn, "보상 받음", done);
                return;
            }

            bool complete = TutorialSignals.GoalSlotFilled && TutorialSignals.GoalMountFilled;
            UiBlockers.Add(btn);

            bool prev = GUI.enabled;
            GUI.enabled = _milestone.CanClaim(complete);
            string label = "보상 받기 · 골드 " + MilestoneCard.DefaultGoldReward
                         + " / 고철 " + MilestoneCard.DefaultScrapReward;
            if (UiSkin.Button(btn, label) && _milestone.TryClaim(complete))
                IdleSignals.ReportMilestoneReward(
                    MilestoneCard.DefaultGoldReward, MilestoneCard.DefaultScrapReward);
            GUI.enabled = prev;
        }

        private static string Mark(bool done) => done ? "[v]" : "[  ]";

        // ───────────────────── 개발 빌드 전용 「i」 패널 ─────────────────────

        /// <summary>
        /// 좌측 「i」 — 종전 HUD 글자 블록이 **통째로 들어간 자리**(설계 지시).
        ///
        /// ⚠️⚠️ **개발 빌드에서만 보인다.** 심사자 화면에서 이 아홉 줄은 내용이 아니라 소음이다.
        ///    그렇다고 지우지 않는 까닭은 **09-15 부터 이 줄들이 결함을 세 번 잡았기** 때문이다
        ///    (얼굴 방향 · 추진제 유입 · 마운트 품목). 진단물은 남기되 **접어 둔다.**
        ///
        /// ⚠️ `GUILayout.BeginArea` 는 넘치는 것을 말없이 자른다 — 여기는 **진단 전용**이라
        ///    잘려도 화면의 본문을 안 잃는다. 본문은 위의 배지·칩·계기가 든다.
        /// </summary>
        private void DrawDevPanel(GUIStyle style)
        {
            if (!Debug.isDebugBuild || _sim == null) return;

            Rect toggle = UiLayout.DevToggleRect(Screen.width, Screen.height);
            UiBlockers.Add(toggle);
            if (UiSkin.Button(toggle, _devPanelOpen ? "×" : "i")) _devPanelOpen = !_devPanelOpen;
            if (!_devPanelOpen) return;

            Rect panel = UiLayout.DevPanelRect(Screen.width, Screen.height);
            if (panel.height <= 20f) return;
            UiPlate.Draw(panel);
            UiBlockers.Add(panel);

            GUILayout.BeginArea(panel);
            GUILayout.Label(OutputLine(), style);
            GUILayout.Label(AmmoLine(), style);
            DrawAmmoBar();
            GUILayout.Label("저장고(군수 생산) "
                            + LogisticsOutputBridge.AmmoProduce.ToString("F1") + " 발/초", style);
            GUILayout.Label("적 " + _sim.Remaining + "/" + _sim.TotalEnemies
                            + "   로봇 HP " + _sim.Robot.hp.ToString("F0")
                            + "/" + _sim.Robot.maxHp.ToString("F0") + ShieldLine(), style);
            GUILayout.Label(DodgeLine(), style);
            if (robotB != null) GUILayout.Label(TagLine(), style);
            GUILayout.Label("고철 " + IdleSignals.WalletScrap.ToString("N0")
                            + "   ·   강화재료 " + IdleSignals.WalletEnhMaterial.ToString("N0")
                            + "   ·   골드 " + IdleSignals.WalletGold.ToString("N0")
                            + " (모은 처치 " + IdleSignals.KillsTowardGold + ")", style);
            GUILayout.Label("경과 " + _sim.Elapsed.ToString("F1")
                            + "s / " + stage.challengeTime.ToString("F0") + "s"
                            + "   ·   웨이브 " + _sim.WaveSize + "마리 / "
                            + _sim.WaveInterval.ToString("F2") + "초", style);
            GUILayout.Label(FaceDiagnosticLine(), style);
            if (!string.IsNullOrEmpty(_tagSkillNote) && Time.time < _tagSkillNoteUntil)
                GUILayout.Label(_tagSkillNote, style);
            GUILayout.Label("이동 WASD / 화살표   ·   회피 = 스페이스 / 화면 플릭", style);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 얼굴 방향 진단 한 줄 — 09-15 육안 8차 ① 에서 들인 임시물.
        /// 🗑️ 자리가 잡히면 통째로 걷는다. 지금은 **「i」 패널 안**으로 들어갔다.
        /// </summary>
        private string FaceDiagnosticLine()
            => _robotView == null
             ? "얼굴 — 뷰 없음"
             : "얼굴 " + (_robotView.FacingOverride.HasValue ? "조준" : "이동")
               + " · 마지막 조준 "
               + (_lastAimAt > 0f ? (Time.time - _lastAimAt).ToString("F1") + "초 전" : "없음")
               + " · 수동유예 " + Mathf.Max(0f, _manualHoldUntil - Time.time).ToString("F1") + "s";

        // ───────────────────────────── 링 게이지 ────────────────────────────

        /// <summary>
        /// 태그 원형 둘레의 **적재 링** (설계 지시 「태그 원형(적재 링 40)」).
        ///
        /// ⚠️ **사각 테두리를 둘레로 쓴다** — IMGUI 로 원호를 그리려면 그림이 하나 더 있어야
        ///    한다. 없는 자산을 지어내지 않고 <c>DrawTagSkillRing</c> 과 같은 수법을 쓴다.
        ///    차는 방향은 **왼위에서 시계 방향**이다.
        ///
        /// ⚠️ **상한이 없으면(0) 안 그린다** — 만충 판정 자체가 없는 자리다.
        /// </summary>
        private static void DrawLoadRing(Rect r, float ratio, Color color, float thickness)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio <= 0f) return;

            float t = Mathf.Max(2f, thickness);
            var outer = new Rect(r.x - t, r.y - t, r.width + t * 2f, r.height + t * 2f);
            float w = outer.width, h = outer.height;
            float filled = (w + h) * 2f * ratio;

            // 위 → 오른 → 아래 → 왼. 각 변은 남은 길이만큼만 채운다.
            float run = Mathf.Min(filled, w);
            if (run > 0f) HudBars.Fill(new Rect(outer.x, outer.y, run, t), color);
            filled -= w;
            if (filled <= 0f) return;

            run = Mathf.Min(filled, h);
            HudBars.Fill(new Rect(outer.xMax - t, outer.y, t, run), color);
            filled -= h;
            if (filled <= 0f) return;

            run = Mathf.Min(filled, w);
            HudBars.Fill(new Rect(outer.xMax - run, outer.yMax - t, run, t), color);
            filled -= w;
            if (filled <= 0f) return;

            run = Mathf.Min(filled, h);
            HudBars.Fill(new Rect(outer.x, outer.yMax - run, t, run), color);
        }

        /// <summary>배지 색 — 주황(사용자 육안 「S1 주황 배지」). ⚠️ 값은 가정이다.</summary>
        private static readonly Color BadgeTint = new Color(1.25f, 0.62f, 0.18f, 1f);

        /// <summary>사방으로 넓힌 자리. 테두리를 두를 때 쓴다.</summary>
        private static Rect Grow(Rect r, float by) =>
            new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        /// <summary>스프라이트가 아틀라스 안 어디에 있는지 — 그 조각만 그린다.</summary>
        private static Rect SpriteUv(Sprite s)
        {
            Rect tr = s.textureRect;
            return new Rect(tr.x / s.texture.width, tr.y / s.texture.height,
                            tr.width / s.texture.width, tr.height / s.texture.height);
        }
    }
}
