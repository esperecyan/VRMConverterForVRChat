VRM Converter for VRChat
========================
**※ポリゴン数を減らす機能はありません。**

Unityのエディタ拡張です。Unity上のVRMプレハブを複製し、VRChat用に次の設定を行います。

- 視点 (※自動アイムーブメントの設定は行いません)
- リップシンク
- 自動まばたき
- バーチャルキャスト風の表情切り替え
- バーチャルキャスト風のアバター同士の干渉 (未検証・触る側のみ)
- 揺れ物

Unity 5.6.3p1 のプロジェクトに、あらかじめ [VRCSDK] と [UniVRM] をインポートしておいてください。
揺れ物の変換を行う場合は、[Dynamic Bone]アセットを購入する必要があります。

[VRCSDK]: https://api.vrchat.cloud/home/download
[UniVRM]: https://github.com/dwango/UniVRM/releases
[Dynamic Bone]: https://assetstore.unity.com/packages/tools/16743

自動で変換できない設定
----------------------
### 自動アイムーブメント
VRChatの目ボーン取得時の不具合により、ボーンが以下の通りの構造・名前である必要があります。
VRM化を行う前に、次のページを参考に構造・名前を変更します。

- VRC_AvatarDescriptorコンポーネントが設定されたオブジェクト
	- Armature
		- Hips
			- Spine
				- Chest
					- Neck
						- Head
							- LeftEye
							- RightEye

参照:  
VRchatでMMDモデルをアバターとして使う方法——上級者編 — 東屋書店  
<http://www.hushimero.xyz/entry/vrchat-EyeTracking>

### VRChatの仕様による自動まばたきの無効化
「Body」という名前のオブジェクトにブレンドシェイプが設定されている場合に、上から4つのキーが自動まばたき用のキーとして使われます。
オブジェクト名を適用な名前にリネームします。

参照:  
VRchatでMMDモデルをアバターとして使う方法－上級者編 — 東屋書店  
<http://www.hushimero.xyz/entry/vrchat-EyeTracking>

パブリックAPI
-------------
アクセス修飾子が `public` であるクラスやメンバー

ライセンス
----------
Mozilla Public License Version 2.0 (MPL-2.0)
<https://www.mozilla.org/MPL/2.0/>

### *.anim
同梱のアニメーションクリップは、しあ様がパブリックドメインで配布されているファイルを改変したもので、CC0-1.0 とします。
<https://creativecommons.org/publicdomain/zero/1.0/deed.ja>

アニメーションオーバーライドで表情をつけよう — VRで美少女になりたい人の備忘録
<http://shiasakura.hatenablog.com/entry/2018/03/30/190811>
しあ☕️こみいち う26b☕️VR珈琲豆屋さんのツイート: “ご連絡ありがとうございます。 こちらはパブリックドメインとして配布しているつもりですのでご自由に使っていただいて構いません。 VRMからVRChatへの変換エディタにはとても興味がありますので楽しみにしていますね！… ”
<https://twitter.com/shiasakura/status/1042558342768390144>

不具合報告など
--------------
Twitterまでご連絡ください。
