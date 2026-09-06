# 劣勢状態（Disadvantage）判定

バトル中、各プレイヤーは **自分の StandBy フェーズ開始時** に劣勢抽選を1回行う。  
一度劣勢に入る（ラッチ）と **バトル終了まで解除されない**。アルティメットスキルは試合中1回のみ。

実装: [`DisadvantageRules.cs`](DisadvantageRules.cs)

---

## 抽選タイミング

| 対象 | タイミング |
|------|------------|
| プレイヤー | プレイヤーの StandBy（`OnTurnStart` の後） |
| 敵 | 敵の StandBy |

- ラッチ済みの側は再抽選しない。
- 成功時: 劣勢ラッチ → BGM 切替（プレイヤーのみ）→ プレイヤーは Ultimate Ready 演出。
- 敵ターン中の即時 BGM 切替は行わない。

乱数: [`BattleRandom`](../Network/BattleRandom.cs)（オンライン同期用）。

---

## 合算ルール

- 成立している **各ボーナス** を合算する。
- **合計上限 100%**（100% 以上は 100% として抽選）。
- 100% の場合は抽選なしで確定ラッチ。
- 経過ターン **50 以上** のとき、**各ボーナス（0% の条件を除く）に +10%** してから合算。

---

## 条件一覧

### ① 絶対的リソース不足

対象: `currentHP + currentMP + currentGP`

| 合計 | ボーナス |
|------|----------|
| **10 以下** | **+100%** |
| **11〜20** | 指数補間（下記） |
| 21 以上 | +0% |

**11〜20 の式**（例: MP=GP=0 のとき HP だけで見た場合）:

```
bonus(T) = 11.96 × 1.196^(19 − T)   （T は合計、11 ≤ T ≤ 20）
```

参考値:

| 合計 T | bonus(%) |
|--------|----------|
| 20 | 10.0 |
| 19 | 11.96 |
| 18 | 14.30 |
| 17 | 17.10 |
| 11 | 41.81 |

---

### ② 相対的リソース差

#### HP ギャップ（3 自ターン継続）

- **自 StandBy ごと** に `相手HP − 自分HP ≥ 20` ならカウンタ +1、**未満なら 0 にリセット**。
- カウンタ **≥ 3** かつ現在もギャップ成立時のみ:

| 条件 | ボーナス |
|------|----------|
| 相手HP − 自分HP ≥ 20 | +20% |
| さらに ≥ 30 | +10% |

#### 合計ギャップ（即時）

`相手(HP+MP+GP) − 自分(HP+MP+GP)`

| 条件 | ボーナス |
|------|----------|
| ≥ 50 | +30% |
| さらに ≥ 70 | +20% |

---

### ③ 重篤な状態異常

複数付与中は **各異常のボーナスを合算**。

| 状態異常 | ボーナス |
|----------|----------|
| 煉獄病、混乱 | +10% |
| 重病、衰弱、拘束、群発頭痛、呪縛 | +5% |

**意図的に対象外**: 病、楽園病、凍結、介入、眼精疲労、煙幕、不運、濃霧 など。

---

### ④ 手札差

**前提**: 自分の手札枚数 **&lt; 10**（初期手札より少ない＝手札を失った状態）。

`相手手札 − 自分手札`:

| 条件 | ボーナス |
|------|----------|
| ≥ 3 | +10% |
| さらに ≥ 5 | +10% |
| さらに ≥ 7 | +20% |

ガルーダ（開幕12枚）も `< 10` の条件は同じ。

---

### ⑤ 試合長引き防止

- `CurrentBattleTurnDisplay ≥ 50` のとき、上記 **各ボーナス（>0 のもの）に +10%**。
- バハムート Mega Flare 解禁ターン（50）と同じ閾値。

---

## ラッチ後の効果

| 効果 | 条件 |
|------|------|
| 劣勢 BGM・背景 | プレイヤーがラッチ済み |
| Ultimate Ready 演出 | プレイヤーがラッチした StandBy |
| アルティメットスキル解禁 | ラッチ + 未使用 + 召喚にアルティメットあり |
| 召喚虹枠 | 同上（Ultimate 使用後は消灯） |
| SR+ ドロー演出強化（白フラッシュ・カットイン） | プレイヤーがラッチ済み |
| バハムート Giga Flare | ラッチ + 未使用アルティメット |
| **レア度別ドロー重み** | ラッチ済み側の抽選のみ（下記） |

`hasUsedUltimateSkill` 後も **劣勢ラッチ自体は維持**（BGM・SR+ 演出・劣勢ドロー重みは継続）。

---

## 劣勢時ドロー確率

設定: [`Resources/CardDrawTable.asset`](../../Resources/CardDrawTable.asset)（[`CardDrawTableSO`](../Card/CardDrawTableSO.cs)）

- 通常重み: `commonDefaultWeight` など（Header: Default draw weight）
- **劣勢時重み**: `disadvantageCommonWeight` など（Header: Disadvantage draw weight）
- 数値が大きいほど出やすい。`customDrawWeight >= 0` のカードは個別重みが優先。
- ラッチ済みプレイヤー／敵それぞれ、**自分側のドロー**から劣勢重みプールを使用（`CardDealer`）。

---

## デバッグ

### ラッチ条件ダンプ

劣勢ラッチ成功時（Editor / Development ビルド）に Console へ出力:

- 合算抽選 %
- 自／敵リソース・HP差・連続ターン数
- **成立した条件ごと**（基本 % → 長期戦 +10 → 適用 %）

`PlayerStatus.lastDisadvantageLatchDump` に同一文字列を保存。

例:

```
[Disadvantage] プレイヤー latched at turn 4 (roll 45%)
  Resources self=18 (HP18/MP0/GP0) enemy=42 (HP42/MP0/GP0)
  HP gap=24, consecutiveOwnTurns=3
  Met conditions:
    - Absolute resource band 11-20 (total=18): +14% => +14%
    - HP gap >= 20 for 3+ own turns (gap=24, streak=3): +20% => +20%
    - Status: 衰弱: +5% => +5%
```

### その他

- `DisadvantageRules.ComputeBreakdown(...)` — 次 StandBy の合算 % 見積もり（Editor / Development）
- `BattleManager.DebugForcePlayerDisadvantageLatch()` — 強制ラッチ + 演出
- `PlayerStatus.DebugResetDisadvantageState()` — ラッチ・HP差カウンタリセット

---

## 変更履歴

- 旧: `HP+MP+GP ≤ 10` で即時劣勢（`UpdateStatus` 都度）。
- 新: StandBy 抽選 + 複数条件合算。ラッチ後はバトル終了まで維持。
