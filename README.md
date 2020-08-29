# NbaLineBot
查詢 NBA 比分的 LINE BOT

對 BOT 輸入日期 (格式 `yyyyMMdd`) 會回傳該日期的比賽

## 使用說明

此專案使用 [LineBotSDK](https://www.nuget.org/packages/LineBotSDK/2.2.26) 套件

下載之後要將 `appsettings.json` 中的 `ChannelAccessToken` 及 `AdminUserId` 改成你自己的

```json
{
  "ChannelAccessToken": "YourChannelAccessToken",
  "AdminUserId": "YourAdminUserId"
}
```

## 試玩

搜尋 @898kobpo 加入好友或掃描 QR Code

![](https://i.imgur.com/fMeQVL8.png)
