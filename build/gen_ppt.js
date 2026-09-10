const pptxgen = require("pptxgenjs");

const BG = "121212";
const CARD = "1C1C18";
const STONE = "3A3A34";
const AMBER = "E0A458";
const TEXT = "ECE8DD";
const MUTED = "9A9A8E";
const DIM = "6E6E64";

const FONT_HEAD = "Cambria";
const FONT_BODY = "Calibri";

function newDeck() {
  const p = new pptxgen();
  p.layout = "LAYOUT_WIDE"; // 13.33 x 7.5
  return p;
}

function bgSlide(p) {
  const s = p.addSlide();
  s.background = { color: BG };
  return s;
}

function kicker(s, text) {
  s.addText(text.toUpperCase(), {
    x: 0.6, y: 0.45, w: 8, h: 0.4,
    fontFace: FONT_BODY, fontSize: 13, color: AMBER, bold: true,
    charSpacing: 2, isTextBox: true, margin: 0,
  });
}

function title(s, text, opts = {}) {
  s.addText(text, {
    x: 0.6, y: opts.y || 0.85, w: opts.w || 11.5, h: opts.h || 0.9,
    fontFace: FONT_HEAD, fontSize: opts.size || 32, color: TEXT, bold: true,
    isTextBox: true, margin: 0,
  });
}

function pageNum(s, n) {
  s.addText(String(n).padStart(2, "0"), {
    x: 12.5, y: 7.05, w: 0.6, h: 0.35,
    fontFace: FONT_BODY, fontSize: 11, color: DIM, align: "right",
    isTextBox: true, margin: 0,
  });
}

function placeholder(s, x, y, w, h, label) {
  s.addShape("rect", {
    x, y, w, h,
    fill: { color: "171714" },
    line: { color: STONE, width: 1.25, dashType: "dash" },
  });
  s.addText([{ text: "이미지 삽입\n", options: { bold: true, color: AMBER, fontSize: 13, breakLine: true } },
             { text: label, options: { color: MUTED, fontSize: 10.5 } }], {
    x: x + 0.3, y: y + h / 2 - 0.5, w: w - 0.6, h: 1,
    fontFace: FONT_BODY, align: "center", valign: "middle", isTextBox: true, margin: 0,
  });
}

function bulletBlock(s, items, x, y, w, h, opts = {}) {
  const paras = items.map((it, i) => ({
    text: it,
    options: {
      bullet: { code: "25AA", indent: 18 },
      color: opts.color || TEXT,
      fontSize: opts.fontSize || 14,
      breakLine: true,
      paraSpaceAfter: opts.spaceAfter || 10,
    },
  }));
  s.addText(paras, { x, y, w, h, fontFace: FONT_BODY, isTextBox: true, margin: 0, valign: "top" });
}

function statCard(s, x, y, w, h, num, label) {
  s.addShape("roundRect", { x, y, w, h, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText(num, {
    x: x + 0.2, y: y + 0.12, w: w - 0.4, h: h * 0.55,
    fontFace: FONT_HEAD, fontSize: 26, bold: true, color: AMBER,
    align: "left", isTextBox: true, margin: 0,
  });
  s.addText(label, {
    x: x + 0.2, y: y + h * 0.6, w: w - 0.4, h: h * 0.35,
    fontFace: FONT_BODY, fontSize: 11, color: MUTED,
    align: "left", isTextBox: true, margin: 0,
  });
}

function circleIcon(s, x, y, size, glyph) {
  s.addShape("ellipse", { x, y, w: size, h: size, fill: { color: CARD }, line: { color: STONE, width: 1 } });
  s.addText(glyph, {
    x, y, w: size, h: size, align: "center", valign: "middle",
    fontFace: FONT_BODY, fontSize: size * 26, color: AMBER, bold: true,
    isTextBox: true, margin: 0,
  });
}

const pres = newDeck();

// ---------- Slide 0: Title ----------
{
  const s = bgSlide(pres);
  s.addText("[무제] 동굴 공포 게임", {
    x: 0.9, y: 2.7, w: 11.5, h: 1.3,
    fontFace: FONT_HEAD, fontSize: 44, bold: true, color: TEXT, isTextBox: true, margin: 0,
  });
  s.addText("친구를 따라 들어간 동굴에서, 친구가 남긴 표식조차 믿을 수 없게 되어가는\n20분 내외의 1인칭 파운드푸티지 공포 게임", {
    x: 0.9, y: 3.95, w: 10.5, h: 0.9,
    fontFace: FONT_BODY, fontSize: 16, color: MUTED, isTextBox: true, margin: 0,
  });
  s.addShape("line", { x: 0.9, y: 2.55, w: 1.4, h: 0, line: { color: AMBER, width: 3 } });
  s.addText("GAME DESIGN DOCUMENT", {
    x: 0.9, y: 2.05, w: 6, h: 0.4,
    fontFace: FONT_BODY, fontSize: 13, color: AMBER, bold: true, charSpacing: 3, isTextBox: true, margin: 0,
  });
}

// ---------- Slide 1: High-Level Vision ----------
{
  const s = bgSlide(pres);
  kicker(s, "01 · High-Level Vision");
  title(s, "핵심 비전");

  s.addShape("roundRect", { x: 0.6, y: 2.0, w: 7.1, h: 2.5, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("엘리베이터 피치", { x: 0.9, y: 2.2, w: 6.5, h: 0.35, fontFace: FONT_BODY, fontSize: 12, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  s.addText("친구를 따라 들어간 동굴에서, 앞서간 친구가 남긴 표식을 따라 나아가지만 그 표식조차 점점 믿을 수 없게 되어가는 20분 내외의 1인칭 파운드푸티지 공포 게임.", {
    x: 0.9, y: 2.6, w: 6.5, h: 1.7, fontFace: FONT_BODY, fontSize: 14.5, color: TEXT, isTextBox: true, margin: 0, valign: "top",
  });

  s.addShape("roundRect", { x: 0.6, y: 4.65, w: 7.1, h: 2.1, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("레퍼런스 + 차별점", { x: 0.9, y: 4.85, w: 6.5, h: 0.35, fontFace: FONT_BODY, fontSize: 12, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  s.addText("Iron Lung(폐쇄 공간, 안 보이는 위협), 8번 출구(갈림길+단서 확인 루프) 참고. 차별점: 정답을 판정해서 되돌리는 구조가 없다 — 표식의 신뢰도 자체가 무너지는 게 핵심이라 공략이 성립하지 않는다.", {
    x: 0.9, y: 5.2, w: 6.5, h: 1.4, fontFace: FONT_BODY, fontSize: 13, color: TEXT, isTextBox: true, margin: 0, valign: "top",
  });

  statCard(s, 8.05, 2.0, 4.65, 1.15, "PC / Unity 3D", "플랫폼");
  statCard(s, 8.05, 3.3, 4.65, 1.15, "20분 이하", "러닝타임 · 단일 세션");
  statCard(s, 8.05, 4.6, 4.65, 1.15, "미정", "비즈니스 모델");
  pageNum(s, 1);
}

// ---------- Slide 2: Art Style / Theme ----------
{
  const s = bgSlide(pres);
  kicker(s, "02 · Art Style & Theme");
  title(s, "아트 스타일 / 무드");

  placeholder(s, 0.6, 2.0, 6.5, 4.75, "동굴 탐험 레퍼런스\n(좁은 크롤 통로 / 높이 변화 비교)");

  bulletBlock(s, [
    "UI 스타일: 캠코더 오버레이 — 타임스탬프·배터리·REC·노이즈·스캔라인",
    "환경 톤: 어둡고 습함. 천장 높이 160cm → 100cm로 낮아지며 압박감 상승",
    "캐릭터 스타일: 해당 없음 — 캐릭터·크리처 모델링 없음. 위협은 소리·화면 효과로만 존재",
    "렌더링/시점: 1인칭 핸드캠 (파운드 푸티지)",
    "연출 참고 클립: 미정",
  ], 7.4, 2.05, 5.3, 4.6, { fontSize: 13.5, spaceAfter: 14 });
  pageNum(s, 2);
}

// ---------- Slide 3: Story ----------
{
  const s = bgSlide(pres);
  kicker(s, "03 · Story");
  title(s, "스토리 프레임");

  s.addShape("roundRect", { x: 0.6, y: 2.0, w: 12.1, h: 2.1, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("핵심 서사", { x: 0.9, y: 2.2, w: 5, h: 0.35, fontFace: FONT_BODY, fontSize: 12, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  s.addText("플레이어는 친구를 따라 담력체험 삼아 동굴에 들어간다. 먼저 사라진 친구가 남긴 표식을 따라 나아가지만, 표식은 점점 신뢰할 수 없게 변하고, 결국 어떤 길을 택해도 마지막엔 무언가에게 습격당해 죽는다.", {
    x: 0.9, y: 2.6, w: 11.5, h: 1.4, fontFace: FONT_BODY, fontSize: 14.5, color: TEXT, isTextBox: true, margin: 0, valign: "top",
  });

  const steps = [
    ["오프닝", "친구와의 대화\n(형식 미정)"],
    ["본편", "환경 스토리텔링\n(흔적, 소리)"],
    ["엔딩", "컷신\n(습격 → 낙하)"],
  ];
  const stepW = 3.6, gap = 0.35, startX = 0.6, y0 = 4.55;
  steps.forEach((st, i) => {
    const x = startX + i * (stepW + gap);
    s.addShape("roundRect", { x, y: y0, w: stepW, h: 1.9, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
    s.addText(st[0], { x: x + 0.3, y: y0 + 0.25, w: stepW - 0.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 18, bold: true, color: AMBER, isTextBox: true, margin: 0 });
    s.addText(st[1], { x: x + 0.3, y: y0 + 0.85, w: stepW - 0.6, h: 0.9, fontFace: FONT_BODY, fontSize: 13, color: TEXT, isTextBox: true, margin: 0 });
    if (i < steps.length - 1) {
      s.addText("→", { x: x + stepW, y: y0 + 0.6, w: gap, h: 0.7, align: "center", valign: "middle", fontFace: FONT_BODY, fontSize: 22, color: STONE, isTextBox: true, margin: 0 });
    }
  });
  s.addText("컷신은 엔딩에서만 사용된다. 본편은 텍스트 튜토리얼 없이 환경으로 전달.", {
    x: 0.6, y: 6.6, w: 12, h: 0.4, fontFace: FONT_BODY, fontSize: 11.5, italic: true, color: MUTED, isTextBox: true, margin: 0,
  });
  pageNum(s, 3);
}

// ---------- Slide 4: Core Loop ----------
{
  const s = bgSlide(pres);
  kicker(s, "04 · Core Loop");
  title(s, "핵심 루프");

  const loopSteps = ["갈림길 도착", "표식 확인", "방향 판단", "이동"];
  const w4 = 2.55, gapArrow = 0.35, startX = 0.6, y0 = 2.15;
  loopSteps.forEach((t, i) => {
    const x = startX + i * (w4 + gapArrow);
    circleIcon(s, x + w4 / 2 - 0.35, y0, 0.7, String(i + 1));
    s.addText(t, { x, y: y0 + 0.85, w: w4, h: 0.6, align: "center", fontFace: FONT_BODY, fontSize: 12.5, color: TEXT, bold: true, isTextBox: true, margin: 0 });
    if (i < loopSteps.length - 1) {
      s.addText("→", { x: x + w4, y: y0, w: gapArrow, h: 0.7, align: "center", valign: "middle", fontFace: FONT_BODY, fontSize: 20, color: STONE, isTextBox: true, margin: 0 });
    }
  });
  s.addText("반복될수록 이상현상 강도 상승, 표식 매체도 단계적으로 전환 → 엔딩 트리거 → 컷신 → 종료", {
    x: 0.6, y: 3.55, w: 12.1, h: 0.5, fontFace: FONT_BODY, fontSize: 13, italic: true, color: AMBER, isTextBox: true, margin: 0,
  });

  s.addShape("roundRect", { x: 0.6, y: 4.3, w: 5.85, h: 2.45, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("핵심 시스템", { x: 0.9, y: 4.5, w: 5.2, h: 0.35, fontFace: FONT_BODY, fontSize: 12, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  bulletBlock(s, [
    "이상현상 상태 관리 (루프 진행에 따른 강도·매체 전환)",
    "플레이어 상태 연출 (숨소리·시야 왜곡)",
    "메타 시스템: 없음 — 단일 세션, 저장·재도전 없음",
  ], 0.9, 4.9, 5.25, 1.75, { fontSize: 12.5, spaceAfter: 8 });

  s.addShape("roundRect", { x: 6.75, y: 4.3, w: 5.95, h: 2.45, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("화면 · 변수", { x: 7.05, y: 4.5, w: 5.3, h: 0.35, fontFace: FONT_BODY, fontSize: 12, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  bulletBlock(s, [
    "주요 화면: 캠 화면 (오프닝 화면은 별도일 수 있음, 형식 미정)",
    "화면 이동: 오프닝 → 본편(캠 반복) → 엔딩 컷신 → 종료",
    "핵심 변수: 루프 진행도, 플레이어 상태 레벨 (세부 수치 미정)",
  ], 7.05, 4.9, 5.35, 1.75, { fontSize: 12.5, spaceAfter: 8 });
  pageNum(s, 4);
}

// ---------- Slide 5: Screens, UI, UX ----------
{
  const s = bgSlide(pres);
  kicker(s, "05 · Screens, UI, UX");
  title(s, "화면 / UI / UX");

  placeholder(s, 0.6, 2.0, 6.5, 4.75, "캠코더 뷰파인더 UI 목업\n(타임스탬프 · REC · 배터리 · 노이즈)");

  bulletBlock(s, [
    "캠 화면: 화면 전체가 뷰파인더. 상/하단에 타임스탬프·배터리·REC 표시",
    "노이즈·스캔라인이 루프 진행에 따라 강해짐",
    "핵심 UI 요소: REC 인디케이터, 배터리 표시(연출용, 자원 관리 여부 미정), 노이즈 강도",
    "화면 전환: 오프닝 종료 → 캠 On → 엔딩 컷신 → 캠 낙하로 종료",
    "플레이어 조작에 의한 메뉴 전환 없음",
  ], 7.4, 2.05, 5.3, 4.6, { fontSize: 13.5, spaceAfter: 14 });
  pageNum(s, 5);
}

// ---------- Slide 6: Level Schema (NO IMAGE — text/diagram only) ----------
{
  const s = bgSlide(pres);
  kicker(s, "06 · Level Schema");
  title(s, "레벨 구조 (전통적 \"레벨\" 없음)");

  s.addText("별도의 여러 공간이 아니라, 작은 T자형 맵 하나를 반복 순회하는 구조. 천장 높이가 낮아지며 이동 방식과 표식 매체가 단계적으로 바뀐다.", {
    x: 0.6, y: 1.95, w: 12.1, h: 0.7, fontFace: FONT_BODY, fontSize: 14, color: TEXT, isTextBox: true, margin: 0,
  });

  const phases = [
    ["약 160cm", "테이프 구간", "서서 이동, 눈으로 표식 확인"],
    ["약 100cm", "가이드라인 구간", "구부려 이동, 손으로 표식 확인"],
    ["막다른 분기", "핏자국 구간", "단서 없음, 결과만 남음"],
  ];
  const pw = 3.75, pgap = 0.35, px0 = 0.6, py0 = 2.95;
  phases.forEach((ph, i) => {
    const x = px0 + i * (pw + pgap);
    s.addShape("roundRect", { x, y: py0, w: pw, h: 2.15, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
    s.addText(ph[0], { x: x + 0.3, y: py0 + 0.22, w: pw - 0.6, h: 0.45, fontFace: FONT_HEAD, fontSize: 20, bold: true, color: AMBER, isTextBox: true, margin: 0 });
    s.addText(ph[1], { x: x + 0.3, y: py0 + 0.75, w: pw - 0.6, h: 0.4, fontFace: FONT_BODY, fontSize: 14, bold: true, color: TEXT, isTextBox: true, margin: 0 });
    s.addText(ph[2], { x: x + 0.3, y: py0 + 1.2, w: pw - 0.6, h: 0.8, fontFace: FONT_BODY, fontSize: 12, color: MUTED, isTextBox: true, margin: 0 });
    if (i < phases.length - 1) {
      s.addText("→", { x: x + pw, y: py0 + 0.7, w: pgap, h: 0.7, align: "center", valign: "middle", fontFace: FONT_BODY, fontSize: 20, color: STONE, isTextBox: true, margin: 0 });
    }
  });

  statCard(s, 0.6, 5.4, 3.9, 1.35, "7회", "총 반복 루프 수");
  statCard(s, 4.65, 5.4, 3.9, 1.35, "T자형", "반복 갈림길 구조");
  statCard(s, 8.7, 5.4, 4.0, 1.35, "불필요", "레벨 에디터 — 고정 배치 + 코드 상태 전환");
  pageNum(s, 6);
}

// ---------- Slide 7: First Time Experience ----------
{
  const s = bgSlide(pres);
  kicker(s, "07 · First Time Experience");
  title(s, "첫 플레이 경험");

  const rows = [
    ["온보딩", "별도 텍스트 튜토리얼 없음. 오프닝 대화(친구와 담력체험 설정)로 자연스럽게 진입."],
    ["필수 전달 개념", "\"표식 = 방향 단서\" — 친구가 남겼다는 서사로 암시. 명시적 튜토리얼 텍스트 없음."],
  ];
  let ry = 2.1;
  rows.forEach((r) => {
    circleIcon(s, 0.6, ry, 0.65, "●");
    s.addText(r[0], { x: 1.5, y: ry - 0.05, w: 3, h: 0.7, fontFace: FONT_BODY, fontSize: 15, bold: true, color: AMBER, valign: "middle", isTextBox: true, margin: 0 });
    s.addText(r[1], { x: 4.6, y: ry - 0.1, w: 8.1, h: 0.9, fontFace: FONT_BODY, fontSize: 13.5, color: TEXT, valign: "middle", isTextBox: true, margin: 0 });
    ry += 1.15;
  });

  statCard(s, 0.6, 5.0, 12.1, 1.75, "20분 이내", "오프닝 포함 전체 소요 시간");
  pageNum(s, 7);
}

// ---------- Slide 8: Team, Schedule, Budget ----------
{
  const s = bgSlide(pres);
  kicker(s, "08 · Team, Schedule, Budget");
  title(s, "팀 / 일정 / 예산");

  s.addShape("roundRect", { x: 0.6, y: 2.4, w: 12.1, h: 3.5, rectRadius: 0.08, fill: { color: CARD }, line: { type: "none" } });
  s.addText("미정", { x: 0.9, y: 2.7, w: 11.5, h: 0.7, fontFace: FONT_HEAD, fontSize: 26, bold: true, color: AMBER, isTextBox: true, margin: 0 });
  s.addText("내부 계획용 — 외부 제출 문서에는 포함하지 않음.", {
    x: 0.9, y: 3.5, w: 11.5, h: 0.6, fontFace: FONT_BODY, fontSize: 14, color: MUTED, isTextBox: true, margin: 0,
  });
  pageNum(s, 8);
}

pres.writeFile({ fileName: "/home/user/HrrorGame/build/동굴_공포게임_기획서.pptx" }).then(() => {
  console.log("done");
});
