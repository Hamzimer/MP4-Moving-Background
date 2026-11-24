# Moving Background

[English](../README.md) · [日本語](README.ja.md) · [繁體中文](README.zh-Hant.md)

手元のMP4を、無音で繰り返し再生するデスクトップ壁紙にできます。Macではディスプレイごとに別の動画を指定でき、デスクトップが隠れると自動で一時停止します。

[GitHub Releasesからダウンロード](https://github.com/Hamzimer/MP4-Moving-Background/releases/latest)

![Mac版の実際の設定画面。既定の壁紙とディスプレイ別の設定を表示](macos-settings.jpg)

## 主な機能

| | macOS | Windowsプレビュー |
| --- | --- | --- |
| 操作 | メニューバーと設定ウインドウ | 通知領域 |
| ディスプレイ | 共通の動画とディスプレイごとの動画を設定 | メインディスプレイ |
| 再生 | 無音ループ。デスクトップが隠れたとき、画面スリープ中、セッションが非アクティブのとき、低電力モード中は一時停止 | 無音ループ。他のアプリが前面にあるときや画面ロック中は一時停止 |
| 表示言語 | 日本語・英語・繁体字中国語 | 日本語・英語・繁体字中国語 |

Mac版は設定画面下部の **表示言語** で切り替えられます。変更はすぐに反映され、次回起動時にも引き継がれます。**システム設定に従う** を選ぶとmacOSの設定に戻ります。Windows版はOSの表示言語に従います。MP4は元の場所から読み込みます。

## インストール

- **macOS 26以降、Apple Silicon・Intel** — [DMG](../dist/Moving-Background-macOS.dmg?raw=true)を開き、アプリを「アプリケーション」へドラッグします。起動後、設定でMP4を選んでください。このビルドはアドホック署名で、Appleの公証は未取得です。
- **Windows 10・11 x64** — [Windows用MSI](https://github.com/Hamzimer/MP4-Moving-Background/releases/latest/download/Moving-Background-Windows-x64.msi)をインストールし、スタートメニューからアプリを開きます。壁紙のMP4は通知領域から選べます。署名なしのプレビュー版で、.NET Framework 4.8が必要です。旧EXE版をインストール済みの場合は、先にWindowsの設定からアンインストールしてください。

[SHA-256チェックサム](https://github.com/Hamzimer/MP4-Moving-Background/releases/latest/download/SHA256SUMS.txt) · [不具合の報告](https://github.com/Hamzimer/MP4-Moving-Background/issues)

気に入ったら、GitHubでスターを付けていただけると励みになります。
