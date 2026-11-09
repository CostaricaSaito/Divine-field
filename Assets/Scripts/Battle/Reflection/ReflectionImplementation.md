# 反射カード（物理反射・反射剣）実装仕様

本文は UTF-8（BOM 付き）で保存すること。

## 1. 概要

相手の攻撃を跳ね返すカード機構。サンプルは **反射剣（MirrorSword）**（物理反射）。  
実装コードは `Assets/Scripts/Battle/Reflection` に置く。

反射には **物理反射 / 魔法反射 / 完全反射** の3種があり、将来拡張する。本ドキュメントの詳細は **物理反射** を主対象とする。

---

## 2. 物理反射が跳ね返せる攻撃

- **最終の合算属性が `ElementType.None`** のときのみ（`ElementHelper.GetCombinedElement` と整合）。
- **魔法単体**（例：ファイアボールのみ）は魔法攻撃扱い → **物理反射不可**。
- **魔法＋武器のコンボ**（例：ファイアボール＋ソード）は通常攻撃扱い → 合算が無属性なら **物理反射可**。
- 複数枚でも、混在により無属性に収束しない場合は不可。無属性に収束すれば可。

---

## 3. 跳ね返す攻撃内容

- 相手の **カード合計攻撃力**について、**加護・衰弱等を含む既存パイプライン適用後の値をそのまま** 臨時攻撃として使用する。
- 攻撃力は **チェーン開始時に元の攻撃者・元の防御者で1回だけ計算** し、以後の跳ね返しでは固定。
- 反射剣は **防御力なし**。防御カードとしては使えず **反射専用**。

---

## 4. 反射不可時の UI

- 条件を満たさないとき、反射カードは **選択不可・グレーアウト**。
- **通常の防御カードとしては使えない**。

---

## 5. 防御フェーズの操作

- 反射カードを選んだら **他カードを選べない UI ロック**。
- 反射を **キャンセル** したら選択は **ニュートラル** に戻る。
- 反射確定時は **反射カード1枚単独**のみ（他防御カードとの併用不可）。

---

## 6. 敵 AI

- 可否はプレイヤーと **同一ルール**。
- **使用できる場合は反射を優先**。

---

## 7. 解決フロー（`ReactiveDefenseChain`）

反射・打ち払い（パリィ）・オーディン「切り払い」は **すべて `ReactiveDefenseChain.RunAsync` の1本** で解決する。
通常防御フェーズ（`CardSequenceManager` / `EnemyDefenseResolver`）、オーブ即時効果（`OrbIceageFreezeFlow`）、
道連れの鎖（`DeadlyChainFlow`）、オーディン割り込み（`OrdinSlashReflectFlow`）はチェーンを呼ぶだけで、独自の反射処理を持たない。

### 7.1 防御応答の分類（`ReactiveDefenseRules.Classify`）

触媒を除いた戦闘カードが **ちょうど1枚** のとき、次の優先順で判定する。

1. `ReflectionRules.CanReflectIncoming` → **Reflect**
2. `ParryRules.CanParryIncoming` → **Parry**
3. `BlockingRules.ResolvesAsBlockingNullify` → **Nullify**
4. それ以外 → **Normal**（通常防御／防御なし）

Reflect / Parry はチェーンを開始する。呼び出し側の分岐順は **チェーン → 無効化 → 完全反射のみ受付の即時効果 → 通常戦闘**。

### 7.2 チェーンの状態

- **Holder** = 現在攻撃を持っている側。攻撃カードのシートと TOTAL の ATK は **Holder のパネルにだけ** 出る。
- **跳ね返し**（反射成功／打ち払い成功／オーディン）: 攻撃カードを防御側パネルへスライド → Holder 交代 → 防御シーケンス表示をクリア → TOTAL を新 Holder 側に再表示。
- **打ち払い失敗**: 攻撃は動かない。**同じ防御側が防御を選び直す**（このときオーディン判定はしない）。
- 跳ね返した後の新しい防御側（元の攻撃者側）も、反射・打ち払い・通常防御・防御なしを選べる。**連鎖回数に上限なし**。

### 7.3 最終解決

最後に跳ね返さなかった防御側が攻撃を受ける。

| 状況 | 解決 |
|---|---|
| 一度も跳ね返っていない | `ResolveCombatAsync`（命中判定スキップ） |
| 跳ね返った後 | `ResolveReflectedCombatAsync`（固定攻撃力） |
| 即時効果（状態異常魔法・オーブ等） | 元の使用者を user、最終防御側を target として `ResolveImmediateEffectAsync` |
| 道連れの鎖 | 呼び出し側が渡す `CombatResolver` で差し替え |

### 7.4 カード消費

- 応答カードは **解決前に消費** する。消費済み（裏向き／今ターン使用済み記録あり）なら二重消費しない。
- プレイヤーの最初の応答は `CardSequenceManager` で消費済み（`alreadyConsumed: true`）。それ以外は未消費として渡す。

---

## 8. TOTAL ATK/DEF 表示（チェーン中）

チェーン中は `TotalAtkDefPanelResolver.TryResolveReflectionPanel` の **1分岐だけ** で決まる（他のフォールバックは通らない）。

- **Holder 側**: `ATK {固定攻撃力}`、属性は incoming 攻撃の合算属性（道連れの鎖中はその文脈の属性を優先）。
- **反対側（防御側）**: その側の「防御」シーケンスカード、またはプレイヤー防御入力中は選択中カードの DEF のみ表示。
- 反射・打ち払い・無効化カードや DEF 0 以下は **表示しない**。
- チェーン終了時（正常終了・例外とも）に防御シーケンスと反射 TOTAL をクリアする。

---

## 9. 演出

### 9.1 確定時（③）

- **DamagePopup** でメッセージ **「弾き返す」**。
- **背景は赤**、**文字色は虹色**。
- SE: `Assets/SE/power32(DF弾く).wav`（他 SE と同様の Addressable キー形式を想定）。
- ポップアップ消滅後、**規定インターバル**（`fadeDuration` + `PostPopupIntervalMs` 系）を待機。

### 9.2 成功時のカード移動（④）

- 相手が出した **攻撃カードの UI（Prefab インスタンス）をそのまま**移動（触媒を含むターンの攻撃一式。incoming がターンの攻撃でない場合は incoming のみ）。
- **複数枚なら全枚**。**縦移動なし・横スライドのみ**。
- **500ms**、**線形イージング**。
- プレイヤーが跳ね返した場合: `EnemyCardDisplayPanel` → `CardDisplayPanel` へ **左へスライド**。
- 相手が跳ね返した場合: `CardDisplayPanel` → `EnemyCardDisplayPanel` へ **右へスライド**。
- 開始／終了位置は **既存パネルのアンカー**に合わせる。

---

## 10. データ

- サンプル: `Assets/Resources/Cards/01_ATTACK/MirrorSword.asset`
- 攻撃として **攻撃力 10** の通常攻撃カードとしても使用可能（仕様どおり）。

### CardData 拡張案（Inspector）

- `ReflectionKind` 列挙: `None` / `Physical` / `Magic` / `Full`
- `CardData` に `reflectionKind`（既定 `None`）。
- **MirrorSword**: `Physical`、`attackPower = 10`、`defensePower = 0`。
- 選択まわりは既存 `SelectionRole` の **Standalone 相当**＋反射選択時の UI ロックで実装。必要なら `SelectionRole` に反射用を追加。

---

## 11. 実装時の注意（メタ）

- 反射・打ち払いの新しい入口を足すときは **`ReactiveDefenseChain.RunAsync` を呼ぶ**。呼び出し側でスライド・TOTAL・消費を個別に書かない。
- 「弾き返す」ポップアップは **虹色背景（左下→右上の斜めグラデ＋色相スクロール）＋直立の赤字＋白縁**（`DamagePopup.SetupReflectionBounce` / `RainbowGradientScrollImage`）。

---

## 12. 関連コード

- 分類: `ReactiveDefenseRules`、`ReflectionRules`、`ParryRules`、`BlockingRules`
- 解決: `ReactiveDefenseChain`、`BattleProcessor`
- TOTAL: `TotalAtkDefPanelResolver`、`CombatSnapshotStore`（`SetReflectionAttackTotalDisplay`）
- スライド: `BattleCardUIController.SlideReflectionAttackSheetsAsync`
- 属性合算: `ElementHelper`
- カード選択: `CardSelectionManager`、`CardRules`
- ポップアップ: `BattleUIManager`、`DamagePopup`

---

*最終更新: 反射・打ち払い・オーディンを ReactiveDefenseChain に一本化*
