# DiagnosticScenarios Docker Setup

このプロジェトは Docker コンテナとして実行することができます。

## 必要な環境

- Docker Desktop または Docker Engine
- Docker Compose

## ビルドと実行

### 方法 1: Docker Compose を使用（推奨）

```bash
# コンテナをビルドして起動
docker-compose up --build

# バックグラウンドで実行
docker-compose up --build -d

# コンテナを停止
docker-compose down
```

### 方法 2: Docker コマンドを直接使用

```bash
# Dockerイメージをビルド
docker build -t diagnostic-scenarios .

# コンテナを実行
docker run -p 8080:80 -p 8443:443 diagnostic-scenarios
```

## アクセス方法

アプリケーションが起動したら、以下の URL でアクセスできます：

- HTTP: http://localhost:8080
- HTTPS: https://localhost:8443
- API エンドポイント: http://localhost:8080/api/values

## 開発モード

開発時にソースコードの変更を即座に反映させたい場合は、`docker-compose.override.yml`を作成して以下の設定を追加できます：

```yaml
version: "3.8"
services:
  diagnosticscenarios:
    volumes:
      - .:/app/src
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

## ログの確認

```bash
# 実行中のログを表示
docker-compose logs -f

# 特定のサービスのログのみ表示
docker-compose logs -f diagnosticscenarios
```

## トラブルシューティング

- ポート 8080 や 8443 が既に使用されている場合は、`docker-compose.yml`の ports 設定を変更してください
- SSL 証明書のエラーが発生する場合は、開発環境では HTTP のみを使用することを検討してください
