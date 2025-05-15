# FaceAnim Optimizer

## AAO Merge Helper のインストール

 [インストールページ]()からVCCやALCOMに簡単に追加できます。

## 概要
FaceAnim Optimizerは、まばたきアニメを改変済アバターに最適化するためのUnityエディタ拡張ツールです。
アニメーションのBlendShapeの値を調整することで、表情アニメーションの品質を向上させます。

## 使い方
1. Unityメニューから `21CSX` → `FaceAnim Optimizer` を選択
2. 対象メッシュを設定
3. アニメーションを追加
4. 「現在値を取得」ボタンをクリックしてBlendShape値を取得
5. 出力設定を確認
6. 「調整アニメーション生成」ボタンをクリックして処理実行

> **ヒント**
> - 自動置換機能は、元々AnimatorControllerにセットされているアニメーションかどうかで判断しています。  
> - 参照したくないBlendShapeは、BlendShape取得後チェックを外してください。  
> - アニメーションを複製してAnimatorContorollerの複製やPrefabVariantの生成のみしたい場合は、  
> 取得したBlendShapeのチェックを全て外せば作成できます。  

## 機能
- アバターのBlendShape値を参照して調整アニメーションを生成
- AnimatorController内の元のアニメーションを自動変換
- AnimatorController, Prefabの自動置換/コピー生成/Variant生成

## 環境
- Unity 2022.3.22f1
- VRCSDK 3.8.1-beta.1

## ライセンス
MIT License

## お問い合わせ
不具合やお問い合わせについては以下までお願いします：
- [X / Twitter](https://x.com/pnpnrkgk)
- [Booth](https://l21l.booth.pm/)
