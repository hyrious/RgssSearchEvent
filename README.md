# RGSS Search Event

RGSS 搜索事件工具，照抄 RMMV 同功能。

暂不支持 RMVX。

## 原理

1. 确保打开了一个正常的工程目录；
2. 从 Game.ini 读取到 rgss.dll 的位置，载入；
3. 运行 dll 里的 RGSSEval 函数，直接执行 rgss 代码，在工程目录生成一个 rgss_search_events.txt；
4. C# 读取这个文件。

### rgss_search_events.txt 文件格式

```
! base64(报错信息)
M 地图编号 base64(地图名称)
S 开关编号 base64(开关名称)
V 变量编号 base64(变量名称)
E 地图编号 事件编号 事件页编号 X坐标 Y坐标 base64(涉及到的开关编号) base64(涉及到的变量编号) 事件名称
```

其中 `涉及到的XX编号` 的格式为 `base64("1 2 3")`。凡是 base64 后为空字符串的，简单替换为 `0` 或任意非 base64 无空格字符串即可。

这么设计的主要目的是不需要安装任何第三方库（如 JSON）。

## License

The MIT license.
