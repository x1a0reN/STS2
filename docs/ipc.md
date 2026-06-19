# GongDou STS2 Challenge IPC

## 管道

- 控制管道：`gongdou.mod.ipc.v1`
- 视频管道：`gongdou.mod.ipc.v1.video`

控制管道沿用 GongDou 客户端现有格式：

```text
[4-byte little-endian payloadLength][UTF-8 JSON payload]
```

请求：

```json
{ "id": "1", "method": "mod.launch.consumeContext", "payload": {} }
```

响应：

```json
{ "id": "1", "ok": true, "data": {} }
```

## 启动流程

1. MOD 启动后异步连接控制管道。
2. 调用 `mod.launch.consumeContext`，`executableName` 固定为 `SlayTheSpire2.exe`。
3. 根据返回的 `leaderboardId` 调用 `mod.leaderboard.getRuleConfig`。
4. 仅接受 `challengeType = slay_the_spire_2_puzzle`。
5. MOD 在游戏内显示准备界面，玩家选择本局卡牌、药水、遗物；不使用客户端配装浮层。
6. MOD 在游戏主线程创建挑战 Run，并直接进入 `破损训练偶` 战斗。
7. 挑战结束后调用 `mod.battle.submit`；客户端根据提交结果显示结算浮层。
8. 如果服务端返回 `needRecording=true`，客户端自动匹配 MOD 视频管道生成的 MP4 并上传。

## 视频帧协议

视频管道发送 raw RGBA 帧：

```text
[Magic 4B = HBRR / 0x52524248]
[Length 4B]
[Width 4B]
[Height 4B]
[PixelFormat 4B = 1 for RGBA32]
[Flags 4B]
[Payload]
```

录像是 best-effort：录像失败不会阻断挑战，但会写入提交 evidence。
