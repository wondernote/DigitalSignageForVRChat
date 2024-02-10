# DigitalSignage for VRChat パッケージ

このパッケージは、VRChatのワールドで使用するデジタルサイネージシステムを提供し、自動的に更新されるイベント情報を表示します。情報は「Wonder Note」から取得され、VRChat内のイベントに関する最新の画像を提供します。

## パッケージの特徴
- 開催日時の近い順に100件のイベント画像を取得し、20秒間隔で表示を更新します。
- 1日に4回の自動取得で、常に最新の情報を提供します。
- PCおよびQuest対応のどちらのワールドでも動作が確認されています。

## 使用方法
1. VCC（VRChat Creator Companion）を使用してパッケージをインポートします。
2. `Runtime`フォルダ内の`DigitalSignage.prefab`（スタンド）、または`DigitalSignage_Wall.prefab`（壁掛け）をワールドに配置してください。
3. `DigitalSignage(_Wall)>SignageContainer>digital_signage(_wall)` を選択すると、インスペクターの「カラー選択」から好みの色を設定できます。
4. 配置できるのは一つのワールドにつき一つまでです。VRChatの通信制限により、複数配置すると正常に機能しない場合があります。

## 依存関係と互換性
- Unityバージョン: 2019.4以上
- VRChat SDK: com.vrchat.worlds >=3.4.2, com.vrchat.base >=3.4.2

## 不具合の報告
問題や不具合を発見した場合は、[contact@wondernote.net](mailto:contact@wondernote.net)までご報告ください。

## アップデートに関して
システムが取得する画像や表示方法などは、ユーザーの要望に応じて将来的に変更される可能性があります。アップデートを行うことで、より役立つサービスを提供していきたいと考えています。

## ライセンス
このパッケージは「WonderNote Digital Signage Custom License」の下で公開されています。詳細は[こちら](https://github.com/wondernote/DigitalSignageForVRChat/blob/main/LICENSE.txt)を参照してください。

