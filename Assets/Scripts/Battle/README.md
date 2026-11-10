# Battle スクリプトの置き場所

バトル1試合まわりの C# はこのフォルダに集約します。**クラス名は従来どおり**（namespace 変更なし）。  
「どこに書くか」は下の表を見てください。

## フォルダ早見表

| フォルダ | 何を入れるか | 例 |
|----------|----------------|-----|
| **Core** | 試合の中心・状態 | `BattleManager`, `GameState`, `PlayerStatus` |
| **Bootstrap** | 開始時の配線 | `BattleBootstrap` |
| **Phase** | フェーズ遷移 | `BattlePhaseController` |
| **Combat** | ダメージ・命中・属性 | `BattleProcessor`, `ElementHelper` |
| **Hand** | 手札・ドロー・リロード | `HandRefillService`, `CardDealer` |
| **Economy** | 買う・売る・両替 | `BuyFeature`, `ExchangeFeature` |
| **Player** | プレイヤー入力・祈り | `PlayerInputController`, `PrayerFlow` |
| **Enemy** | CPU・敵AI | `EnemyAI`, `EnemyCpuAttackPlanner` |
| **Coordination** | まとめ役 Coordinator | `GameEndOrchestrator`, `SummonSkillCoordinator` |
| **Rules** | 戦闘ルール（カード1枚専用以外） | `CardRule`, `DisadvantageRules` |
| **Sequence** | カード演出シーケンス | `CardSequenceManager` |
| **Magic** | 魔法プール | `MagicPoolManager` |
| **Online** | ローカル側の同期サービス | `OnlineBattleSyncService` |
| **Audio** | BGM・SE | `SoundEffectPlayer` |
| **Debug** | デバッグパネル | `BattleDebugTools` |
| **Hosts** | Coordinator 用 `I*Host` だけ | `IPlayerInputHost` など |
| **UI** | バトル画面の見た目 | `BattleUIManager`, 手札UI など |
| **Reflection / Orb / …** | 特定メカニクス | 既存サブフォルダのまま |

## 新規コードの置き分け（3問）

1. **1枚のカードだけの話？** → `Assets/Scripts/Card/SpecialEffect/`
2. **画面表示だけ？** → `Battle/UI/`
3. **それ以外の試合ロジック** → 上表のフォルダ（迷ったら **Coordination** か **Rules**）

## バトル外

| 場所 | 内容 |
|------|------|
| `Scripts/Card/` | カードデータ・効果（`Data/`, `SpecialEffect/`） |
| `Scripts/StatusEffect/` | 状態異常 |
| `Scripts/Summon/` | 召喚獣 |
| `Scripts/Network/` | 通信・`RemotePlayerAgent` |
| `Scripts/UI/` | タイトル・メインメニューなど |

## Host パターン

Coordinator は **`Hosts/` のインターフェース**経由で `BattleManager` とつなぐ（`BattleManager.I` 直参照は避ける）。

敵用 Host（`IEnemyTurnHost` など）は **Enemy/** に置いています。
