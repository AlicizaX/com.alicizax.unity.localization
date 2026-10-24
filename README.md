# AlicizaX Localization

本包提供 Luban 多语言词库、运行时入口 `LocalizationKit`、TMP 绑定、编辑器预览和独立的导出 CLI。包安装后不会自动创建项目文件；没有多语言配置的项目可以正常进入 Play。需要启用时，在 Unity 执行 **AlicizaX / Localization / Initialize**，或在命令行执行包内 CLI 的 `init` 命令。

## 项目配置

新项目初始化默认在 Unity 工程父目录的 `Config/Excels/Localization` 创建 `Localization.ini`、`LocalizationConst.xlsx`、`Localization_Default.xlsx` 和 `gen-localization.bat/sh`。这是唯一可编辑的多语言配置入口；`unity_project` 指向实际 Unity 工程，默认的 `source=.` 从 ini 同目录读取 Excel。`LocalizationConst.xlsx` 保存常量，`Localization_*.xlsx` 保存普通配置表的文案，可以按 Item、Quest 等业务拆成多张。表中的 `Texts` 工作表包含 `key`、`layer` 和各语言列；语言列使用 Luban 的 `string?`，空单元格为 null，两个双引号 `""` 为有效空字符串。

`Localization.ini` 中的路径相对于自身所在目录：

| 配置项 | 作用 |
|---|---|
| `unity_project` | Unity 工程目录；脚本据此查找该工程已安装的 CLI |
| `source` | Excel 目录，默认 `.` |
| `languages`、`default_locale` | 语言列及首次语言 |
| `luban_dll` | 已构建的 Luban CLI DLL |
| `ordinary_schema` | 可选；普通表 `luban.conf`，用于检查 `text` 引用 |
| `base_data_out`、`base_code_out` | base bytes 和 `BootstrapLocalizationKey.cs` 输出目录 |
| `patch_data_out`、`patch_code_out` | patch bytes 和 `LocalizationKey.cs` 输出目录 |
| `accessor_mode` | `resolved` 直接返回当前译文；`raw` 返回 key |

初始化后先配置 `luban_dll` 的实际路径并填入文案行。`init --config` 可指定其他绝对路径，但文件名必须是 `Localization.ini`；初始化按实际路径填写 `unity_project` 和默认输出路径，重复执行不会覆盖已有配置。生成时 CLI 会核对 ini 的 `unity_project` 与传入工程是否一致。一层有 Excel 行，且该层两个输出目录都有值，才会启用。单层数据目录必须位于 `Assets` 下的 Resources；双层项目在启动时读取 base，并在资源系统就绪后通过 `LocalizationKit.EnablePatchAsync()` 挂载 patch。只填某层一个输出目录表示禁用该层。新项目默认仅启用 patch 层，但把 bytes 放在 Resources，生成 `LocalizationKey.cs`，无需热更系统。

当前模板工程的 ini、离线脚本和三张 Excel 都位于仓库根目录的 `Config/Excels/Localization`；`unity_project=../../../Client` 指向 Unity 工程。普通配置生成仍只读取仓库 `Config/config.ini`，与多语言生成互不触发；普通表的 Luban 模板可生成 `NameKey` 和集中刷新的 `Name`，多语言包自身不依赖项目 `Game.Config`。

## 生成命令

Unity 菜单 **AlicizaX / Config / Generate Localization** 与离线 `gen-localization.bat/sh` 调用包内同一个 .NET 8 CLI。源码在包根目录的 `Editor/Generation/Cli~/Program.cs` 与 `Workbook.cs`，项目文件是同目录的 `Localization.Cli.csproj`，可运行入口是 `Localization.Cli.dll`。`Cli~` 末尾的波浪号让 Unity 不导入这些独立 .NET 文件，所以应从文件系统或 IDE 打开该目录，不能依赖 Unity Project 窗口查找。使用项目只需 .NET 8 Runtime，不需要安装 SDK；包本身不复制 Luban DLL。离线脚本先读取 ini 的 `unity_project`，再查找该工程的嵌入式包或唯一的 `Library/PackageCache` 安装。CLI 支持：

```text
dotnet <包根>/Editor/Generation/Cli~/Localization.Cli.dll init --project <Unity工程根> --package <包根> [--config <Localization.ini路径>]
dotnet <包根>/Editor/Generation/Cli~/Localization.Cli.dll generate --config <Localization.ini路径> --project <Unity工程根> --package <包根>
```

CLI 读取 Excel 的 `Texts` 行，校验同层重复 key、格式参数与普通表文案引用，再由 Luban 按包内 `Editor/CatalogSchema.xml` 导出词库二进制，在 `Library/LocalizationGeneration` 暂存并提交产物。有效的常量访问器按层生成，调用 `LocalizationKit.Get/Format`。`Assets/Resources/Localization/Settings.json` 由 ini 推导并生成，供启动阶段读取，不是第二处可编辑配置。null 继承 base，空字符串覆盖 base；协议仍是 Luban `TextEntry(key:string, value:string?)`，没有自定义存在位或数值 ID。

## 运行时与卸载

`LocalizationKit` 在首场景加载前读取 Resources 词库，无需往场景添加注册组件。`LocalizedText` 通过 key 自动刷新 TMP 文本；业务代码可使用生成的常量访问器，或调用 Kit 的 `Get`、`Format`、`SetLanguageAsync`。双层项目在资源就绪后显式调用一次 `EnablePatchAsync`，切换语言时仍会加载对应 patch。格式化由 ZString 完成。

移除包时，包内菜单、CLI、运行时入口随之移除；项目拥有的 Excel、ini、词库 bytes、生成访问器、业务代码及 Prefab 不会自动删除。业务代码或 Prefab 若引用多语言 API，需先处理这些引用再卸包。普通表的 `gen-all.bat/sh` 和 `config.ini` 不依赖本包。
