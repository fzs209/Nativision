# Nativision（WinUI 3 桌面版）

一个从头写的本地播放器，专门验证一件事：**不用 Windows Media Player，自己写的程序走系统播放管线，能不能同样让显示器进入杜比视界模式。**

> 2026-11-24 起改为 WinUI 3 桌面应用（非打包、自包含）：解压后直接双击 `Nativision.exe` 运行，不再是 UWP，不用装证书、不用旁加载 MSIX。旧 UWP 版已在真机上验证过能触发杜比视界；这个桌面版底层仍是同一个系统播放器对象，但换了应用模型之后是否同样触发，**需要重新真机验证**，不能直接沿用旧结论。

## 为什么这样写

关键不在“用 Media Foundation”这几个字，而在用哪一层：

- 自己拿 Media Foundation 的底层接口一帧一帧解码、再自己画到屏幕上 — 解码完画面就在自己手里了，和 MPC-BE 自带解码器一样，最后送出去的还是普通 HDR 信号，不会触发杜比视界。这条路不要走。
- 这个程序用的是系统播放框架那一层（就是 Media Player、电影和电视、Energy Media Player 用的同一层）：程序只把文件交给系统，解码、动态数据处理、往显示器送信号全由系统管线完成，程序只管界面和控制。能不能触发，取决于系统愿不愿意对这个程序也走和 Media Player 一样的处理 — 这正是要实测的。

所以代码里没有任何自带解码器、没有任何 FFmpeg，这是有意的。

## 功能

- 打开多个文件排成列表，播完自动下一个；拖放打开
- 系统播放控件自带：进度、音量、全屏、倍速按钮、音轨和字幕选择按钮
- 额外控制：上一个/下一个、0.5×–2× 倍速、循环、外挂字幕（.srt/.vtt，在控件的字幕按钮里选）
- 诊断面板：解码分辨率、状态、缓冲领先；一键复制全部诊断文本（显示高级颜色一项在桌面版可能读不到，读不到不影响播放判断）
- 命令行打开：`Nativision.exe "D:\movie.mp4"`，资源管理器里对视频文件选「打开方式」指向这个 exe 也是同一条路

## 直接下载构建好的程序（GitHub Actions）

每次推送到 `main`，GitHub Actions（Windows 机器）会自动编译并发布，不用本地装 Visual Studio：

1. 打开本仓库的 **Actions** 页 → 最近一次 **Build Nativision** 运行 → 页面底部 **Artifacts** 下载 `Nativision-winui3` 并解压。
2. 双击里面的 `Nativision.exe` 直接运行。没有安装步骤，不用装证书，不用开开发人员模式（自包含发布，Windows App Runtime 已打进文件夹，所以体积比旧版大）。
3. 前提和 Media Player 一样：系统已装 HEVC 视频扩展和杜比视界扩展、Windows 设置里 HDR 已开。

## 本地编译运行（备选）

1. 装 Visual Studio 2022，安装时勾选「.NET 桌面开发」和「Windows 应用开发」工作负载（WinUI 3 模板在后者里）。
2. 用 Visual Studio 打开 `Nativision\Nativision.csproj`，平台选 **x64**、配置选 **Debug**，按 F5。
3. 或命令行：`dotnet publish Nativision\Nativision.csproj -c Release -r win-x64 --self-contained true -o publish`

## 验证方法（和之前测 Edge 一样）

1. 先在 Windows Media Player 里播目标 MP4 文件，记住显示器是否进杜比视界、画面颜色。
2. 再在这个程序里播同一个文件，看两点：显示器菜单有没有进杜比视界模式；同一画面颜色和 Media Player 是否一致。
3. 点「复制诊断」把文本粘贴给我，连同上面两点的结果一起发。

结果分三种，后续完全不同：

- **触发了** — 桌面版这条路成立，以后就在这个版本上补功能。
- **能播但只进 HDR10** — 说明系统对非打包桌面应用和对自家 Media Player 的处理不一样；旧 UWP 版的验证结果仍然有效，可以退回 UWP 版继续用。
- **播不了/报错** — 先看诊断面板和红色报错文本，把文本发我，先分清是容器/扩展问题还是管线问题。

## 已知范围

- 预期能测的是 MP4/MOV 里的 Profile 5 / 8，和 Media Player 的范围一致。
- MKV、Profile 7 双层、TrueHD/DTS 无损音轨：系统管线本身就不擅长，预计和 Media Player 一样受限，不要拿这些文件的结果否定整条路线。
- 系统没有给普通应用提供“已进入杜比视界”的读数接口，最终判断永远以显示器菜单为准。
