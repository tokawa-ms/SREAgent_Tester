# SRE Agent Tester

このサンプルアプリケーションは、.NET 8.0 で構築されたシンプルなアプリケーションです。
SRE Agent をテストするための複数の障害モードをシミュレーションするための UI と API を提供します。

## ソースコードの取得

このコードをローカルマシンで取得するには、リポジトリをクローンして SREAgent_Tester ディレクトリに移動してください。

## ビルドと実行

### 標準的な方法

ソースコードをダウンロード後、以下のコマンドで webapi を簡単に実行できます：

```dotnetcli
dotnet build
dotnet run
```

### Docker コンテナでの実行

このアプリケーションは Docker コンテナとしても実行できます：

```bash
# Dockerイメージのビルド
docker build -t diagnostic-scenarios .

# コンテナの実行
docker run -p 8080:80 diagnostic-scenarios
```

または、Docker Compose を使用：

```bash
docker-compose up -d
```

## Azure App Service での実行

Azure App Service での実行も可能です。Azure Portal から新しい Web アプリを作成し、ソースコードをデプロイしてください。

## 利用可能な Web UI

このアプリケーションには、障害シナリオをトリガーするための Web UI が用意されています。ブラウザで以下の URL にアクセスしてください：

````
http://localhost:5000/

## 利用可能なエンドポイント

このアプリケーションは特定の URL にアクセスすることで様々な問題のあるシナリオをトリガーします。

### Deadlock（デッドロック）

```http
http://localhost:5000/api/diagscenario/deadlock
````

このメソッドはターゲットをハングアップさせ、多くのスレッドを蓄積させます。

### High CPU usage（高 CPU 使用率）

```http
http://localhost:5000/api/diagscenario/highcpu/{milliseconds}
```

指定した{milliseconds}の間、ターゲットに大量の CPU を使用させます。

### Memory leak（メモリリーク）

```http
http://localhost:5000/api/diagscenario/memleak/{kb}
```

このメソッドは{kb}で指定した量のメモリをリークさせます。

### Memory usage spike（メモリ使用量スパイク）

```http
http://localhost:5000/api/diagscenario/memspike/{seconds}
```

指定した秒数にわたって断続的なメモリスパイクを発生させます。メモリ使用量がベースラインからスパイクし、再びベースラインに戻る動作を数回繰り返します。

### Exception（例外）

```http
http://localhost:5000/api/diagscenario/exception
```

意図的に例外を発生させて、例外処理やエラーログの診断に使用します。

### Task Wait（タスク待機 - 非推奨パターン）

```http
http://localhost:5000/api/diagscenario/taskwait
```

Task.Wait()や Task.Result を使用した非推奨パターンを実装しており、スレッドプール枯渇の問題を発生させます。

### Task Sleep Wait（タスク睡眠待機 - 非推奨パターン）

```http
http://localhost:5000/api/diagscenario/tasksleepwait
```

タスクが完了するまでループでスリープする非推奨パターンで、スレッドプールの枯渇を発生させます。

### Task Async Wait（タスク非同期待機 - 推奨パターン）

```http
http://localhost:5000/api/diagscenario/taskasyncwait
```

await キーワードを使用した正しい非同期パターンの実装例です。

## 参考文献・参照実装

このアプリケーションの実装にあたり、以下の文献や実装を参照し、引用・改変しました。

[Diagnostic scenarios sample debug target](https://github.com/dotnet/samples/tree/main/core/diagnostics/DiagnosticScenarios)（© Microsoft Corporation.）を改変して実装。
